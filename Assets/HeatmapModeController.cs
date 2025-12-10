using API;
using Models;
using Objects;
using System;
using System.Collections;
using System.Collections.Generic;
using UI;
using UnityEngine;

public class HeatmapModeController : MonoBehaviour
{
    [SerializeField] private bool debugLogHeatmap = true;

    [Header("Visual Settings")]
    [SerializeField] private Color baseColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField, Range(0f, 1f)] private float alphaMin = 0.0f;
    [SerializeField, Range(0f, 1f)] private float alphaMax = 1.0f;

    [Header("Refs")]
    [SerializeField] private GameObject locationPrefab;
    [SerializeField] private HeatmapHUDController heatmapHudPrefab;

    private APIscript api;
    private readonly Dictionary<string, GameObject> zoneGo = new();
    private readonly Dictionary<string, Renderer> zoneRenderer = new();
    private readonly Dictionary<string, TaskDTO> taskCache = new();
    private readonly Dictionary<string, Color> originalColors = new();

    [SerializeField] private HeatmapTooltipUI tooltipPrefab;

    private HeatmapTooltipUI tooltipInstance;
    private string currentTooltipLocId = null;

    // dados do último cálculo de heatmap (para tooltip)
    private Dictionary<string, double> lastValuesByZone = new();
    private double lastTotalVal = 0;
    private HeatmapMetric lastMetric;

    // cache “estática” de mapeamentos
    private readonly Dictionary<string, string> activityToLocationCache = new();

    private GameObject hudInstance;
    private HeatmapHUDController hud;
    private DateTime? currentFrom, currentTo;
    private HeatmapMetric currentMetric = HeatmapMetric.Frequency;

    // escala e curva
    [SerializeField] private HeatmapScale currentScale = HeatmapScale.Relative;
    [SerializeField] private HeatmapCurve currentCurve = HeatmapCurve.Linear;
    [SerializeField] private float logCurveStrength = 10f; // k da curva log

    private UIManager uiManager;
    private bool isInHeatmapMode = false;

    private Roof roofController;

    private readonly List<GameObject> firstFloorZones = new();
    private bool firstFloorVisible = true;

    // mapas auxiliares reutilizáveis
    private readonly Dictionary<string, string> locationNameById = new();
    private readonly Dictionary<string, TaskDTO> taskById = new();
    private readonly Dictionary<string, List<string>> activityToLocations = new();
    private bool mappingsPrepared = false;

    private static readonly int PROP_COLOR = Shader.PropertyToID("_Color");
    private static readonly int PROP_BASE_COLOR = Shader.PropertyToID("_BaseColor");

    private void Update()
    {
       
        if (isInHeatmapMode && Input.GetKeyUp(KeyCode.Alpha1))
        {
            if (currentFrom.HasValue && currentTo.HasValue)
            {
                StartCoroutine(ComputeAndApplyHeatmap(
                    currentFrom.Value,
                    currentTo.Value,
                    currentMetric));
            }
        }

        if (isInHeatmapMode)
        {
            HandleHoverTooltip();
        }
    }



    private void Awake()
    {
        api = FindObjectOfType<APIscript>(true);
        uiManager = FindObjectOfType<UIManager>(true);
        roofController = FindObjectOfType<Roof>(true);
    }

    public void EnterHeatmapMode()
    {
        if (isInHeatmapMode) return;
        isInHeatmapMode = true;

        
        firstFloorVisible = true;

        if (uiManager != null) {
            uiManager.InteractableOff();
            uiManager.VirtualGridOff();
        }
        ChangeCameraToTop();
        

        StartCoroutine(SetupAndEnter());
    }

    private static string FormatSeconds(double s)
    {
        var ts = TimeSpan.FromSeconds(s);
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private IEnumerator SetupAndEnter()
    {
        if (api == null) api = FindObjectOfType<APIscript>(true);

        if (heatmapHudPrefab == null)
        {
            Debug.LogError("[Heatmap] 'heatmapHudPrefab' não está ligado no Inspector.");
            yield break;
        }
        if (locationPrefab == null)
        {
            Debug.LogError("[Heatmap] 'locationPrefab' não está ligado no Inspector.");
            yield break;
        }

        var tLoc = api.GetVirtualMapLocationsAsync();
        while (!tLoc.IsCompleted) yield return null;

        if (zoneGo.Count == 0)
        {
            foreach (var loc in api.locations)
            {
                var go = Instantiate(locationPrefab,
                                     new Vector3(loc.coordinateX, loc.coordinateY, loc.coordinateZ),
                                     Quaternion.identity);
                go.name = $"HeatmapZone_{loc.name}";
                zoneGo[loc.id] = go;

                var tag = go.AddComponent<HeatmapZoneTag>();
                tag.locationId = loc.id;

                if (loc.coordinateY > 1f)
                    firstFloorZones.Add(go);

                var area = go.GetComponent<CreatePolygon>();
                if (area != null && loc.vertices != null && loc.vertices.Count >= 4)
                {
                    var verts = new Vector3[4];
                    for (int i = 0; i < 4; i++)
                    {
                        verts[i].x = loc.vertices[i].X;
                        verts[i].z = loc.vertices[i].Z;
                    }
                    area.initialize(verts);
                    area.HideAllSpheres();
                }

                Renderer rend = null;
                var meshCreator = go.GetComponent<CarSlotMeshCreator>();
                if (meshCreator != null && meshCreator.meshRenderer != null)
                    rend = meshCreator.meshRenderer;
                else
                    rend = go.GetComponentInChildren<Renderer>(true);

                var cpl = go.GetComponentInChildren<Objects.ColorPickerLocation>(true);
                if (cpl) cpl.enabled = false;

                if (rend == null)
                {
                    Debug.LogWarning($"[Heatmap] Renderer não encontrado na zona '{loc.name}'.");
                    continue;
                }

                EnsureZoneMaterial(rend);

                Color dbColor;
                if (!TryParseHex(loc.color, out dbColor))
                    dbColor = ReadRendererColor(rend);

                
                originalColors[loc.id] = dbColor;

                
                var c0 = baseColor;
                c0.a = 0f;
                SetRendererColor(rend, c0);


                zoneRenderer[loc.id] = rend;
                go.SetActive(false);
            }
        }

        var canvas = FindObjectOfType<Canvas>(true);
        if (canvas == null)
        {
            Debug.LogError("[Heatmap] Canvas não encontrado na cena.");
            yield break;
        }

        hudInstance = Instantiate(heatmapHudPrefab.gameObject, canvas.transform);
        hud = hudInstance.GetComponent<HeatmapHUDController>();
        if (hud == null)
        {
            Debug.LogError("[Heatmap] Prefab do HUD não contém HeatmapHUDController.");
            yield break;
        }

        hud.OnDateRangeChanged += HandleDateRangeChanged;
        hud.OnMetricChanged += HandleMetricChanged;
        hud.OnExitRequested += ExitHeatmapMode;
        hud.OnScaleChanged += HandleScaleChanged;
        hud.OnCurveChanged += HandleCurveChanged;

        var today = DateTime.Today;
        currentFrom = today.AddDays(-7);
        currentTo = today;
        currentMetric = HeatmapMetric.Frequency;
        currentScale = HeatmapScale.Relative;
        currentCurve = HeatmapCurve.Linear;

        hud.SetDefaults(currentFrom.Value, currentTo.Value, true);

        if (tooltipPrefab != null)
        {
            tooltipInstance = Instantiate(tooltipPrefab, canvas.transform);
            tooltipInstance.Hide();
        }

        StartCoroutine(ComputeAndApplyHeatmap(currentFrom.Value, currentTo.Value, currentMetric));
    }

    private void HandleDateRangeChanged(DateTime? from, DateTime? to)
    {
        if (!from.HasValue || !to.HasValue) return;
        currentFrom = from; currentTo = to;
        StartCoroutine(ComputeAndApplyHeatmap(from.Value, to.Value, currentMetric));
    }

    private void HandleMetricChanged(bool usePosition)
    {
        currentMetric = usePosition ? HeatmapMetric.Frequency : HeatmapMetric.Occupancy;
        if (currentFrom.HasValue && currentTo.HasValue)
            StartCoroutine(ComputeAndApplyHeatmap(currentFrom.Value, currentTo.Value, currentMetric));
    }

    private void HandleScaleChanged(HeatmapScale scale)
    {
        currentScale = scale;
        if (currentFrom.HasValue && currentTo.HasValue)
            StartCoroutine(ComputeAndApplyHeatmap(currentFrom.Value, currentTo.Value, currentMetric));
    }

    private void HandleCurveChanged(HeatmapCurve curve)
    {
        currentCurve = curve;
        if (currentFrom.HasValue && currentTo.HasValue)
            StartCoroutine(ComputeAndApplyHeatmap(currentFrom.Value, currentTo.Value, currentMetric));
    }

    //       --- CÁLCULO / APLICAÇÃO HEATMAP ------
    private IEnumerator ComputeAndApplyHeatmap(DateTime from, DateTime to, HeatmapMetric metric)
    {
        if (api == null) api = FindObjectOfType<APIscript>(true);

        if (api.locations == null || api.locations.Count == 0)
        {
            var tLoc = api.GetVirtualMapLocationsAsync();
            while (!tLoc.IsCompleted) yield return null;
            mappingsPrepared = false;
        }

        if (api.activities == null || api.activities.Count == 0)
        {
            var tHist0 = api.GetActivityAndLocationHistoryAsync();
            while (!tHist0.IsCompleted) yield return null;
        }

        var histories = api.activities;
        if (histories == null || histories.Count == 0)
        {
            Debug.LogWarning("[Heatmap] Sem ActivityAndLocationHistory.");
            HideAllZones();
            yield break;
        }

        if (api.tasks == null || api.tasks.Count == 0)
        {
            var tTasks0 = api.GetTasksAsync();
            while (!tTasks0.IsCompleted) yield return null;
            mappingsPrepared = false;
        }

        var allTasks = api.tasks;
        if (allTasks == null || allTasks.Count == 0)
        {
            Debug.LogWarning("[Heatmap] Lista de tasks veio vazia.");
            HideAllZones();
            yield break;
        }

        PrepareStaticMappings(allTasks);

        var filtroInicio = from.Date;
        var filtroFim = to.Date.AddDays(1);

        if (debugLogHeatmap)
            Debug.Log($"[Heatmap][DEBUG] Filtro datas => {filtroInicio:yyyy-MM-dd HH:mm} .. {filtroFim:yyyy-MM-dd HH:mm}");

        var valuesByZone = new Dictionary<string, double>();
        int entradasContadas = 0;

        foreach (var carHist in histories)
        {
            var carId = carHist?.CaseInstanceId ?? "(sem caseInstanceId)";
            if (carHist?.History == null) continue;

            foreach (var entry in carHist.History)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ActivityId))
                {
                    if (debugLogHeatmap)
                        Debug.Log($"[Heatmap][DEBUG] Car {carId} entry.Id={entry?.Id} sem ActivityId, ignorar.");
                    continue;
                }

                if (!taskById.TryGetValue(entry.ActivityId, out var task) || task == null)
                {
                    if (debugLogHeatmap)
                        Debug.Log($"[Heatmap][DEBUG] Car {carId} entry.Id={entry.Id} ActivityId={entry.ActivityId} sem Task correspondente, ignorar.");
                    continue;
                }

                var start = task.startTime;
                var end = task.completionTime;

                bool conta = false;
                double valor = 0.0;

                if (metric == HeatmapMetric.Frequency)
                {
                    conta = (start >= filtroInicio && start < filtroFim);
                    valor = 1.0;
                }
                else // Occupancy
                {
                    if (end <= start) end = start.AddSeconds(1);

                    var overlapStart = (start > filtroInicio) ? start : filtroInicio;
                    var overlapEnd = (end < filtroFim) ? end : filtroFim;
                    var overlap = (overlapEnd - overlapStart).TotalSeconds;
                    conta = overlap > 0;
                    valor = overlap;
                }

                if (!conta)
                {
                    if (debugLogHeatmap)
                        Debug.Log($"[Heatmap][DEBUG] Task '{task.id}' actId='{task.activityId}' | entry.Id={entry.Id} car='{carId}' fora do intervalo, ignorar. Start={start} End={end}");
                    continue;
                }

                var locIdsToCount = new List<string>();

                if (!string.IsNullOrEmpty(entry.LocationId))
                {
                    locIdsToCount.Add(entry.LocationId);
                }
                else if (!string.IsNullOrEmpty(task.activityId) &&
                         activityToLocations.TryGetValue(task.activityId, out var locList) &&
                         locList != null && locList.Count > 0)
                {
                    locIdsToCount.AddRange(locList);
                }
                else
                {
                    if (debugLogHeatmap)
                        Debug.Log($"[Heatmap][DEBUG] Task '{task.id}' actId='{task.activityId}' | entry.Id={entry.Id} car='{carId}' sem LocationId e sem mapeamento activityIds, ignorar.");
                    continue;
                }

                foreach (var locId in locIdsToCount)
                {
                    var atual = valuesByZone.GetValueOrDefault(locId);
                    valuesByZone[locId] = atual + valor;
                    entradasContadas++;

                    if (debugLogHeatmap)
                    {
                        locationNameById.TryGetValue(locId, out var locName);
                        Debug.Log($"[Heatmap][DEBUG][{metric}] +{valor} em {locName ?? locId} ({locId}) -> total agora = {valuesByZone[locId]}");
                    }
                }
            }
        }

        if (debugLogHeatmap)
            Debug.Log($"[Heatmap][DEBUG] Entradas efectivamente contadas = {entradasContadas}");

        double totalVal = 0;
        double maxVal = 0;
        foreach (var v in valuesByZone.Values)
        {
            totalVal += v;
            if (v > maxVal) maxVal = v;
        }

        if (debugLogHeatmap)
        {
            Debug.Log($"[Heatmap] Intervalo: {from:yyyy-MM-dd} -> {to:yyyy-MM-dd} | " +
                      $"Métrica: {metric} | Σ(total)={totalVal} | max={maxVal} | scale={currentScale} | curve={currentCurve}");
        }

        if (totalVal <= 0.0)
        {
            HideAllZones();

            // também limpamos os dados das tooltips neste caso
            lastValuesByZone.Clear();
            lastTotalVal = 0;
            lastMetric = metric;

            yield break;
        }

        // guardar dados globais para tooltip
        lastValuesByZone = valuesByZone;
        lastTotalVal = totalVal;
        lastMetric = metric;

        var mpb = new MaterialPropertyBlock();

        if (roofController != null && roofController.FirstFloor != null)
            firstFloorVisible = roofController.FirstFloor.activeSelf;
        else
            firstFloorVisible = true;

        foreach (var loc in api.locations)
        {
            valuesByZone.TryGetValue(loc.id, out var v);

            // ESCALA
            float tNorm;
            if (currentScale == HeatmapScale.Relative)
            {
                tNorm = (totalVal > 0.0)
                    ? Mathf.Clamp01((float)(v / totalVal))
                    : 0f;
            }
            else // Absolute
            {
                tNorm = (maxVal > 0.0)
                    ? Mathf.Clamp01((float)(v / maxVal))
                    : 0f;
            }

            // CURVA
            float tCurve = tNorm;
            if (currentCurve == HeatmapCurve.Logarithmic && tNorm > 0f)
            {
                float k = Mathf.Max(1.01f, logCurveStrength);
                tCurve = Mathf.Log(1f + (k - 1f) * tNorm) / Mathf.Log(k);
            }

            if (!zoneGo.TryGetValue(loc.id, out var zoneObj) || zoneObj == null)
                continue;

            if (tCurve <= 0f)
            {
                zoneObj.SetActive(false);
                continue;
            }

            bool isFirstFloor = loc.coordinateY > 1f;
            bool shouldBeActive = true;

            if (isFirstFloor && !firstFloorVisible)
                shouldBeActive = false;

            zoneObj.SetActive(shouldBeActive);
            if (!shouldBeActive)
                continue;

            if (!zoneRenderer.TryGetValue(loc.id, out var rend) || rend == null)
                continue;

            var c = baseColor;
            c.a = Mathf.Lerp(alphaMin, alphaMax, tCurve);

            SetRendererColor(rend, c);
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(PROP_COLOR, c);
            rend.SetPropertyBlock(mpb);

            if (debugLogHeatmap)
            {
                string metricInfo = (metric == HeatmapMetric.Frequency)
                    ? $"{v:0}/{totalVal:0}"
                    : $"{v:0}/{totalVal:0} s";

                Debug.Log($"[Heatmap] {loc.name} => {metricInfo} -> norm={tNorm:0.###} curve={tCurve:0.###}");
            }
        }
    }

    private void PrepareStaticMappings(List<TaskDTO> allTasks)
    {
        if (mappingsPrepared) return;

        locationNameById.Clear();
        foreach (var loc in api.locations)
            locationNameById[loc.id] = loc.name;

        taskById.Clear();
        foreach (var t in allTasks)
        {
            if (t == null || string.IsNullOrEmpty(t.id)) continue;
            if (!taskById.ContainsKey(t.id))
                taskById[t.id] = t;
        }

        activityToLocations.Clear();
        foreach (var loc in api.locations)
        {
            if (loc.activityIds == null) continue;

            foreach (var act in loc.activityIds)
            {
                if (string.IsNullOrEmpty(act)) continue;

                if (!activityToLocations.TryGetValue(act, out var list))
                {
                    list = new List<string>();
                    activityToLocations[act] = list;
                }
                if (!list.Contains(loc.id))
                    list.Add(loc.id);
            }
        }

        mappingsPrepared = true;
    }

    private void ApplyUniformAlpha(float alpha)
    {
        var mpb = new MaterialPropertyBlock();
        foreach (var kv in zoneRenderer)
        {
            var r = kv.Value;
            if (!r) continue;
            var c = baseColor; c.a = alpha;
            SetRendererColor(r, c);
            r.GetPropertyBlock(mpb);
            mpb.SetColor(PROP_COLOR, c);
            r.SetPropertyBlock(mpb);
        }
    }

    public void ExitHeatmapMode()
    {
        StopAllCoroutines();
        isInHeatmapMode = false;

        // destruir completamente as zonas do heatmap
        foreach (var kv in zoneGo)
        {
            if (kv.Value != null)
            {
                kv.Value.SetActive(false);

                if (zoneRenderer.TryGetValue(kv.Key, out var rend) && rend != null)
                {
                    var c0 = baseColor;
                    c0.a = 0f;
                    SetRendererColor(rend, c0);
                }
            }
        }

       
 
        

        zoneGo.Clear();
        zoneRenderer.Clear();
        firstFloorZones.Clear();

        // limpar estado relacionado com o último cálculo
        lastValuesByZone.Clear();
        lastTotalVal = 0;
        currentTooltipLocId = null;
        mappingsPrepared = false;

        // limpar tooltip
        if (tooltipInstance != null)
        {
            tooltipInstance.Hide();
            Destroy(tooltipInstance.gameObject);
            tooltipInstance = null;
        }

        if (uiManager != null) uiManager.InteractableOn();

        if (hud != null)
        {
            hud.OnDateRangeChanged -= HandleDateRangeChanged;
            hud.OnMetricChanged -= HandleMetricChanged;
            hud.OnExitRequested -= ExitHeatmapMode;
            hud.OnScaleChanged -= HandleScaleChanged;
            hud.OnCurveChanged -= HandleCurveChanged;

            Destroy(hudInstance);
            hud = null;
            hudInstance = null;
        }
    }

    public void HideFirstFloorZones()
    {
        foreach (var zone in firstFloorZones)
        {
            if (zone != null)
                zone.SetActive(false);
        }
    }

    public void ShowFirstFloorZones()
    {
        foreach (var zone in firstFloorZones)
        {
            if (zone != null)
                zone.SetActive(true);
        }
    }

    // ---------- Helpers ----------
    private Color ReadRendererColor(Renderer r)
    {
        var mat = r ? r.sharedMaterial : null;
        if (mat == null) return Color.white;

        if (mat.HasProperty(PROP_BASE_COLOR)) return mat.GetColor(PROP_BASE_COLOR);
        if (mat.HasProperty(PROP_COLOR)) return mat.GetColor(PROP_COLOR);
        return Color.white;
    }

    private void SetRendererColor(Renderer r, Color c)
    {
        if (!r) return;
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);

        var mat = r.sharedMaterial;
        if (mat != null && mat.HasProperty(PROP_BASE_COLOR)) mpb.SetColor(PROP_BASE_COLOR, c);
        if (mat != null && mat.HasProperty(PROP_COLOR)) mpb.SetColor(PROP_COLOR, c);

        r.SetPropertyBlock(mpb);
    }

    private void HideAllZones()
    {
        foreach (var kv in zoneGo)
        {
            if (kv.Value != null)
                kv.Value.SetActive(false);
        }
    }

    private void EnsureZoneMaterial(Renderer rend)
    {
        if (!rend) return;
        if (rend.sharedMaterial != null) return;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetFloat("_ZWrite", 0f);
#else
        var mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 3);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
