using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;


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

    [Header("Frontend Cache")]
    [SerializeField] private bool enableFrontendCache = true;
    [SerializeField] private float cacheBreakdownMonthSeconds = 30f;
    [SerializeField] private float cacheTrendMonthsSeconds = 120f;
    [SerializeField] private float cacheTodayHourlySeconds = 10f;
    [SerializeField] private float cacheWeekDailySeconds = 30f;
    [SerializeField] private float cacheMonthWeeklySeconds = 60f;
    [SerializeField] private float cachePowerTimeseriesSeconds = 10f;
    [SerializeField] private float cacheEnergyDaySeriesSeconds = 120f;

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




    // ids (t�m de bater com a API)
    private const string ID_RIGHT = "shelly3EMPinturaDireita";
    private const string ID_LEFT = "shelly3EMPinturaEsquerda";
    private const string ID_SAND = "shelly3EMJatoAreia";

    private Coroutine refreshCo;

    public event Action<OverviewResponse> OverviewUpdated;

    private sealed class FrontendCacheEntry
    {
        public object Data;
        public float ExpireAt;
    }

    private readonly Dictionary<string, FrontendCacheEntry> frontendCache = new Dictionary<string, FrontendCacheEntry>();

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

    [Serializable]
    public class EnergyUsageSeries
    {
        public string name;
        public List<float> values;
    }

    [Serializable]
    public class EnergyUsageChartPayload
    {
        public string title;
        public string subtitle;
        public string unit;
        public List<string> categories;
        public List<string> timestamps;
        public List<EnergyUsageSeries> series;
    }

    [Serializable]
    public class PowerTimeseriesPoint
    {
        public string time;
        public float value;
    }

    [Serializable]
    public class PowerTimeseriesMeter
    {
        public string label;
        public PowerTimeseriesPoint[] points;
    }

    [Serializable]
    public class PowerTimeseriesMeters
    {
        public PowerTimeseriesMeter shelly3EMJatoAreia;
        public PowerTimeseriesMeter shelly3EMPinturaDireita;
        public PowerTimeseriesMeter shelly3EMPinturaEsquerda;
    }

    [Serializable]
    public class PowerTimeseriesResponse
    {
        public string generated_at;
        public string window;
        public string every;
        public string unit;
        public PowerTimeseriesMeters meters;
    }

    [Serializable]
    public class EnergyDayPoint
    {
        public string date;
        public float value;
    }

    [Serializable]
    public class EnergyDayMeter
    {
        public string label;
        public EnergyDayPoint[] points;
    }

    [Serializable]
    public class EnergyDayMeters
    {
        public EnergyDayMeter shelly3EMJatoAreia;
        public EnergyDayMeter shelly3EMPinturaDireita;
        public EnergyDayMeter shelly3EMPinturaEsquerda;
    }

    [Serializable]
    public class EnergyDayResponse
    {
        public string period;
        public string unit;
        public EnergyDayMeters meters;
    }

    [Serializable]
    public class TodayHourlyPoint
    {
        public int hour;
        public float kwh;
    }

    [Serializable]
    public class TodayHourlySeries
    {
        public string meter_id;
        public string label;
        public float total_kwh;
        public TodayHourlyPoint[] hourly;
    }

    [Serializable]
    public class TodayHourlyResponse
    {
        public string date;
        public string unit;
        public TodayHourlySeries[] series;
    }

    [Serializable]
    public class WeekDailyPoint
    {
        public string date;
        public float kwh;
    }

    [Serializable]
    public class WeekDailySeries
    {
        public string meter_id;
        public string label;
        public float total_kwh;
        public WeekDailyPoint[] daily;
    }

    [Serializable]
    public class WeekDailyResponse
    {
        public string period;
        public string unit;
        public WeekDailySeries[] series;
    }

    [Serializable]
    public class MonthWeeklyPoint
    {
        public string week_start;
        public float kwh;
    }

    [Serializable]
    public class MonthWeeklySeries
    {
        public string meter_id;
        public string label;
        public float total_kwh;
        public MonthWeeklyPoint[] weekly;
    }

    [Serializable]
    public class MonthWeeklyResponse
    {
        public string period;
        public string unit;
        public MonthWeeklySeries[] series;
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
        const string cacheKey = "breakdown_month";
        if (TryGetFrontendCache(cacheKey, out BreakdownMonthResponse cached))
        {
            onSuccess?.Invoke(cached);
            return;
        }

        StartCoroutine(FetchBreakdownMonth(onSuccess, onError));
    }
    public void RequestMonthlyTrendLastMonths(
        int months,
        Action<EnergyTrendPayload> onOk,
        Action<string> onErr
    )
    {
        string cacheKey = $"trend_months_{months}";
        if (TryGetFrontendCache(cacheKey, out EnergyTrendPayload cached))
        {
            onOk?.Invoke(cached);
            return;
        }

        StartCoroutine(_RequestMonthlyTrendLastMonthsCo(months, onOk, onErr));
    }

    public void RequestPowerTimeseries(
        string window,
        string every,
        Action<EnergyUsageChartPayload> onOk,
        Action<string> onErr)
    {
        string safeWindow = string.IsNullOrWhiteSpace(window) ? "24h" : window;
        string safeEvery = string.IsNullOrWhiteSpace(every) ? "1h" : every;
        string cacheKey = $"power_ts_{safeWindow}_{safeEvery}";

        if (TryGetFrontendCache(cacheKey, out EnergyUsageChartPayload cached))
        {
            onOk?.Invoke(cached);
            return;
        }

        StartCoroutine(_RequestPowerTimeseriesCo(window, every, onOk, onErr));
    }

    public void RequestPowerTimeseriesBetween(
        DateTime fromInclusive,
        DateTime toInclusive,
        Action<EnergyUsageChartPayload> onOk,
        Action<string> onErr)
    {
        if (toInclusive < fromInclusive)
        {
            DateTime tmp = fromInclusive;
            fromInclusive = toInclusive;
            toInclusive = tmp;
        }

        var fromUtc = fromInclusive.ToUniversalTime();
        var toUtc = toInclusive.ToUniversalTime();

        double totalHours = Math.Max(1d, (toUtc - fromUtc).TotalHours);
        int windowHours = Mathf.Clamp((int)Math.Ceiling(totalHours) + 24, 1, 24 * 365);
        string window = windowHours + "h";

        string every;
        if (totalHours <= 72d)
            every = "1h";
        else if (totalHours <= 24d * 14d)
            every = "3h";
        else
            every = "1d";

        RequestPowerTimeseries(
            window,
            every,
            payload =>
            {
                var filtered = FilterPayloadByDateRange(payload, fromInclusive, toInclusive);
                onOk?.Invoke(filtered);
            },
            onErr
        );
    }

    public void RequestEnergyDaySeries(
        string month,
        Action<EnergyDayResponse> onOk,
        Action<string> onErr)
    {
        string safeMonth = string.IsNullOrWhiteSpace(month) ? DateTime.UtcNow.ToString("yyyy-MM") : month;
        string cacheKey = $"energy_day_{safeMonth}";

        if (TryGetFrontendCache(cacheKey, out EnergyDayResponse cached))
        {
            onOk?.Invoke(cached);
            return;
        }

        StartCoroutine(_RequestEnergyDaySeriesCo(month, onOk, onErr));
    }

    public void RequestTodayHourly(
        Action<TodayHourlyResponse> onOk,
        Action<string> onErr)
    {
        const string cacheKey = "today_hourly";
        if (TryGetFrontendCache(cacheKey, out TodayHourlyResponse cached))
        {
            onOk?.Invoke(cached);
            return;
        }

        StartCoroutine(_RequestTodayHourlyCo(onOk, onErr));
    }

    public void RequestWeekDaily(
        Action<WeekDailyResponse> onOk,
        Action<string> onErr)
    {
        const string cacheKey = "week_daily";
        if (TryGetFrontendCache(cacheKey, out WeekDailyResponse cached))
        {
            onOk?.Invoke(cached);
            return;
        }

        StartCoroutine(_RequestWeekDailyCo(onOk, onErr));
    }

    public void RequestMonthWeekly(
        Action<MonthWeeklyResponse> onOk,
        Action<string> onErr)
    {
        const string cacheKey = "month_weekly";
        if (TryGetFrontendCache(cacheKey, out MonthWeeklyResponse cached))
        {
            onOk?.Invoke(cached);
            return;
        }

        StartCoroutine(_RequestMonthWeeklyCo(onOk, onErr));
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
                OverviewUpdated?.Invoke(data);
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

                SetFrontendCache("breakdown_month", data, cacheBreakdownMonthSeconds);
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
                subtitle = "", // opcional: podes p�r "�ltimos 5 meses"
                unit = data.unit ?? "kWh",
                categories = new System.Collections.Generic.List<string>(n),
                values = new System.Collections.Generic.List<float>(n),
            };

            for (int i = 0; i < n; i++)
            {
                payload.categories.Add(data.labels[i]);
                payload.values.Add(Mathf.Max(0f, data.values[i]));
            }

            SetFrontendCache($"trend_months_{months}", payload, cacheTrendMonthsSeconds);

            onOk?.Invoke(payload);
        }
    }

    private IEnumerator _RequestPowerTimeseriesCo(
        string window,
        string every,
        Action<EnergyUsageChartPayload> onOk,
        Action<string> onErr)
    {
        string safeWindow = string.IsNullOrWhiteSpace(window) ? "24h" : window;
        string safeEvery = string.IsNullOrWhiteSpace(every) ? "1h" : every;
        string url = apiBaseUrl.TrimEnd('/') + "/energy/timeseries/power?window=" + safeWindow + "&every=" + safeEvery;

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke($"API error: {req.responseCode} {req.error} ({url})");
                yield break;
            }

            string json = req.downloadHandler.text;
            PowerTimeseriesResponse data = null;

            try { data = JsonUtility.FromJson<PowerTimeseriesResponse>(json); }
            catch (Exception e)
            {
                onErr?.Invoke($"JSON parse failed: {e.Message}");
                yield break;
            }

            if (data == null || data.meters == null)
            {
                onErr?.Invoke("Invalid payload from API.");
                yield break;
            }

            var rawSeries = new List<(string meterId, PowerTimeseriesMeter meter)>(3)
            {
                (ID_SAND, data.meters.shelly3EMJatoAreia),
                (ID_RIGHT, data.meters.shelly3EMPinturaDireita),
                (ID_LEFT, data.meters.shelly3EMPinturaEsquerda),
            };

            var timeKeys = new List<string>();
            var timeSet = new HashSet<string>();
            var seriesNames = new List<string>();
            var seriesPointsByTime = new List<Dictionary<string, float>>();

            for (int s = 0; s < rawSeries.Count; s++)
            {
                var meter = rawSeries[s].meter;
                if (meter == null) continue;

                string label = string.IsNullOrWhiteSpace(meter.label) ? rawSeries[s].meterId : meter.label;
                var byTime = new Dictionary<string, float>();

                if (meter.points != null)
                {
                    for (int p = 0; p < meter.points.Length; p++)
                    {
                        var point = meter.points[p];
                        if (point == null || string.IsNullOrWhiteSpace(point.time)) continue;

                        float value = Mathf.Max(0f, point.value);
                        byTime[point.time] = value;
                        if (timeSet.Add(point.time)) timeKeys.Add(point.time);
                    }
                }

                seriesNames.Add(label);
                seriesPointsByTime.Add(byTime);
            }

            if (timeKeys.Count == 0 || seriesPointsByTime.Count == 0)
            {
                onErr?.Invoke("No data.");
                yield break;
            }

            timeKeys.Sort((a, b) =>
            {
                bool okA = DateTime.TryParse(a, null, DateTimeStyles.RoundtripKind, out var dtA);
                bool okB = DateTime.TryParse(b, null, DateTimeStyles.RoundtripKind, out var dtB);
                if (okA && okB) return dtA.CompareTo(dtB);
                return string.CompareOrdinal(a, b);
            });

            var categories = new List<string>(timeKeys.Count);
            for (int i = 0; i < timeKeys.Count; i++)
            {
                var raw = timeKeys[i];
                if (DateTime.TryParse(raw, null, DateTimeStyles.RoundtripKind, out var dt))
                {
                    if (safeEvery == "3h")
                    {
                        categories.Add(dt.ToLocalTime().ToString("HH:mm"));
                    }
                    else if (safeEvery == "1d")
                    {
                        categories.Add(dt.ToLocalTime().ToString("dd/MM"));
                    }
                    else if (safeEvery == "7d")
                    {
                        categories.Add($"Week {i + 1}");
                    }
                    else
                    {
                        categories.Add(dt.ToLocalTime().ToString("dd/MM HH:mm"));
                    }
                }
                else
                    categories.Add(raw);
            }

            var payload = new EnergyUsageChartPayload
            {
                title = "Energy Usage Chart",
                subtitle = $"Window: {safeWindow} | Step: {safeEvery}",
                unit = string.IsNullOrWhiteSpace(data.unit) ? "W" : data.unit,
                categories = categories,
                timestamps = new List<string>(timeKeys),
                series = new List<EnergyUsageSeries>(seriesPointsByTime.Count)
            };

            for (int s = 0; s < seriesPointsByTime.Count; s++)
            {
                var byTime = seriesPointsByTime[s];
                var values = new List<float>(timeKeys.Count);

                for (int i = 0; i < timeKeys.Count; i++)
                {
                    if (byTime.TryGetValue(timeKeys[i], out var v)) values.Add(v);
                    else values.Add(0f);
                }

                payload.series.Add(new EnergyUsageSeries
                {
                    name = seriesNames[s],
                    values = values
                });
            }

            SetFrontendCache($"power_ts_{safeWindow}_{safeEvery}", payload, cachePowerTimeseriesSeconds);

            onOk?.Invoke(payload);
        }
    }

    private EnergyUsageChartPayload FilterPayloadByDateRange(
        EnergyUsageChartPayload payload,
        DateTime fromInclusive,
        DateTime toInclusive)
    {
        if (payload == null || payload.timestamps == null || payload.series == null)
            return payload;

        if (toInclusive < fromInclusive)
        {
            DateTime tmp = fromInclusive;
            fromInclusive = toInclusive;
            toInclusive = tmp;
        }

        DateTime fromDate = fromInclusive.Date;
        DateTime toDate = toInclusive.Date;

        var keepIdx = new List<int>();
        for (int i = 0; i < payload.timestamps.Count; i++)
        {
            string ts = payload.timestamps[i];
            if (!DateTime.TryParse(ts, null, DateTimeStyles.RoundtripKind, out var dt))
                continue;

            DateTime localDate = dt.ToLocalTime().Date;
            if (localDate >= fromDate && localDate <= toDate)
                keepIdx.Add(i);
        }

        if (keepIdx.Count == 0)
            return payload;

        var filtered = new EnergyUsageChartPayload
        {
            title = payload.title,
            subtitle = $"{fromDate:yyyy-MM-dd} → {toDate:yyyy-MM-dd}",
            unit = payload.unit,
            categories = new List<string>(keepIdx.Count),
            timestamps = new List<string>(keepIdx.Count),
            series = new List<EnergyUsageSeries>(payload.series.Count)
        };

        for (int k = 0; k < keepIdx.Count; k++)
        {
            int idx = keepIdx[k];
            filtered.timestamps.Add(payload.timestamps[idx]);

            if (payload.categories != null && idx < payload.categories.Count)
                filtered.categories.Add(payload.categories[idx]);
            else
                filtered.categories.Add((k + 1).ToString());
        }

        for (int s = 0; s < payload.series.Count; s++)
        {
            var src = payload.series[s];
            if (src == null)
                continue;

            var vals = new List<float>(keepIdx.Count);
            for (int k = 0; k < keepIdx.Count; k++)
            {
                int idx = keepIdx[k];
                float v = (src.values != null && idx < src.values.Count) ? src.values[idx] : 0f;
                vals.Add(v);
            }

            filtered.series.Add(new EnergyUsageSeries
            {
                name = src.name,
                values = vals
            });
        }

        return filtered;
    }

    private IEnumerator _RequestEnergyDaySeriesCo(
        string month,
        Action<EnergyDayResponse> onOk,
        Action<string> onErr)
    {
        string safeMonth = string.IsNullOrWhiteSpace(month) ? DateTime.UtcNow.ToString("yyyy-MM") : month;
        string url = apiBaseUrl.TrimEnd('/') + "/energy/timeseries/energy/day?month=" + safeMonth;

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke($"API error: {req.responseCode} {req.error} ({url})");
                yield break;
            }

            string json = req.downloadHandler.text;
            EnergyDayResponse data = null;

            try { data = JsonUtility.FromJson<EnergyDayResponse>(json); }
            catch (Exception e)
            {
                onErr?.Invoke($"JSON parse failed: {e.Message}");
                yield break;
            }

            if (data == null || data.meters == null)
            {
                onErr?.Invoke("Invalid payload from API.");
                yield break;
            }

            SetFrontendCache($"energy_day_{safeMonth}", data, cacheEnergyDaySeriesSeconds);

            onOk?.Invoke(data);
        }
    }

    private IEnumerator _RequestTodayHourlyCo(
        Action<TodayHourlyResponse> onOk,
        Action<string> onErr)
    {
        string url = apiBaseUrl.TrimEnd('/') + "/energy/today/hourly";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke($"API error: {req.responseCode} {req.error} ({url})");
                yield break;
            }

            TodayHourlyResponse data = null;
            try { data = JsonUtility.FromJson<TodayHourlyResponse>(req.downloadHandler.text); }
            catch (Exception e)
            {
                onErr?.Invoke($"JSON parse failed: {e.Message}");
                yield break;
            }

            if (data == null || data.series == null)
            {
                onErr?.Invoke("Invalid payload from API.");
                yield break;
            }

            SetFrontendCache("today_hourly", data, cacheTodayHourlySeconds);

            onOk?.Invoke(data);
        }
    }

    private IEnumerator _RequestWeekDailyCo(
        Action<WeekDailyResponse> onOk,
        Action<string> onErr)
    {
        string url = apiBaseUrl.TrimEnd('/') + "/energy/week/daily";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke($"API error: {req.responseCode} {req.error} ({url})");
                yield break;
            }

            WeekDailyResponse data = null;
            try { data = JsonUtility.FromJson<WeekDailyResponse>(req.downloadHandler.text); }
            catch (Exception e)
            {
                onErr?.Invoke($"JSON parse failed: {e.Message}");
                yield break;
            }

            if (data == null || data.series == null)
            {
                onErr?.Invoke("Invalid payload from API.");
                yield break;
            }

            SetFrontendCache("week_daily", data, cacheWeekDailySeconds);

            onOk?.Invoke(data);
        }
    }

    private IEnumerator _RequestMonthWeeklyCo(
        Action<MonthWeeklyResponse> onOk,
        Action<string> onErr)
    {
        string url = apiBaseUrl.TrimEnd('/') + "/energy/month/weekly";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke($"API error: {req.responseCode} {req.error} ({url})");
                yield break;
            }

            MonthWeeklyResponse data = null;
            try { data = JsonUtility.FromJson<MonthWeeklyResponse>(req.downloadHandler.text); }
            catch (Exception e)
            {
                onErr?.Invoke($"JSON parse failed: {e.Message}");
                yield break;
            }

            if (data == null || data.series == null)
            {
                onErr?.Invoke("Invalid payload from API.");
                yield break;
            }

            SetFrontendCache("month_weekly", data, cacheMonthWeeklySeconds);

            onOk?.Invoke(data);
        }
    }

    private bool TryGetFrontendCache<T>(string key, out T value) where T : class
    {
        value = null;
        if (!enableFrontendCache) return false;

        if (!frontendCache.TryGetValue(key, out var entry) || entry == null || entry.Data == null)
            return false;

        if (Time.unscaledTime > entry.ExpireAt)
        {
            frontendCache.Remove(key);
            return false;
        }

        value = entry.Data as T;
        return value != null;
    }

    private void SetFrontendCache(string key, object value, float ttlSeconds)
    {
        if (!enableFrontendCache || value == null) return;

        frontendCache[key] = new FrontendCacheEntry
        {
            Data = value,
            ExpireAt = Time.unscaledTime + Mathf.Max(1f, ttlSeconds)
        };
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



