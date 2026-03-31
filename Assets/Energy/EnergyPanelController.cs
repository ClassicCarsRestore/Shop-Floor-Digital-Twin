using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class EnergyPanelController : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string apiBaseUrl = "https://sensors.raimundobranco.com";
    [SerializeField] private int requestTimeoutSeconds = 10;
    [SerializeField] private float refreshSeconds = 5f;
    [SerializeField] private bool autoRefresh = true;

    [Header("Panels - Totals")]
    [SerializeField] private MetricPanelUI currentEnergyPanel;
    [SerializeField] private MetricPanelUI monthEnergyPanel;

    [Header("Panels - Current (W)")]
    [SerializeField] private MetricPanelUI currentJatoAreiaPanel;
    [SerializeField] private MetricPanelUI currentPinturaDireitaPanel;
    [SerializeField] private MetricPanelUI currentPinturaEsquerdaPanel;
    [SerializeField] private MetricPanelUI currentQuadroPainelSolarPanel;
    [SerializeField] private MetricPanelUI currentCompressorPanel;
    [SerializeField] private MetricPanelUI currentCarregadorParedePanel;
    [SerializeField] private MetricPanelUI currentBateChapasEsquerdaPanel;
    [SerializeField] private MetricPanelUI currentBateChapasDireitaPanel;
    [SerializeField] private MetricPanelUI currentArCondicionadoPanel;
    [SerializeField] private MetricPanelUI currentSalaConvivioPanel;
    [SerializeField] private MetricPanelUI currentTomadasOficinaPanel;
    [SerializeField] private MetricPanelUI currentPortaoEletricoPanel;

    [Header("Panels - Month (kWh)")]
    [SerializeField] private MetricPanelUI monthJatoAreiaPanel;
    [SerializeField] private MetricPanelUI monthPinturaDireitaPanel;
    [SerializeField] private MetricPanelUI monthPinturaEsquerdaPanel;
    [SerializeField] private MetricPanelUI monthQuadroPainelSolarPanel;
    [SerializeField] private MetricPanelUI monthCompressorPanel;
    [SerializeField] private MetricPanelUI monthCarregadorParedePanel;
    [SerializeField] private MetricPanelUI monthBateChapasEsquerdaPanel;
    [SerializeField] private MetricPanelUI monthBateChapasDireitaPanel;
    [SerializeField] private MetricPanelUI monthArCondicionadoPanel;
    [SerializeField] private MetricPanelUI monthSalaConvivioPanel;
    [SerializeField] private MetricPanelUI monthTomadasOficinaPanel;
    [SerializeField] private MetricPanelUI monthPortaoEletricoPanel;

    [Header("Frontend Cache")]
    [SerializeField] private bool enableFrontendCache = true;
    [SerializeField] private float cacheBreakdownMonthSeconds = 30f;
    [SerializeField] private float cacheTrendMonthsSeconds = 120f;
    [SerializeField] private float cacheTodayHourlySeconds = 10f;
    [SerializeField] private float cacheWeekDailySeconds = 30f;
    [SerializeField] private float cacheMonthWeeklySeconds = 60f;
    [SerializeField] private float cachePowerTimeseriesSeconds = 10f;
    [SerializeField] private float cacheEnergyDaySeriesSeconds = 120f;

    private const string ID_JATO_AREIA = "shelly3EMJatoAreia";
    private const string ID_PINTURA_DIREITA = "shelly3EMPinturaDireita";
    private const string ID_PINTURA_ESQUERDA = "shelly3EMPinturaEsquerda";
    private const string ID_QUADRO_PAINEL_SOLAR = "shelly3EMQuadroPainelSolar";
    private const string ID_COMPRESSOR = "shelly3EMCompressor";
    private const string ID_CARREGADOR_PAREDE = "shelly3EMCarregadorParede";
    private const string ID_BATE_CHAPAS_ESQ = "shelly3EMBateChapasEsquerda";
    private const string ID_BATE_CHAPAS_DIR = "shelly3EMBateChapasDireita";
    private const string ID_AR_CONDICIONADO = "shellyProEM50ArCondicionado";
    private const string ID_SALA_CONVIVIO = "shellyProEM50SalaConvivio";
    private const string ID_TOMADAS_OFICINA = "shellyProEM50TomadasOficina";
    private const string ID_PORTAO_ELETRICO = "shellyProEM50PortaoEletrico";

    private const string LABEL_JATO_AREIA = "Jato de Areia";
    private const string LABEL_PINTURA_DIREITA = "Pintura Direita";
    private const string LABEL_PINTURA_ESQUERDA = "Pintura Esquerda";
    private const string LABEL_QUADRO_PAINEL_SOLAR = "Quadro Painel Solar";
    private const string LABEL_COMPRESSOR = "Compressor";
    private const string LABEL_CARREGADOR_PAREDE = "Carregador Parede";
    private const string LABEL_BATE_CHAPAS_ESQ = "Bate Chapas Esquerda";
    private const string LABEL_BATE_CHAPAS_DIR = "Bate Chapas Direita";
    private const string LABEL_AR_CONDICIONADO = "Ar Condicionado";
    private const string LABEL_SALA_CONVIVIO = "Sala Convivio";
    private const string LABEL_TOMADAS_OFICINA = "Tomadas Oficina";
    private const string LABEL_PORTAO_ELETRICO = "Portao Eletrico";

    private static readonly string[] METER_IDS =
    {
        ID_JATO_AREIA,
        ID_PINTURA_DIREITA,
        ID_PINTURA_ESQUERDA,
        ID_QUADRO_PAINEL_SOLAR,
        ID_COMPRESSOR,
        ID_CARREGADOR_PAREDE,
        ID_BATE_CHAPAS_ESQ,
        ID_BATE_CHAPAS_DIR,
        ID_AR_CONDICIONADO,
        ID_SALA_CONVIVIO,
        ID_TOMADAS_OFICINA,
        ID_PORTAO_ELETRICO
    };

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
        public string unit;
        public int months;
        public bool include_current;
        public string[] labels;
        public float[] values;
    }

    [Serializable]
    public class EnergyTrendPayload
    {
        public string title;
        public string subtitle;
        public List<string> categories;
        public List<float> values;
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
        public PowerTimeseriesMeter shelly3EMQuadroPainelSolar;
        public PowerTimeseriesMeter shelly3EMCompressor;
        public PowerTimeseriesMeter shelly3EMCarregadorParede;
        public PowerTimeseriesMeter shelly3EMBateChapasEsquerda;
        public PowerTimeseriesMeter shelly3EMBateChapasDireita;
        public PowerTimeseriesMeter shellyProEM50ArCondicionado;
        public PowerTimeseriesMeter shellyProEM50SalaConvivio;
        public PowerTimeseriesMeter shellyProEM50TomadasOficina;
        public PowerTimeseriesMeter shellyProEM50PortaoEletrico;
    }

    [Serializable]
    public class PowerTimeseriesResponse
    {
        public string generated_at;
        public string window;
        public string from;
        public string to;
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
        public EnergyDayMeter shelly3EMQuadroPainelSolar;
        public EnergyDayMeter shelly3EMCompressor;
        public EnergyDayMeter shelly3EMCarregadorParede;
        public EnergyDayMeter shelly3EMBateChapasEsquerda;
        public EnergyDayMeter shelly3EMBateChapasDireita;
        public EnergyDayMeter shellyProEM50ArCondicionado;
        public EnergyDayMeter shellyProEM50SalaConvivio;
        public EnergyDayMeter shellyProEM50TomadasOficina;
        public EnergyDayMeter shellyProEM50PortaoEletrico;
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
        Action<string> onErr)
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
        string safeEvery = NormalizeEveryForApi(every);
        string cacheKey = $"power_ts_{safeWindow}_{safeEvery}";

        if (TryGetFrontendCache(cacheKey, out EnergyUsageChartPayload cached))
        {
            onOk?.Invoke(cached);
            return;
        }

        StartCoroutine(_RequestPowerTimeseriesCo(safeWindow, safeEvery, onOk, onErr));
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
        string every = SelectEveryForRange(totalHours);
        string fromIso = fromUtc.ToString("o", CultureInfo.InvariantCulture);
        string toIso = toUtc.ToString("o", CultureInfo.InvariantCulture);

        string cacheKey = $"power_ts_range_{fromIso}_{toIso}_{every}";
        if (TryGetFrontendCache(cacheKey, out EnergyUsageChartPayload cached))
        {
            var adaptedCached = AdaptCustomRangeXAxis(cached, fromInclusive, toInclusive);
            if (adaptedCached == null || adaptedCached.categories == null || adaptedCached.categories.Count == 0)
                adaptedCached = BuildEmptyCustomRangePayload(fromInclusive, toInclusive, cached);

            onOk?.Invoke(adaptedCached);
            return;
        }

        StartCoroutine(_RequestPowerTimeseriesCo(
            null,
            every,
            payload =>
            {
                SetFrontendCache(cacheKey, payload, cachePowerTimeseriesSeconds);

                var adapted = AdaptCustomRangeXAxis(payload, fromInclusive, toInclusive);
                if (adapted == null || adapted.categories == null || adapted.categories.Count == 0)
                    adapted = BuildEmptyCustomRangePayload(fromInclusive, toInclusive, payload);

                onOk?.Invoke(adapted);
            },
            err =>
            {
                if (IsNoDataError(err))
                {
                    onOk?.Invoke(BuildEmptyCustomRangePayload(fromInclusive, toInclusive, null));
                    return;
                }

                onErr?.Invoke(err);
            },
            fromIso,
            toIso
        ));
    }

    public void RequestEnergyDayTimeseriesBetween(
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

        DateTime fromDate = fromInclusive.Date;
        DateTime toDate = toInclusive.Date;
        string cacheKey = $"energy_day_range_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}";

        if (TryGetFrontendCache(cacheKey, out EnergyUsageChartPayload cached))
        {
            onOk?.Invoke(cached);
            return;
        }

        StartCoroutine(_RequestEnergyDayTimeseriesBetweenCo(fromDate, toDate, cacheKey, onOk, onErr));
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

    private IEnumerator FetchAndApplyOverview()
    {
        string url = BuildUrl("/energy/dashboard/overview");

        yield return GetJson<OverviewResponse>(
            url,
            data =>
            {
                if (data == null)
                {
                    ApplyErrorState();
                    return;
                }

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
        Action<string> onErr)
    {
        string url = apiBaseUrl.TrimEnd('/') + "/energy/trend/months?months=" + months;

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke($"API error: {req.responseCode} {req.error} ({url})");
                yield break;
            }

            var json = req.downloadHandler.text;
            EnergyTrendMonthsResponse data = null;

            try
            {
                data = JsonUtility.FromJson<EnergyTrendMonthsResponse>(json);
            }
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
                subtitle = "",
                unit = data.unit ?? "kWh",
                categories = new List<string>(n),
                values = new List<float>(n),
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
        Action<string> onErr,
        string fromIso = null,
        string toIso = null)
    {
        string safeEvery = NormalizeEveryForApi(every);

        bool hasAbsoluteRange = !string.IsNullOrWhiteSpace(fromIso) && !string.IsNullOrWhiteSpace(toIso);
        string safeWindow = string.IsNullOrWhiteSpace(window) ? "24h" : window;

        string url;
        if (hasAbsoluteRange)
        {
            string safeFrom = UnityWebRequest.EscapeURL(fromIso);
            string safeTo = UnityWebRequest.EscapeURL(toIso);
            url = apiBaseUrl.TrimEnd('/') + "/energy/timeseries/power?from=" + safeFrom + "&to=" + safeTo + "&every=" + safeEvery;
        }
        else
        {
            url = apiBaseUrl.TrimEnd('/') + "/energy/timeseries/power?window=" + safeWindow + "&every=" + safeEvery;
        }

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

            try
            {
                data = JsonUtility.FromJson<PowerTimeseriesResponse>(json);
            }
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

            var timeKeys = new List<string>();
            var timeSet = new HashSet<string>();
            var seriesNames = new List<string>();
            var seriesPointsByTime = new List<Dictionary<string, float>>();

            for (int i = 0; i < METER_IDS.Length; i++)
            {
                string meterId = METER_IDS[i];
                PowerTimeseriesMeter meter = GetPowerTimeseriesMeter(data.meters, meterId);
                if (meter == null) continue;

                string label = string.IsNullOrWhiteSpace(meter.label) ? GetDefaultLabelForMeterId(meterId) : meter.label;
                var byTime = new Dictionary<string, float>();

                if (meter.points != null)
                {
                    for (int p = 0; p < meter.points.Length; p++)
                    {
                        var point = meter.points[p];
                        if (point == null || string.IsNullOrWhiteSpace(point.time)) continue;

                        float value = Mathf.Max(0f, point.value);
                        byTime[point.time] = value;
                        if (timeSet.Add(point.time))
                            timeKeys.Add(point.time);
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
                    if (safeEvery == "1m" || safeEvery == "3h")
                        categories.Add(dt.ToLocalTime().ToString("HH:mm"));
                    else if (safeEvery == "1d")
                        categories.Add(dt.ToLocalTime().ToString("dd/MM"));
                    else if (safeEvery == "7d")
                        categories.Add($"Week {i + 1}");
                    else
                        categories.Add(dt.ToLocalTime().ToString("dd/MM HH:mm"));
                }
                else
                {
                    categories.Add(raw);
                }
            }

            var payload = new EnergyUsageChartPayload
            {
                title = "Energy Usage Chart",
                subtitle = hasAbsoluteRange
                    ? $"Range: {fromIso} → {toIso} | Step: {safeEvery}"
                    : $"Window: {safeWindow} | Step: {safeEvery}",
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
                    if (byTime.TryGetValue(timeKeys[i], out var v))
                        values.Add(v);
                    else
                        values.Add(0f);
                }

                payload.series.Add(new EnergyUsageSeries
                {
                    name = seriesNames[s],
                    values = values
                });
            }

            if (!hasAbsoluteRange)
                SetFrontendCache($"power_ts_{safeWindow}_{safeEvery}", payload, cachePowerTimeseriesSeconds);

            onOk?.Invoke(payload);
        }
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

            try
            {
                data = JsonUtility.FromJson<EnergyDayResponse>(json);
            }
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

    private IEnumerator _RequestEnergyDayTimeseriesBetweenCo(
        DateTime fromDate,
        DateTime toDate,
        string cacheKey,
        Action<EnergyUsageChartPayload> onOk,
        Action<string> onErr)
    {
        var labelsByMeter = new Dictionary<string, string>();
        var valuesByMeterDate = new Dictionary<string, Dictionary<DateTime, float>>();

        for (int i = 0; i < METER_IDS.Length; i++)
        {
            string meterId = METER_IDS[i];
            labelsByMeter[meterId] = GetDefaultLabelForMeterId(meterId);
            valuesByMeterDate[meterId] = new Dictionary<DateTime, float>();
        }

        string unit = "kWh";

        DateTime monthCursor = new DateTime(fromDate.Year, fromDate.Month, 1);
        DateTime monthEnd = new DateTime(toDate.Year, toDate.Month, 1);

        while (monthCursor <= monthEnd)
        {
            string safeMonth = monthCursor.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            string monthCacheKey = $"energy_day_{safeMonth}";

            EnergyDayResponse monthData;
            if (!TryGetFrontendCache(monthCacheKey, out monthData))
            {
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

                    try
                    {
                        monthData = JsonUtility.FromJson<EnergyDayResponse>(req.downloadHandler.text);
                    }
                    catch (Exception e)
                    {
                        onErr?.Invoke($"JSON parse failed: {e.Message}");
                        yield break;
                    }

                    if (monthData == null || monthData.meters == null)
                    {
                        onErr?.Invoke("Invalid payload from API.");
                        yield break;
                    }

                    SetFrontendCache(monthCacheKey, monthData, cacheEnergyDaySeriesSeconds);
                }
            }

            if (monthData != null)
            {
                if (!string.IsNullOrWhiteSpace(monthData.unit))
                    unit = monthData.unit;

                for (int i = 0; i < METER_IDS.Length; i++)
                {
                    string meterId = METER_IDS[i];
                    MergeEnergyDayMeter(
                        GetEnergyDayMeter(monthData.meters, meterId),
                        meterId,
                        labelsByMeter,
                        valuesByMeterDate,
                        fromDate,
                        toDate
                    );
                }
            }

            monthCursor = monthCursor.AddMonths(1);
        }

        int dayCount = Mathf.Max(1, (toDate - fromDate).Days + 1);
        var categories = new List<string>(dayCount);
        var timestamps = new List<string>(dayCount);

        for (int i = 0; i < dayCount; i++)
        {
            DateTime d = fromDate.AddDays(i);
            categories.Add(d.ToString("dd/MM"));
            timestamps.Add(d.ToString("yyyy-MM-dd'T'00:00:00K", CultureInfo.InvariantCulture));
        }

        var payload = new EnergyUsageChartPayload
        {
            title = "Energy Usage Chart - By Date",
            subtitle = $"{fromDate:yyyy-MM-dd} → {toDate:yyyy-MM-dd} (Energy/day)",
            unit = string.IsNullOrWhiteSpace(unit) ? "kWh" : unit,
            categories = categories,
            timestamps = timestamps,
            series = new List<EnergyUsageSeries>(METER_IDS.Length)
        };

        for (int m = 0; m < METER_IDS.Length; m++)
        {
            string meterId = METER_IDS[m];
            var meterValues = valuesByMeterDate[meterId];
            var values = new List<float>(dayCount);

            for (int i = 0; i < dayCount; i++)
            {
                DateTime d = fromDate.AddDays(i);
                values.Add(meterValues.TryGetValue(d, out var v) ? Mathf.Max(0f, v) : 0f);
            }

            payload.series.Add(new EnergyUsageSeries
            {
                name = labelsByMeter.TryGetValue(meterId, out var label) ? label : meterId,
                values = values
            });
        }

        payload = AdaptCustomRangeXAxis(payload, fromDate, toDate);
        SetFrontendCache(cacheKey, payload, cacheEnergyDaySeriesSeconds);
        onOk?.Invoke(payload);
    }

    private void MergeEnergyDayMeter(
        EnergyDayMeter meter,
        string meterId,
        Dictionary<string, string> labelsByMeter,
        Dictionary<string, Dictionary<DateTime, float>> valuesByMeterDate,
        DateTime fromDate,
        DateTime toDate)
    {
        if (meter == null) return;

        if (!string.IsNullOrWhiteSpace(meter.label))
            labelsByMeter[meterId] = meter.label;

        if (meter.points == null || !valuesByMeterDate.TryGetValue(meterId, out var dst))
            return;

        for (int i = 0; i < meter.points.Length; i++)
        {
            var p = meter.points[i];
            if (p == null || string.IsNullOrWhiteSpace(p.date)) continue;

            if (!DateTime.TryParseExact(p.date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                continue;

            DateTime day = d.Date;
            if (day < fromDate || day > toDate) continue;

            dst[day] = Mathf.Max(0f, p.value);
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
            try
            {
                data = JsonUtility.FromJson<TodayHourlyResponse>(req.downloadHandler.text);
            }
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
            try
            {
                data = JsonUtility.FromJson<WeekDailyResponse>(req.downloadHandler.text);
            }
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
            try
            {
                data = JsonUtility.FromJson<MonthWeeklyResponse>(req.downloadHandler.text);
            }
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

    private void ApplyToUI(OverviewResponse data)
    {
        SetPanel(currentEnergyPanel, "Current Energy Consumption", FormatW(data != null && data.total != null ? data.total.current_power_w : 0f));
        SetPanel(monthEnergyPanel, "This Month Energy Consumption", FormatKwh(data != null && data.total != null ? data.total.month_energy_kwh : 0f));

        Dictionary<string, MeterBlock> byId = new Dictionary<string, MeterBlock>();
        if (data != null && data.meters != null)
        {
            foreach (var m in data.meters)
            {
                if (m != null && !string.IsNullOrEmpty(m.id))
                    byId[m.id] = m;
            }
        }

        SetPanel(currentJatoAreiaPanel, $"Current {LABEL_JATO_AREIA}", FormatW(GetCurrentW(byId, ID_JATO_AREIA)));
        SetPanel(currentPinturaDireitaPanel, $"Current {LABEL_PINTURA_DIREITA}", FormatW(GetCurrentW(byId, ID_PINTURA_DIREITA)));
        SetPanel(currentPinturaEsquerdaPanel, $"Current {LABEL_PINTURA_ESQUERDA}", FormatW(GetCurrentW(byId, ID_PINTURA_ESQUERDA)));
        SetPanel(currentQuadroPainelSolarPanel, $"Current {LABEL_QUADRO_PAINEL_SOLAR}", FormatW(GetCurrentW(byId, ID_QUADRO_PAINEL_SOLAR)));
        SetPanel(currentCompressorPanel, $"Current {LABEL_COMPRESSOR}", FormatW(GetCurrentW(byId, ID_COMPRESSOR)));
        SetPanel(currentCarregadorParedePanel, $"Current {LABEL_CARREGADOR_PAREDE}", FormatW(GetCurrentW(byId, ID_CARREGADOR_PAREDE)));
        SetPanel(currentBateChapasEsquerdaPanel, $"Current {LABEL_BATE_CHAPAS_ESQ}", FormatW(GetCurrentW(byId, ID_BATE_CHAPAS_ESQ)));
        SetPanel(currentBateChapasDireitaPanel, $"Current {LABEL_BATE_CHAPAS_DIR}", FormatW(GetCurrentW(byId, ID_BATE_CHAPAS_DIR)));
        SetPanel(currentArCondicionadoPanel, $"Current {LABEL_AR_CONDICIONADO}", FormatW(GetCurrentW(byId, ID_AR_CONDICIONADO)));
        SetPanel(currentSalaConvivioPanel, $"Current {LABEL_SALA_CONVIVIO}", FormatW(GetCurrentW(byId, ID_SALA_CONVIVIO)));
        SetPanel(currentTomadasOficinaPanel, $"Current {LABEL_TOMADAS_OFICINA}", FormatW(GetCurrentW(byId, ID_TOMADAS_OFICINA)));
        SetPanel(currentPortaoEletricoPanel, $"Current {LABEL_PORTAO_ELETRICO}", FormatW(GetCurrentW(byId, ID_PORTAO_ELETRICO)));

        SetPanel(monthJatoAreiaPanel, $"This Month {LABEL_JATO_AREIA}", FormatKwh(GetMonthKwh(byId, ID_JATO_AREIA)));
        SetPanel(monthPinturaDireitaPanel, $"This Month {LABEL_PINTURA_DIREITA}", FormatKwh(GetMonthKwh(byId, ID_PINTURA_DIREITA)));
        SetPanel(monthPinturaEsquerdaPanel, $"This Month {LABEL_PINTURA_ESQUERDA}", FormatKwh(GetMonthKwh(byId, ID_PINTURA_ESQUERDA)));
        SetPanel(monthQuadroPainelSolarPanel, $"This Month {LABEL_QUADRO_PAINEL_SOLAR}", FormatKwh(GetMonthKwh(byId, ID_QUADRO_PAINEL_SOLAR)));
        SetPanel(monthCompressorPanel, $"This Month {LABEL_COMPRESSOR}", FormatKwh(GetMonthKwh(byId, ID_COMPRESSOR)));
        SetPanel(monthCarregadorParedePanel, $"This Month {LABEL_CARREGADOR_PAREDE}", FormatKwh(GetMonthKwh(byId, ID_CARREGADOR_PAREDE)));
        SetPanel(monthBateChapasEsquerdaPanel, $"This Month {LABEL_BATE_CHAPAS_ESQ}", FormatKwh(GetMonthKwh(byId, ID_BATE_CHAPAS_ESQ)));
        SetPanel(monthBateChapasDireitaPanel, $"This Month {LABEL_BATE_CHAPAS_DIR}", FormatKwh(GetMonthKwh(byId, ID_BATE_CHAPAS_DIR)));
        SetPanel(monthArCondicionadoPanel, $"This Month {LABEL_AR_CONDICIONADO}", FormatKwh(GetMonthKwh(byId, ID_AR_CONDICIONADO)));
        SetPanel(monthSalaConvivioPanel, $"This Month {LABEL_SALA_CONVIVIO}", FormatKwh(GetMonthKwh(byId, ID_SALA_CONVIVIO)));
        SetPanel(monthTomadasOficinaPanel, $"This Month {LABEL_TOMADAS_OFICINA}", FormatKwh(GetMonthKwh(byId, ID_TOMADAS_OFICINA)));
        SetPanel(monthPortaoEletricoPanel, $"This Month {LABEL_PORTAO_ELETRICO}", FormatKwh(GetMonthKwh(byId, ID_PORTAO_ELETRICO)));
    }

    private void ApplyErrorState()
    {
        const string na = "--";

        SetPanel(currentEnergyPanel, "Current Energy Consumption", na);
        SetPanel(monthEnergyPanel, "This Month Energy Consumption", na);

        SetPanel(currentJatoAreiaPanel, $"Current {LABEL_JATO_AREIA}", na);
        SetPanel(currentPinturaDireitaPanel, $"Current {LABEL_PINTURA_DIREITA}", na);
        SetPanel(currentPinturaEsquerdaPanel, $"Current {LABEL_PINTURA_ESQUERDA}", na);
        SetPanel(currentQuadroPainelSolarPanel, $"Current {LABEL_QUADRO_PAINEL_SOLAR}", na);
        SetPanel(currentCompressorPanel, $"Current {LABEL_COMPRESSOR}", na);
        SetPanel(currentCarregadorParedePanel, $"Current {LABEL_CARREGADOR_PAREDE}", na);
        SetPanel(currentBateChapasEsquerdaPanel, $"Current {LABEL_BATE_CHAPAS_ESQ}", na);
        SetPanel(currentBateChapasDireitaPanel, $"Current {LABEL_BATE_CHAPAS_DIR}", na);
        SetPanel(currentArCondicionadoPanel, $"Current {LABEL_AR_CONDICIONADO}", na);
        SetPanel(currentSalaConvivioPanel, $"Current {LABEL_SALA_CONVIVIO}", na);
        SetPanel(currentTomadasOficinaPanel, $"Current {LABEL_TOMADAS_OFICINA}", na);
        SetPanel(currentPortaoEletricoPanel, $"Current {LABEL_PORTAO_ELETRICO}", na);

        SetPanel(monthJatoAreiaPanel, $"This Month {LABEL_JATO_AREIA}", na);
        SetPanel(monthPinturaDireitaPanel, $"This Month {LABEL_PINTURA_DIREITA}", na);
        SetPanel(monthPinturaEsquerdaPanel, $"This Month {LABEL_PINTURA_ESQUERDA}", na);
        SetPanel(monthQuadroPainelSolarPanel, $"This Month {LABEL_QUADRO_PAINEL_SOLAR}", na);
        SetPanel(monthCompressorPanel, $"This Month {LABEL_COMPRESSOR}", na);
        SetPanel(monthCarregadorParedePanel, $"This Month {LABEL_CARREGADOR_PAREDE}", na);
        SetPanel(monthBateChapasEsquerdaPanel, $"This Month {LABEL_BATE_CHAPAS_ESQ}", na);
        SetPanel(monthBateChapasDireitaPanel, $"This Month {LABEL_BATE_CHAPAS_DIR}", na);
        SetPanel(monthArCondicionadoPanel, $"This Month {LABEL_AR_CONDICIONADO}", na);
        SetPanel(monthSalaConvivioPanel, $"This Month {LABEL_SALA_CONVIVIO}", na);
        SetPanel(monthTomadasOficinaPanel, $"This Month {LABEL_TOMADAS_OFICINA}", na);
        SetPanel(monthPortaoEletricoPanel, $"This Month {LABEL_PORTAO_ELETRICO}", na);
    }

    private void SetCurrentUsage(MetricPanelUI panel, string label, float value)
    {
        SetPanel(panel, $"Current {label} Usage", FormatW(value));
    }

    private void SetMonthUsage(MetricPanelUI panel, string label, float value)
    {
        SetPanel(panel, $"This Month {label} Usage", FormatKwh(value));
    }

    private void SetPanel(MetricPanelUI panel, string title, string value)
    {
        if (panel != null)
            panel.Set(title, value);
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

    private string FormatW(float w) => $"{Mathf.Max(0f, w):0.##} W";
    private string FormatKwh(float k) => $"{Mathf.Max(0f, k):0.###} kWh";

    private string SelectEveryForRange(double totalHours)
    {
        if (totalHours <= 24d) return "1h";
        if (totalHours <= 24d * 10d) return "3h";
        if (totalHours <= 24d * 120d) return "1d";
        return "7d";
    }

    private EnergyUsageChartPayload AdaptCustomRangeXAxis(
        EnergyUsageChartPayload payload,
        DateTime fromInclusive,
        DateTime toInclusive)
    {
        if (payload == null || payload.timestamps == null || payload.categories == null)
            return payload;

        if (payload.timestamps.Count == 0 || payload.categories.Count == 0)
            return payload;

        if (toInclusive < fromInclusive)
        {
            DateTime tmp = fromInclusive;
            fromInclusive = toInclusive;
            toInclusive = tmp;
        }

        DateTime fromLocal = fromInclusive.ToLocalTime();
        DateTime toLocal = toInclusive.ToLocalTime();

        GetAdaptiveAxisPlan(fromLocal, toLocal, out int stepHours, out int stepDays, out int stepMonths, out string labelFormat, out string stepLabel);

        int previousBucket = int.MinValue;
        int lastNonEmpty = -1;

        for (int i = 0; i < payload.timestamps.Count; i++)
        {
            if (!DateTime.TryParse(payload.timestamps[i], null, DateTimeStyles.RoundtripKind, out var dt))
            {
                payload.categories[i] = i == 0 ? payload.categories[i] : string.Empty;
                continue;
            }

            DateTime local = dt.ToLocalTime();
            int bucket = GetAxisBucket(local, fromLocal, stepHours, stepDays, stepMonths);
            bool shouldShow = i == 0 || bucket != previousBucket;
            payload.categories[i] = shouldShow ? local.ToString(labelFormat) : string.Empty;

            if (shouldShow)
            {
                previousBucket = bucket;
                lastNonEmpty = i;
            }
        }

        if (payload.categories.Count > 0)
        {
            int last = payload.categories.Count - 1;
            if (string.IsNullOrEmpty(payload.categories[last]) &&
                DateTime.TryParse(payload.timestamps[last], null, DateTimeStyles.RoundtripKind, out var dtLast))
            {
                payload.categories[last] = dtLast.ToLocalTime().ToString(labelFormat);
            }
        }

        payload.subtitle = $"{fromLocal:yyyy-MM-dd} → {toLocal:yyyy-MM-dd} (Power, step {stepLabel})";
        return payload;
    }

    private EnergyUsageChartPayload BuildEmptyCustomRangePayload(
        DateTime fromInclusive,
        DateTime toInclusive,
        EnergyUsageChartPayload template)
    {
        if (toInclusive < fromInclusive)
        {
            DateTime tmp = fromInclusive;
            fromInclusive = toInclusive;
            toInclusive = tmp;
        }

        DateTime fromLocal = fromInclusive.ToLocalTime();
        DateTime toLocal = toInclusive.ToLocalTime();

        var timeline = BuildAdaptiveTimeline(fromLocal, toLocal, out string labelFormat, out string stepLabel);
        if (timeline.Count == 0)
            timeline.Add(fromLocal);

        var categories = new List<string>(timeline.Count);
        var timestamps = new List<string>(timeline.Count);
        for (int i = 0; i < timeline.Count; i++)
        {
            categories.Add(timeline[i].ToString(labelFormat));
            timestamps.Add(timeline[i].ToUniversalTime().ToString("o"));
        }

        var seriesNames = GetSeriesNamesOrDefault(template);
        var series = new List<EnergyUsageSeries>(seriesNames.Count);

        for (int s = 0; s < seriesNames.Count; s++)
        {
            var values = new List<float>(timeline.Count);
            for (int i = 0; i < timeline.Count; i++)
                values.Add(0f);

            series.Add(new EnergyUsageSeries
            {
                name = seriesNames[s],
                values = values
            });
        }

        return new EnergyUsageChartPayload
        {
            title = "Energy Usage Chart",
            subtitle = $"{fromLocal:yyyy-MM-dd} → {toLocal:yyyy-MM-dd} (Power, step {stepLabel}, no data yet)",
            unit = template != null && !string.IsNullOrWhiteSpace(template.unit) ? template.unit : "W",
            categories = categories,
            timestamps = timestamps,
            series = series
        };
    }

    private List<string> GetSeriesNamesOrDefault(EnergyUsageChartPayload template)
    {
        var names = new List<string>();

        if (template != null && template.series != null)
        {
            for (int i = 0; i < template.series.Count; i++)
            {
                var s = template.series[i];
                if (s == null || string.IsNullOrWhiteSpace(s.name)) continue;
                if (!names.Contains(s.name)) names.Add(s.name);
            }
        }

        if (names.Count == 0)
        {
            for (int i = 0; i < METER_IDS.Length; i++)
                names.Add(GetDefaultLabelForMeterId(METER_IDS[i]));
        }

        return names;
    }

    private List<DateTime> BuildAdaptiveTimeline(DateTime fromLocal, DateTime toLocal, out string labelFormat, out string stepLabel)
    {
        GetAdaptiveAxisPlan(fromLocal, toLocal, out int stepHours, out int stepDays, out int stepMonths, out labelFormat, out stepLabel);

        var ticks = new List<DateTime>();
        DateTime cursor = fromLocal;
        int guard = 0;

        while (cursor <= toLocal && guard < 2000)
        {
            ticks.Add(cursor);

            if (stepHours > 0) cursor = cursor.AddHours(stepHours);
            else if (stepDays > 0) cursor = cursor.AddDays(stepDays);
            else cursor = cursor.AddMonths(stepMonths);

            guard++;
        }

        if (ticks.Count == 0 || ticks[ticks.Count - 1] < toLocal)
            ticks.Add(toLocal);

        return ticks;
    }

    private void GetAdaptiveAxisPlan(
        DateTime fromLocal,
        DateTime toLocal,
        out int stepHours,
        out int stepDays,
        out int stepMonths,
        out string labelFormat,
        out string stepLabel)
    {
        double totalDays = Math.Max(1d / 24d, (toLocal - fromLocal).TotalDays);

        stepHours = 0;
        stepDays = 0;
        stepMonths = 0;

        if (totalDays <= 1.5d)
        {
            stepHours = 1;
            labelFormat = "HH:mm";
            stepLabel = "1h";
            return;
        }

        if (totalDays <= 3.5d)
        {
            stepHours = 12;
            labelFormat = "dd/MM HH:mm";
            stepLabel = "12h";
            return;
        }

        if (totalDays <= 45d)
        {
            stepDays = 1;
            labelFormat = "dd/MM";
            stepLabel = "1d";
            return;
        }

        if (totalDays <= 120d)
        {
            stepDays = 15;
            labelFormat = "dd/MM";
            stepLabel = "15d";
            return;
        }

        if (totalDays <= 365d)
        {
            stepMonths = 1;
            labelFormat = "MM/yyyy";
            stepLabel = "1mo";
            return;
        }

        stepMonths = 3;
        labelFormat = "MM/yyyy";
        stepLabel = "3mo";
    }

    private int GetAxisBucket(DateTime pointLocal, DateTime fromLocal, int stepHours, int stepDays, int stepMonths)
    {
        if (stepHours > 0)
            return Mathf.Max(0, (int)Math.Floor((pointLocal - fromLocal).TotalHours / stepHours));

        if (stepDays > 0)
            return Mathf.Max(0, (int)Math.Floor((pointLocal.Date - fromLocal.Date).TotalDays / stepDays));

        int months = (pointLocal.Year - fromLocal.Year) * 12 + (pointLocal.Month - fromLocal.Month);
        return Mathf.Max(0, months / Mathf.Max(1, stepMonths));
    }

    private bool IsNoDataError(string err)
    {
        if (string.IsNullOrWhiteSpace(err)) return false;
        return err.IndexOf("no data", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string NormalizeEveryForApi(string every)
    {
        if (string.IsNullOrWhiteSpace(every))
            return "1h";

        string normalized = every.Trim().ToLowerInvariant();

        if (normalized == "1m" || normalized == "1h" || normalized == "3h" || normalized == "1d" || normalized == "7d")
            return normalized;

        return "1h";
    }

    private string GetDefaultLabelForMeterId(string meterId)
    {
        switch (meterId)
        {
            case ID_JATO_AREIA: return LABEL_JATO_AREIA;
            case ID_PINTURA_DIREITA: return LABEL_PINTURA_DIREITA;
            case ID_PINTURA_ESQUERDA: return LABEL_PINTURA_ESQUERDA;
            case ID_QUADRO_PAINEL_SOLAR: return LABEL_QUADRO_PAINEL_SOLAR;
            case ID_COMPRESSOR: return LABEL_COMPRESSOR;
            case ID_CARREGADOR_PAREDE: return LABEL_CARREGADOR_PAREDE;
            case ID_BATE_CHAPAS_ESQ: return LABEL_BATE_CHAPAS_ESQ;
            case ID_BATE_CHAPAS_DIR: return LABEL_BATE_CHAPAS_DIR;
            case ID_AR_CONDICIONADO: return LABEL_AR_CONDICIONADO;
            case ID_SALA_CONVIVIO: return LABEL_SALA_CONVIVIO;
            case ID_TOMADAS_OFICINA: return LABEL_TOMADAS_OFICINA;
            case ID_PORTAO_ELETRICO: return LABEL_PORTAO_ELETRICO;
            default: return meterId;
        }
    }

    private PowerTimeseriesMeter GetPowerTimeseriesMeter(PowerTimeseriesMeters meters, string meterId)
    {
        if (meters == null) return null;

        switch (meterId)
        {
            case ID_JATO_AREIA: return meters.shelly3EMJatoAreia;
            case ID_PINTURA_DIREITA: return meters.shelly3EMPinturaDireita;
            case ID_PINTURA_ESQUERDA: return meters.shelly3EMPinturaEsquerda;
            case ID_QUADRO_PAINEL_SOLAR: return meters.shelly3EMQuadroPainelSolar;
            case ID_COMPRESSOR: return meters.shelly3EMCompressor;
            case ID_CARREGADOR_PAREDE: return meters.shelly3EMCarregadorParede;
            case ID_BATE_CHAPAS_ESQ: return meters.shelly3EMBateChapasEsquerda;
            case ID_BATE_CHAPAS_DIR: return meters.shelly3EMBateChapasDireita;
            case ID_AR_CONDICIONADO: return meters.shellyProEM50ArCondicionado;
            case ID_SALA_CONVIVIO: return meters.shellyProEM50SalaConvivio;
            case ID_TOMADAS_OFICINA: return meters.shellyProEM50TomadasOficina;
            case ID_PORTAO_ELETRICO: return meters.shellyProEM50PortaoEletrico;
            default: return null;
        }
    }

    private EnergyDayMeter GetEnergyDayMeter(EnergyDayMeters meters, string meterId)
    {
        if (meters == null) return null;

        switch (meterId)
        {
            case ID_JATO_AREIA: return meters.shelly3EMJatoAreia;
            case ID_PINTURA_DIREITA: return meters.shelly3EMPinturaDireita;
            case ID_PINTURA_ESQUERDA: return meters.shelly3EMPinturaEsquerda;
            case ID_QUADRO_PAINEL_SOLAR: return meters.shelly3EMQuadroPainelSolar;
            case ID_COMPRESSOR: return meters.shelly3EMCompressor;
            case ID_CARREGADOR_PAREDE: return meters.shelly3EMCarregadorParede;
            case ID_BATE_CHAPAS_ESQ: return meters.shelly3EMBateChapasEsquerda;
            case ID_BATE_CHAPAS_DIR: return meters.shelly3EMBateChapasDireita;
            case ID_AR_CONDICIONADO: return meters.shellyProEM50ArCondicionado;
            case ID_SALA_CONVIVIO: return meters.shellyProEM50SalaConvivio;
            case ID_TOMADAS_OFICINA: return meters.shellyProEM50TomadasOficina;
            case ID_PORTAO_ELETRICO: return meters.shellyProEM50PortaoEletrico;
            default: return null;
        }
    }
}