#endif
        rend.sharedMaterial = mat;
        rend.sharedMaterial.renderQueue = 3000;
    }

    private static bool TryParseHex(string hex, out Color c)
    {
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c))
            return true;
        c = Color.white; return false;
    }

    private void ApplyHeatmapBase(float alpha)
    {
        foreach (var kv in zoneRenderer)
        {
            var rend = kv.Value;
            if (!rend) continue;
            var c = baseColor; c.a = Mathf.Clamp01(alpha);
            SetRendererColor(rend, c);
        }
    }

    private void ChangeCameraToTop()
    {
        var cameraSystemObj = GameObject.Find("CameraSystem");
        if (cameraSystemObj == null) return;

        var cameraSystem = cameraSystemObj.GetComponent<CameraSystem>();
        if (cameraSystem != null)
            cameraSystem.SwitchToTopCam();
    }

    public bool TryGetTooltipText(string locationId, out string title, out string body)
    {
        title = "";
        body = "";

        if (lastValuesByZone == null || lastValuesByZone.Count == 0 || lastTotalVal <= 0)
            return false;

        if (!lastValuesByZone.TryGetValue(locationId, out var v) || v <= 0)
            return false;

        string zoneName = locationId;
        if (locationNameById.TryGetValue(locationId, out var name) && !string.IsNullOrEmpty(name))
            zoneName = name;

        title = zoneName;

        double percent = (v / lastTotalVal) * 100.0;

        if (lastMetric == HeatmapMetric.Frequency)
        {
            int zoneCount = (int)Math.Round(v);
            int totalCount = (int)Math.Round(lastTotalVal);

            body = $"Entradas: {zoneCount} / Total: {totalCount} ({percent:0.#}%)";
        }
        else
        {
            TimeSpan ts = TimeSpan.FromSeconds(v);
            string timeStr = $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s";
            body = $"Tempo ocupado: {timeStr} ({percent:0.#}%)";
        }

        return true;
    }

    private void HandleHoverTooltip()
    {
        if (tooltipInstance == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var tag = hit.collider.GetComponentInParent<HeatmapZoneTag>();
            if (tag != null && !string.IsNullOrEmpty(tag.locationId))
            {
                if (currentTooltipLocId != tag.locationId)
                {
                    currentTooltipLocId = tag.locationId;

                    if (TryGetTooltipText(tag.locationId, out string title, out string body))
                    {
                        tooltipInstance.SetText(title, body);
                    }
                    else
                    {
                        tooltipInstance.Hide();
                        return;
                    }
                }

                tooltipInstance.SetPosition(Input.mousePosition + new Vector3(15f, -15f));
                tooltipInstance.Show();
                return;
            }
        }

        currentTooltipLocId = null;
        tooltipInstance.Hide();
    }
}

// helper C# 8
static class DictExt
{
    public static double GetValueOrDefault(this Dictionary<string, double> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : 0d;
}
