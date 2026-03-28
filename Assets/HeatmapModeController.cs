using API;
using Models;
using Objects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using E2C;

public class HeatmapModeController : MonoBehaviour
{
    [Serializable]
    public class HeatmapZonePoint
    {
        public string locationId;
        public string zoneName;
        public float value;
        public float percent;
    }

    [Serializable]
    public class HeatmapChartPayload
    {
        public DateTime from;
        public DateTime to;
        public HeatmapMetric metric;
        public HeatmapScale scale;
        public HeatmapCurve curve;
        public float total;
        public float max;
        public List<HeatmapZonePoint> points = new();
    }

    public event Action<HeatmapChartPayload> OnHeatmapChartUpdated;
    public event Action<string> OnHeatmapZoneClicked;

    [SerializeField] private bool debugLogHeatmap = true;

    [Header("Visual Settings")]
    [SerializeField] private Color baseColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField, Range(0f, 1f)] private float alphaMin = 0.0f;
    [SerializeField, Range(0f, 1f)] private float alphaMax = 1.0f;

    [Header("Refs")]
    [SerializeField] private GameObject locationPrefab;
    [SerializeField] private HeatmapHUDController heatmapHudPrefab;

    [Header("Chart UI (Optional)")]
    [SerializeField] private GameObject heatmapChartUiPrefab;

    [Header("Synthetic Data (Runtime tests)")]
    [SerializeField] private HeatmapSyntheticDataProvider syntheticDataProvider;
    [SerializeField] private bool enableSyntheticHotkeys = true;
    [SerializeField] private bool enableSyntheticRuntime = false;

    [Header("Synthetic Campaign (One-shot)")]
    [SerializeField] private bool includeBenchmarkInCampaign = true;
    [SerializeField] private int[] campaignDateWindowsDays = new[] { 7, 30, 90 };
    [SerializeField] private string campaignReportFileName = "heatmap-campaign-last.txt";

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
    private GameObject chartUiInstance;
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

    private HeatmapChartPayload lastChartPayload;
    private bool syntheticCampaignRunning = false;
    private string lastSyntheticCampaignReport = string.Empty;

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
            HandleZoneClick();

            if (enableSyntheticRuntime && enableSyntheticHotkeys && syntheticDataProvider != null)
            {
                if (Input.GetKeyUp(KeyCode.F5))
                    StartCoroutine(RegenerateSyntheticAndRecompute());

                if (Input.GetKeyUp(KeyCode.F6))
                    StartCoroutine(CycleSyntheticScenarioAndRecompute());

                if (Input.GetKeyUp(KeyCode.F7))
                    StartCoroutine(RunSyntheticScalabilityBenchmark());

                if (Input.GetKeyUp(KeyCode.F8))
                    StartCoroutine(RunFullSyntheticCampaign());

                if (Input.GetKeyUp(KeyCode.F9))
                    PrintLastSyntheticCampaignReport();
            }
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

        if (uiManager != null)
        {
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

        var canvas = ResolveHeatmapCanvas();
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

        if (heatmapChartUiPrefab != null)
        {
            CleanupStaleHeatmapChartUi(canvas.transform);

            var chartRoot = Instantiate(heatmapChartUiPrefab);
            chartUiInstance = AttachChartUiToHud(chartRoot, hudInstance.transform, canvas.transform);

            if (chartUiInstance != null)
            {
                CleanupChartPreviews(chartUiInstance);
                CleanupBakedChartChildren(chartUiInstance);
                chartUiInstance.transform.SetAsLastSibling();
                NormalizeChartUiLayout(chartUiInstance);
            }
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

        if (enableSyntheticRuntime && syntheticDataProvider != null && syntheticDataProvider.ForceSyntheticData)
        {
            bool shouldRegenerate = syntheticDataProvider.RegenerateEachRequest || !syntheticDataProvider.HasInjectedSynthetic;
            if (shouldRegenerate)
            {
                yield return syntheticDataProvider.InjectIntoApi(api, api.locations);
                mappingsPrepared = false;
            }
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
            EmitHeatmapChartPayload(new Dictionary<string, double>(), 0.0, 0.0, from, to, metric);
            yield break;
        }

        if (api.tasks == null || api.tasks.Count == 0)
        {
            var tTasks0 = api.GetTasksAsync();
            while (!tTasks0.IsCompleted) yield return null;
            mappingsPrepared = false;
        }

        if (enableSyntheticRuntime && syntheticDataProvider != null)
        {
            bool backendMissing = (api.activities == null || api.activities.Count == 0 || api.tasks == null || api.tasks.Count == 0);
            if (syntheticDataProvider.ShouldInject(api, backendMissing))
            {
                yield return syntheticDataProvider.InjectIntoApi(api, api.locations);
                mappingsPrepared = false;
            }
        }

        var allTasks = api.tasks;
        if (allTasks == null || allTasks.Count == 0)
        {
            Debug.LogWarning("[Heatmap] Lista de tasks veio vazia.");
            HideAllZones();
            EmitHeatmapChartPayload(new Dictionary<string, double>(), 0.0, 0.0, from, to, metric);
            yield break;
        }

        PrepareStaticMappings(allTasks);

        var filtroInicio = from.Date;
        var filtroFim = to.Date.AddDays(1);

        if (debugLogHeatmap)
            Debug.Log($"[Heatmap][DEBUG] Filtro datas => {filtroInicio:yyyy-MM-dd HH:mm} .. {filtroFim:yyyy-MM-dd HH:mm}");

        var valuesByZone = new Dictionary<string, double>();

        foreach (var carHist in histories)
        {
            if (carHist?.History == null) continue;

            foreach (var entry in carHist.History)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ActivityId))
                {
                    continue;
                }

                if (!taskById.TryGetValue(entry.ActivityId, out var task) || task == null)
                {
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
                    continue;
                }

                foreach (var locId in locIdsToCount)
                {
                    var atual = valuesByZone.GetValueOrDefault(locId);
                    valuesByZone[locId] = atual + valor;
                }
            }
        }

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

            EmitHeatmapChartPayload(valuesByZone, totalVal, maxVal, from, to, metric);

            yield break;
        }

        // guardar dados globais para tooltip
        lastValuesByZone = valuesByZone;
        lastTotalVal = totalVal;
        lastMetric = metric;

        EmitHeatmapChartPayload(valuesByZone, totalVal, maxVal, from, to, metric);

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

    private IEnumerator RegenerateSyntheticAndRecompute()
    {
        if (syntheticDataProvider == null)
            yield break;

        if (api == null) api = FindObjectOfType<APIscript>(true);
        if (api == null)
            yield break;

        if (api.locations == null || api.locations.Count == 0)
        {
            var tLoc = api.GetVirtualMapLocationsAsync();
            while (!tLoc.IsCompleted) yield return null;
            mappingsPrepared = false;
        }

        yield return syntheticDataProvider.InjectIntoApi(api, api.locations);
        mappingsPrepared = false;

        if (currentFrom.HasValue && currentTo.HasValue)
            yield return ComputeAndApplyHeatmap(currentFrom.Value, currentTo.Value, currentMetric);
    }

    private IEnumerator CycleSyntheticScenarioAndRecompute()
    {
        if (syntheticDataProvider == null)
            yield break;

        var next = syntheticDataProvider.CycleScenario();
        Debug.Log($"[HeatmapSynthetic] Cenário ativo: {next}");

        yield return RegenerateSyntheticAndRecompute();
    }

    private IEnumerator RunSyntheticScalabilityBenchmark()
    {
        yield return RunSyntheticScalabilityBenchmarkInternal(logToConsole: true, outputLines: null);
    }

    private IEnumerator RunSyntheticScalabilityBenchmarkInternal(bool logToConsole, List<string> outputLines)
    {
        if (syntheticDataProvider == null)
            yield break;

        if (api == null) api = FindObjectOfType<APIscript>(true);
        if (api == null)
            yield break;

        if (api.locations == null || api.locations.Count == 0)
        {
            var tLoc = api.GetVirtualMapLocationsAsync();
            while (!tLoc.IsCompleted) yield return null;
            mappingsPrepared = false;
        }

        if (!currentFrom.HasValue || !currentTo.HasValue)
            yield break;

        var sizes = syntheticDataProvider.BenchmarkEventCounts;
        if (sizes == null || sizes.Count == 0)
            yield break;

        if (logToConsole)
            Debug.Log("[HeatmapSynthetic][Benchmark] Início benchmark de escalabilidade.");

        foreach (var size in sizes)
        {
            float t0 = Time.realtimeSinceStartup;

            yield return syntheticDataProvider.InjectIntoApi(api, api.locations, overrideTotalEvents: size);
            mappingsPrepared = false;

            float tIngestion = (Time.realtimeSinceStartup - t0) * 1000f;

            float t1 = Time.realtimeSinceStartup;
            yield return ComputeAndApplyHeatmap(currentFrom.Value, currentTo.Value, currentMetric);
            float tCompute = (Time.realtimeSinceStartup - t1) * 1000f;

            float t2 = Time.realtimeSinceStartup;
            yield return new WaitForEndOfFrame();
            float tRender = (Time.realtimeSinceStartup - t2) * 1000f;

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "[HeatmapSynthetic][Benchmark] events={0} | ingestionMs={1:0.##} | computeMs={2:0.##} | renderFrameMs={3:0.##}",
                size,
                tIngestion,
                tCompute,
                tRender);

            if (logToConsole)
                Debug.Log(line);

            outputLines?.Add(line);
            yield return null;
        }

        if (logToConsole)
            Debug.Log("[HeatmapSynthetic][Benchmark] Fim benchmark.");
    }

    private IEnumerator RunFullSyntheticCampaign()
    {
        if (syntheticCampaignRunning)
        {
            Debug.LogWarning("[HeatmapSynthetic][Campaign] Já existe uma campanha em execução.");
            yield break;
        }

        if (syntheticDataProvider == null)
        {
            Debug.LogWarning("[HeatmapSynthetic][Campaign] syntheticDataProvider não configurado.");
            yield break;
        }

        syntheticCampaignRunning = true;
        Debug.Log("[HeatmapSynthetic][Campaign] Início campanha completa (F8).");

        var originalMetric = currentMetric;
        var originalFrom = currentFrom;
        var originalTo = currentTo;

        var report = new StringBuilder(4096);
        report.AppendLine("# Heatmap Synthetic Campaign Report");
        report.AppendLine($"GeneratedAtUtc: {DateTime.UtcNow:O}");
        report.AppendLine($"WindowsDays: {string.Join(",", campaignDateWindowsDays ?? Array.Empty<int>())}");
        report.AppendLine($"IncludeBenchmark: {includeBenchmarkInCampaign}");
        report.AppendLine();

        bool shouldRestoreHeatmap = false;

        try
        {
            if (api == null) api = FindObjectOfType<APIscript>(true);
            if (api == null)
            {
                report.AppendLine("ERROR|APIscript not found");
                yield break;
            }

            if (api.locations == null || api.locations.Count == 0)
            {
                var tLoc = api.GetVirtualMapLocationsAsync();
                while (!tLoc.IsCompleted) yield return null;
                mappingsPrepared = false;
            }

            if (api.locations == null || api.locations.Count == 0)
            {
                report.AppendLine("ERROR|No locations available for campaign");
                yield break;
            }

            var scenarios = new[]
            {
                HeatmapSyntheticScenario.Uniform,
                HeatmapSyntheticScenario.Skewed,
                HeatmapSyntheticScenario.MetricDifferentiation
            };

            var metrics = new[] { HeatmapMetric.Frequency, HeatmapMetric.Occupancy };
            var windows = (campaignDateWindowsDays == null || campaignDateWindowsDays.Length == 0)
                ? new[] { 7, 30, 90 }
                : campaignDateWindowsDays;

            foreach (var scenario in scenarios)
            {
                syntheticDataProvider.SetScenario(scenario);

                float tInject0 = Time.realtimeSinceStartup;
                yield return syntheticDataProvider.InjectIntoApi(api, api.locations, overrideTotalEvents: null, overrideScenario: scenario);
                float injectMs = (Time.realtimeSinceStartup - tInject0) * 1000f;
                mappingsPrepared = false;

                report.AppendLine($"SCENARIO|name={scenario}|injectMs={injectMs.ToString("0.##", CultureInfo.InvariantCulture)}");

                if (!string.IsNullOrEmpty(syntheticDataProvider.LastGroundTruthSummaryLine))
                    report.AppendLine(syntheticDataProvider.LastGroundTruthSummaryLine);
                if (!string.IsNullOrEmpty(syntheticDataProvider.LastGroundTruthTopFrequencyLine))
                    report.AppendLine(syntheticDataProvider.LastGroundTruthTopFrequencyLine);
                if (!string.IsNullOrEmpty(syntheticDataProvider.LastGroundTruthTopOccupancyLine))
                    report.AppendLine(syntheticDataProvider.LastGroundTruthTopOccupancyLine);

                foreach (var days in windows)
                {
                    int safeDays = Mathf.Max(1, days);
                    var to = DateTime.UtcNow;
                    var from = to.AddDays(-safeDays);

                    currentFrom = from;
                    currentTo = to;

                    foreach (var metric in metrics)
                    {
                        currentMetric = metric;

                        float t0 = Time.realtimeSinceStartup;
                        yield return ComputeAndApplyHeatmap(from, to, metric);
                        float computeMs = (Time.realtimeSinceStartup - t0) * 1000f;

                        if (TryGetLastChartPayload(out var payload) && payload != null)
                        {
                            string top3 = BuildTop3FromPayload(payload);
                            report.AppendLine(string.Format(
                                CultureInfo.InvariantCulture,
                                "RESULT|scenario={0}|windowDays={1}|metric={2}|from={3:yyyy-MM-dd}|to={4:yyyy-MM-dd}|total={5:0.##}|max={6:0.##}|computeMs={7:0.##}|top3={8}",
                                scenario,
                                safeDays,
                                metric,
                                from,
                                to,
                                payload.total,
                                payload.max,
                                computeMs,
                                top3));
                        }
                        else
                        {
                            report.AppendLine($"RESULT|scenario={scenario}|windowDays={safeDays}|metric={metric}|status=NO_PAYLOAD");
                        }

                        yield return null;
                    }
                }

                if (includeBenchmarkInCampaign)
                {
                    currentFrom = DateTime.UtcNow.AddDays(-30);
                    currentTo = DateTime.UtcNow;
                    currentMetric = HeatmapMetric.Frequency;

                    var benchLines = new List<string>();
                    yield return RunSyntheticScalabilityBenchmarkInternal(logToConsole: false, outputLines: benchLines);
                    for (int i = 0; i < benchLines.Count; i++)
                        report.AppendLine($"BENCH|scenario={scenario}|{benchLines[i]}");
                }

                report.AppendLine();
            }
        }
        finally
        {
            currentMetric = originalMetric;
            currentFrom = originalFrom;
            currentTo = originalTo;

            shouldRestoreHeatmap = originalFrom.HasValue && originalTo.HasValue;

            syntheticCampaignRunning = false;
        }

        if (shouldRestoreHeatmap)
            yield return ComputeAndApplyHeatmap(originalFrom.Value, originalTo.Value, originalMetric);

        lastSyntheticCampaignReport = report.ToString();
        SaveSyntheticCampaignReport(lastSyntheticCampaignReport);

        Debug.Log("[HeatmapSynthetic][Campaign][FINAL_REPORT_BEGIN]\n" + lastSyntheticCampaignReport + "\n[HeatmapSynthetic][Campaign][FINAL_REPORT_END]");
        Debug.Log("[HeatmapSynthetic][Campaign] Fim campanha completa.");
    }

    private static string BuildTop3FromPayload(HeatmapChartPayload payload)
    {
        if (payload == null || payload.points == null || payload.points.Count == 0 || payload.total <= 0f)
            return "n/a";

        var ordered = new List<HeatmapZonePoint>(payload.points);
        ordered.Sort((a, b) => b.value.CompareTo(a.value));

        int take = Mathf.Min(3, ordered.Count);
        var parts = new List<string>(take);

        for (int i = 0; i < take; i++)
        {
            var p = ordered[i];
            string zone = string.IsNullOrWhiteSpace(p.zoneName) ? p.locationId : p.zoneName;
            parts.Add($"{zone}={p.percent * 100f:0.00}%");
        }

        return string.Join(" | ", parts);
    }

    private void SaveSyntheticCampaignReport(string reportText)
    {
        try
        {
            string fileName = string.IsNullOrWhiteSpace(campaignReportFileName)
                ? "heatmap-campaign-last.txt"
                : campaignReportFileName.Trim();

            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string docsDir = Path.Combine(repoRoot, "docs");
            Directory.CreateDirectory(docsDir);

            string fullPath = Path.Combine(docsDir, fileName);
            File.WriteAllText(fullPath, reportText ?? string.Empty, Encoding.UTF8);

            Debug.Log($"[HeatmapSynthetic][Campaign] Relatório guardado em: {fullPath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[HeatmapSynthetic][Campaign] Falha ao guardar relatório: " + ex.Message);
        }
    }

    private void PrintLastSyntheticCampaignReport()
    {
        if (string.IsNullOrWhiteSpace(lastSyntheticCampaignReport))
        {
            Debug.Log("[HeatmapSynthetic][Campaign] Ainda não existe relatório em memória. Corre F8 primeiro.");
            return;
        }

        Debug.Log("[HeatmapSynthetic][Campaign][LAST_REPORT_BEGIN]\n" + lastSyntheticCampaignReport + "\n[HeatmapSynthetic][Campaign][LAST_REPORT_END]");
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

    public bool TryGetLastChartPayload(out HeatmapChartPayload payload)
    {
        payload = lastChartPayload;
        return payload != null;
    }

    private void EmitHeatmapChartPayload(
        Dictionary<string, double> valuesByZone,
        double totalVal,
        double maxVal,
        DateTime from,
        DateTime to,
        HeatmapMetric metric)
    {
        var payload = new HeatmapChartPayload
        {
            from = from,
            to = to,
            metric = metric,
            scale = currentScale,
            curve = currentCurve,
            total = (float)Math.Max(0.0, totalVal),
            max = (float)Math.Max(0.0, maxVal),
            points = new List<HeatmapZonePoint>()
        };

        if (api != null && api.locations != null)
        {
            foreach (var loc in api.locations)
            {
                if (loc == null || string.IsNullOrEmpty(loc.id))
                    continue;

                valuesByZone.TryGetValue(loc.id, out var valueRaw);

                string zoneName = !string.IsNullOrEmpty(loc.name) ? loc.name : loc.id;
                float value = (float)Math.Max(0.0, valueRaw);
                float percent = (totalVal > 0.0)
                    ? Mathf.Clamp01((float)(valueRaw / totalVal))
                    : 0f;

                payload.points.Add(new HeatmapZonePoint
                {
                    locationId = loc.id,
                    zoneName = zoneName,
                    value = value,
                    percent = percent
                });
            }
        }

        lastChartPayload = payload;
        OnHeatmapChartUpdated?.Invoke(payload);
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
        lastChartPayload = null;

        OnHeatmapChartUpdated?.Invoke(new HeatmapChartPayload
        {
            from = currentFrom ?? DateTime.Today,
            to = currentTo ?? DateTime.Today,
            metric = currentMetric,
            scale = currentScale,
            curve = currentCurve,
            total = 0f,
            max = 0f,
            points = new List<HeatmapZonePoint>()
        });

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

            if (chartUiInstance != null)
            {
                Destroy(chartUiInstance);
                chartUiInstance = null;
            }

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

    private GameObject AttachChartUiToHud(GameObject chartRoot, Transform hudParent, Transform canvasParent)
    {
        if (chartRoot == null || hudParent == null)
            return null;

        var targetParent = canvasParent != null ? canvasParent : hudParent;

        var panel = FindDeepChild(chartRoot.transform, "HeatmapChartPanel");
        if (panel != null)
        {
            panel.SetParent(targetParent, false);
            panel.localScale = Vector3.one;

            Destroy(chartRoot);
            return panel.gameObject;
        }

        var rootRect = chartRoot.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.SetParent(targetParent, false);
            return chartRoot;
        }

        var innerCanvas = chartRoot.GetComponentInChildren<Canvas>(true);
        if (innerCanvas != null)
        {
            var canvasGo = innerCanvas.gameObject;
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.SetParent(targetParent, false);
                Destroy(chartRoot);
                return canvasGo;
            }
        }

        chartRoot.transform.SetParent(targetParent, false);
        return chartRoot;
    }

    private void NormalizeChartUiLayout(GameObject chartRoot)
    {
        if (chartRoot == null)
            return;

        chartRoot.transform.localScale = Vector3.one;

        var rootRect = chartRoot.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.localScale = Vector3.one;
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.sizeDelta = new Vector2(Mathf.Clamp(rootRect.sizeDelta.x <= 1f ? 700f : rootRect.sizeDelta.x, 600f, 900f), Mathf.Clamp(rootRect.sizeDelta.y <= 1f ? 380f : rootRect.sizeDelta.y, 240f, 720f));

            var hudRect = hudInstance != null ? hudInstance.GetComponent<RectTransform>() : null;
            if (hudRect != null)
            {
                rootRect.anchoredPosition = new Vector2(hudRect.anchoredPosition.x, hudRect.anchoredPosition.y - hudRect.sizeDelta.y - 8f);
            }
            else
            {
                rootRect.anchoredPosition = new Vector2(16f, -312f);
            }
        }

        var allRects = chartRoot.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < allRects.Length; i++)
        {
            if (allRects[i] != null)
                allRects[i].localScale = Vector3.one;
        }

        if (rootRect != null)
            rootRect.sizeDelta = new Vector2(Mathf.Clamp(rootRect.sizeDelta.x, 600f, 900f), Mathf.Clamp(rootRect.sizeDelta.y, 240f, 720f));
    }

    private void CleanupStaleHeatmapChartUi(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return;

        var toDestroy = new List<GameObject>();
        var transforms = canvasRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            var tr = transforms[i];
            if (tr == null) continue;

            var go = tr.gameObject;
            var n = go.name;

            if (n == "HeatmapChartPanel" || n == "HeatmapChart" || n.StartsWith("HeatmapChart(") || n.Contains("HeatmapChart(Preview)"))
            {
                toDestroy.Add(go);
            }
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
            if (toDestroy[i] != null)
                Destroy(toDestroy[i]);
        }

        chartUiInstance = null;
    }

    private void CleanupChartPreviews(GameObject chartRoot)
    {
        if (chartRoot == null)
            return;

        var previewHandlers = chartRoot.GetComponentsInChildren<E2ChartPreviewHandler>(true);
        for (int i = 0; i < previewHandlers.Length; i++)
        {
            var preview = previewHandlers[i];
            if (preview != null)
                Destroy(preview.gameObject);
        }

        var charts = chartRoot.GetComponentsInChildren<E2Chart>(true);
        for (int i = 0; i < charts.Length; i++)
        {
            var chart = charts[i];
            if (chart == null) continue;
            chart.ClearPreview();
        }

        var transforms = chartRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var tr = transforms[i];
            if (tr == null) continue;
            if (tr.gameObject.name.Contains("(Preview)"))
                Destroy(tr.gameObject);
        }
    }

    private void CleanupBakedChartChildren(GameObject chartRoot)
    {
        if (chartRoot == null)
            return;

        var chartTransform = FindDeepChild(chartRoot.transform, "HeatmapChart");
        if (chartTransform == null)
            return;

        var toDelete = new List<GameObject>();
        for (int i = 0; i < chartTransform.childCount; i++)
        {
            var child = chartTransform.GetChild(i);
            if (child == null) continue;

            var n = child.name;
            if (n == "Content" || n == "Title" || n == "Legends" || n == "Tooltip" || n.Contains("(Preview)"))
                toDelete.Add(child.gameObject);
        }

        for (int i = 0; i < toDelete.Count; i++)
        {
            if (toDelete[i] != null)
                Destroy(toDelete[i]);
        }
    }

    private Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == childName)
                return child;

            var nested = FindDeepChild(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void ChangeCameraToTop()
    {
        var cameraSystemObj = GameObject.Find("CameraSystem");
        if (cameraSystemObj == null) return;

        var cameraSystem = cameraSystemObj.GetComponent<CameraSystem>();
        if (cameraSystem != null)
            cameraSystem.SwitchToTopCam();
    }

    private Canvas ResolveHeatmapCanvas()
    {
        var launcher = FindObjectOfType<HeatmapButtonLauncher>(true);
        if (launcher != null)
        {
            var launcherCanvas = launcher.GetComponentInParent<Canvas>();
            if (launcherCanvas != null)
                return launcherCanvas;
        }

        var taggedUi = GameObject.FindGameObjectWithTag("TAG");
        if (taggedUi != null)
        {
            var taggedCanvas = taggedUi.GetComponent<Canvas>();
            if (taggedCanvas != null)
                return taggedCanvas;
        }

        if (uiManager != null)
        {
            var uiCanvas = uiManager.GetComponentInParent<Canvas>();
            if (uiCanvas != null)
                return uiCanvas;
        }

        return FindObjectOfType<Canvas>(true);
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

            body = $"Entries: {zoneCount} / Total: {totalCount} ({percent:0.#}%)";
        }
        else
        {
            TimeSpan ts = TimeSpan.FromSeconds(v);
            string timeStr = $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s";
            body = $"Time Spent: {timeStr} ({percent:0.#}%)";
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

    private void HandleZoneClick()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        var tag = hit.collider.GetComponentInParent<HeatmapZoneTag>();
        if (tag == null || string.IsNullOrEmpty(tag.locationId))
            return;

        OnHeatmapZoneClicked?.Invoke(tag.locationId);
    }
}

// helper C# 8
static class DictExt
{
    public static double GetValueOrDefault(this Dictionary<string, double> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : 0d;
}
