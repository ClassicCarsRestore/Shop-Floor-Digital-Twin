using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using SimpleJSON;


public class EnergyPanelController : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string apiBaseUrl = "https://sensors.raimundobranco.com";
    [SerializeField] private int requestTimeoutSeconds = 10;
    [SerializeField] private float refreshSeconds = 5f;
    [SerializeField] private bool autoRefresh = true;

    [Header("Panels - Totals")]
    [SerializeField] private MetricPanelUI currentEnergyPanel; // total current power (W)
    [SerializeField] private MetricPanelUI monthEnergyPanel;   // total month energy (kWh)

    [Header("Panels - Current (W)")]
    [SerializeField] private MetricPanelUI currentRightBoothPanel;
    [SerializeField] private MetricPanelUI currentLeftBoothPanel;
    [SerializeField] private MetricPanelUI currentSandBlastPanel;

    [Header("Panels - Month (kWh)")]
    [SerializeField] private MetricPanelUI monthRightBoothPanel;
    [SerializeField] private MetricPanelUI monthLeftBoothPanel;
    [SerializeField] private MetricPanelUI monthSandBlastPanel;

    [Serializable]
    public class EnergyDaySeriesResponse
    {
        public string period;  // "YYYY-MM"
        public string unit;    // "kWh"
        public Dictionary<string, MeterSeries> meters; // chave = id do meter
    }

    [Serializable]
    public class MeterSeries
    {
        public string label;
        public List<DayPoint> points;
    }

    [Serializable]
    public class DayPoint
    {
        public string date; // "YYYY-MM-DD"
        public float value; // kWh
    }




    // ids (têm de bater com a API)
    private const string ID_RIGHT = "shelly3EMPinturaDireita";
    private const string ID_LEFT = "shelly3EMPinturaEsquerda";
    private const string ID_SAND = "shelly3EMJatoAreia";

    private Coroutine refreshCo;

    [Serializable]
    public class MetricPanelUI
    {
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Value;

        public void Set(string title, string value)
        {
            if (Title) Title.text = title;
            if (Value) Value.text = value;
        }
    }

    // ---------- API DTOs (Overview) ----------
    [Serializable]
    public class OverviewResponse
    {
        public string generated_at;
        public TotalBlock total;
        public MeterBlock[] meters;
    }

    [Serializable]
    public class TotalBlock
    {
        public float current_power_w;
        public float month_energy_kwh;
    }

    [Serializable]
    public class MeterBlock
    {
        public string id;
        public string label;
        public float current_power_w;
        public string current_power_time;
        public float month_energy_kwh;
        public bool is_running;
    }

    // ---------- API DTOs (Breakdown Month) ----------
    // Nota: JsonUtility suporta List<T> em classes [Serializable]
    [Serializable]
    public class BreakdownMonthResponse
    {
        public string period;
        public string unit;
        public List<string> labels;
        public List<float> values;
        public float total_kwh;
    }

    [Serializable]
    public class EnergyTrendMonthsResponse
    {
        public string unit;           // "kWh"
        public int months;            // 5
        public string[] labels;       // ["Dez 2025", ...]
        public float[] values;        // [..]
    }


  

    [Serializable]
    public class EnergyTrendPayload
    {
        public string title;
        public string subtitle;
        public System.Collections.Generic.List<string> categories;
        public System.Collections.Generic.List<float> values;
        public string unit;
    }


    private void OnEnable()
    {
        if (autoRefresh)
            refreshCo = StartCoroutine(RefreshLoop());
        else
            RefreshOnce();
    }

    private void OnDisable()
    {
        if (refreshCo != null)
        {
            StopCoroutine(refreshCo);
            refreshCo = null;
        }
    }

    [ContextMenu("Refresh Once")]
    public void RefreshOnce()
    {
        StartCoroutine(FetchAndApplyOverview());
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return FetchAndApplyOverview();
            yield return new WaitForSeconds(refreshSeconds);
        }
    }

    // ----------- PUBLIC API for binders -----------

    public void RequestBreakdownMonth(Action<BreakdownMonthResponse> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(FetchBreakdownMonth(onSuccess, onError));
    }
    public void RequestMonthlyTrendLastMonths(
        int months,
        Action<EnergyTrendPayload> onOk,
        Action<string> onErr
    )
    {
        StartCoroutine(_RequestMonthlyTrendLastMonthsCo(months, onOk, onErr));
    }

    // ----------- Internal HTTP calls -----------

    private IEnumerator FetchAndApplyOverview()
    {
        string url = BuildUrl("/energy/dashboard/overview");

        yield return GetJson<OverviewResponse>(
            url,
            data =>
            {
                if (data == null) { ApplyErrorState(); return; }
                ApplyToUI(data);
            },
            err =>
            {
                Debug.LogWarning($"[EnergyPanelController] Overview error: {err} ({url})");
                ApplyErrorState();
            }
        );
    }

    private IEnumerator FetchBreakdownMonth(Action<BreakdownMonthResponse> onSuccess, Action<string> onError)
    {
        string url = BuildUrl("/energy/dashboard/breakdown/month");

        yield return GetJson<BreakdownMonthResponse>(
            url,
            data =>
            {
                if (data == null)
                {
                    onError?.Invoke("Empty response");
                    return;
                }
                onSuccess?.Invoke(data);
            },
            err =>
            {
                Debug.LogWarning($"[EnergyPanelController] BreakdownMonth error: {err} ({url})");
                onError?.Invoke(err);
            }
        );
    }

    private IEnumerator GetJson<T>(string url, Action<T> onSuccess, Action<string> onError) where T : class
    {
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"{req.responseCode} {req.error}");
                yield break;
            }

            string json = req.downloadHandler.text;
            T data = null;

            try
            {
                data = JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                onError?.Invoke($"JSON parse failed: {e.Message}");
                yield break;
            }

            onSuccess?.Invoke(data);
        }
    }

    private IEnumerator _RequestMonthlyTrendLastMonthsCo(
        int months,
        Action<EnergyTrendPayload> onOk,
        Action<string> onErr
    )
    {
        string url = apiBaseUrl.TrimEnd('/') + "/energy/trend/months?months=" + months;

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke($"API error: {req.responseCode} {req.error} ({url})");
                yield break;
            }

            var json = req.downloadHandler.text;
            EnergyTrendMonthsResponse data = null;

            try { data = JsonUtility.FromJson<EnergyTrendMonthsResponse>(json); }
            catch (Exception e)
            {
                onErr?.Invoke($"JSON parse failed: {e.Message}\n{json}");
                yield break;
            }

            if (data == null || data.labels == null || data.values == null)
            {
                onErr?.Invoke("Invalid payload from API.");
                yield break;
            }

            int n = Mathf.Min(data.labels.Length, data.values.Length);
            if (n <= 0)
            {
                onErr?.Invoke("No data.");
                yield break;
            }

            var payload = new EnergyTrendPayload
            {
                title = "Energy Consumption Monthly Trend",
                subtitle = "", // opcional: podes pôr "Últimos 5 meses"
                unit = data.unit ?? "kWh",
                categories = new System.Collections.Generic.List<string>(n),
                values = new System.Collections.Generic.List<float>(n),
            };

            for (int i = 0; i < n; i++)
            {
                payload.categories.Add(data.labels[i]);
                payload.values.Add(Mathf.Max(0f, data.values[i]));
            }

            onOk?.Invoke(payload);
        }
    }
   
    

    

    // helper raw json no controller
    private IEnumerator GetJsonRaw(string url, Action<string> onSuccess, Action<string> onError)
    {
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"{req.responseCode} {req.error}");
                yield break;
            }

            onSuccess?.Invoke(req.downloadHandler.text);
        }
    }


    private string BuildUrl(string path)
    {
        return apiBaseUrl.TrimEnd('/') + path;
    }

    // ----------- UI apply -----------

    private void ApplyToUI(OverviewResponse data)
    {
        if (currentEnergyPanel != null)
            currentEnergyPanel.Set("Current Energy Consumption", FormatW(data.total.current_power_w));

        if (monthEnergyPanel != null)
            monthEnergyPanel.Set("This Month Energy Consumption", FormatKwh(data.total.month_energy_kwh));

        Dictionary<string, MeterBlock> byId = new Dictionary<string, MeterBlock>();
        if (data.meters != null)
        {
            foreach (var m in data.meters)
            {
                if (m != null && !string.IsNullOrEmpty(m.id))
                    byId[m.id] = m;
            }
        }

        if (currentRightBoothPanel != null)
            currentRightBoothPanel.Set("Current Right Paint Booth Usage", FormatW(GetCurrentW(byId, ID_RIGHT)));

        if (currentLeftBoothPanel != null)
            currentLeftBoothPanel.Set("Current Left Paint Booth Usage", FormatW(GetCurrentW(byId, ID_LEFT)));

        if (currentSandBlastPanel != null)
            currentSandBlastPanel.Set("Current Sand Blasting Usage", FormatW(GetCurrentW(byId, ID_SAND)));

        if (monthRightBoothPanel != null)
            monthRightBoothPanel.Set("This Month Right Paint Booth Usage", FormatKwh(GetMonthKwh(byId, ID_RIGHT)));

        if (monthLeftBoothPanel != null)
            monthLeftBoothPanel.Set("This Month Left Paint Booth Usage", FormatKwh(GetMonthKwh(byId, ID_LEFT)));

        if (monthSandBlastPanel != null)
            monthSandBlastPanel.Set("This Month Sand Blasting Usage", FormatKwh(GetMonthKwh(byId, ID_SAND)));
    }

    private float GetCurrentW(Dictionary<string, MeterBlock> byId, string id)
    {
        if (byId != null && byId.TryGetValue(id, out var m))
            return Mathf.Max(0f, m.current_power_w);
        return 0f;
    }

    private float GetMonthKwh(Dictionary<string, MeterBlock> byId, string id)
    {
        if (byId != null && byId.TryGetValue(id, out var m))
            return Mathf.Max(0f, m.month_energy_kwh);
        return 0f;
    }

    private void ApplyErrorState()
    {
        string na = "--";

        if (currentEnergyPanel != null) currentEnergyPanel.Set("Current Energy Consumption", na);
        if (monthEnergyPanel != null) monthEnergyPanel.Set("This Month Energy Consumption", na);

        if (currentRightBoothPanel != null) currentRightBoothPanel.Set("Current Right Paint Booth Usage", na);
        if (currentLeftBoothPanel != null) currentLeftBoothPanel.Set("Current Left Paint Booth Usage", na);
        if (currentSandBlastPanel != null) currentSandBlastPanel.Set("Current Sand Blasting Usage", na);

        if (monthRightBoothPanel != null) monthRightBoothPanel.Set("This Month Right Paint Booth Usage", na);
        if (monthLeftBoothPanel != null) monthLeftBoothPanel.Set("This Month Left Paint Booth Usage", na);
        if (monthSandBlastPanel != null) monthSandBlastPanel.Set("This Month Sand Blasting Usage", na);
    }

    private string FormatW(float w) => $"{w:0.##} W";
    private string FormatKwh(float k) => $"{k:0.###} kWh";



}



