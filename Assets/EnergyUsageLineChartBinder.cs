using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using E2C;
using TMPro;

public class EnergyUsageLineChartBinder : MonoBehaviour
{
    public enum FilterMode
    {
        Today,
        ThisWeek,
        ThisMonth,
        CustomRange
    }

    [Header("Controller")]
    [SerializeField] private EnergyPanelController controller;

    [Header("Chart refs (no mesmo GO do chart)")]
    [SerializeField] private E2Chart chart;
    [SerializeField] private E2ChartData chartData;

    [Header("Filter")]
    [SerializeField] private FilterMode defaultFilter = FilterMode.Today;

    [Header("Date Range (From/To)")]
    [SerializeField] private GameObject datePickerPrefab;
    [SerializeField] private Button buttonFrom;
    [SerializeField] private Button buttonTo;
    [SerializeField] private TMP_Text fromLabel;
    [SerializeField] private TMP_Text toLabel;

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 60f;
    [SerializeField] private bool autoRefresh = true;

    [Header("Frontend cache (mínima)")]
    [SerializeField] private float todayCacheSeconds = 10f;
    [SerializeField] private float weekCacheSeconds = 30f;
    [SerializeField] private float monthCacheSeconds = 60f;

    private sealed class CachedPayload
    {
        public EnergyPanelController.EnergyUsageChartPayload payload;
        public float expireAt;
    }

    private readonly Dictionary<FilterMode, CachedPayload> cacheByFilter = new Dictionary<FilterMode, CachedPayload>();

    private Coroutine co;
    private FilterMode activeFilter;
    private DateTime? customFromDate;
    private DateTime? customToDate;
    private Canvas parentCanvas;
    private bool isOpeningPicker;

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();

        if (buttonFrom != null)
        {
            buttonFrom.onClick.RemoveListener(OnFromClicked);
            buttonFrom.onClick.AddListener(OnFromClicked);
        }

        if (buttonTo != null)
        {
            buttonTo.onClick.RemoveListener(OnToClicked);
            buttonTo.onClick.AddListener(OnToClicked);
        }
    }

    private void OnEnable()
    {
        if (controller == null) controller = GetComponentInParent<EnergyPanelController>();
        activeFilter = defaultFilter;

        if (!customFromDate.HasValue || !customToDate.HasValue)
        {
            customToDate = DateTime.Today;
            customFromDate = DateTime.Today.AddDays(-6);
        }

        RefreshDateLabels();
        RefreshOnce();

        if (autoRefresh)
            co = StartCoroutine(Loop());
    }

    private void OnDisable()
    {
        if (co != null) StopCoroutine(co);
        co = null;
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshSeconds);
            RefreshOnce();
        }
    }

    [ContextMenu("Refresh Once")]
    public void RefreshOnce()
    {
        if (!IsReady()) return;

        if (TryUseCache(activeFilter))
            return;

        switch (activeFilter)
        {
            case FilterMode.Today:
                controller.RequestTodayHourly(
                    data =>
                    {
                        var payload = BuildTodayPayload(data);
                        SetCache(FilterMode.Today, payload, todayCacheSeconds);
                        ApplyToLineChart(payload);
                    },
                    err => Debug.LogWarning("[EnergyUsageLineChartBinder] " + err)
                );
                break;

            case FilterMode.ThisWeek:
                controller.RequestWeekDaily(
                    data =>
                    {
                        var payload = BuildWeekPayload(data);
                        SetCache(FilterMode.ThisWeek, payload, weekCacheSeconds);
                        ApplyToLineChart(payload);
                    },
                    err => Debug.LogWarning("[EnergyUsageLineChartBinder] " + err)
                );
                break;

            case FilterMode.ThisMonth:
                controller.RequestMonthWeekly(
                    data =>
                    {
                        var payload = BuildMonthPayload(data);
                        SetCache(FilterMode.ThisMonth, payload, monthCacheSeconds);
                        ApplyToLineChart(payload);
                    },
                    err => Debug.LogWarning("[EnergyUsageLineChartBinder] " + err)
                );
                break;

            case FilterMode.CustomRange:
                RefreshCustomRange();
                break;
        }
    }

    public void OnTodayClicked()
    {
        activeFilter = FilterMode.Today;
        RefreshOnce();
    }

    public void OnThisWeekClicked()
    {
        activeFilter = FilterMode.ThisWeek;
        RefreshOnce();
    }

    public void OnThisMonthClicked()
    {
        activeFilter = FilterMode.ThisMonth;
        RefreshOnce();
    }

    public void OnFromClicked()
    {
        OpenDatePicker(true);
    }

    public void OnToClicked()
    {
        OpenDatePicker(false);
    }

    private void OpenDatePicker(bool isFrom)
    {
        if (isOpeningPicker || datePickerPrefab == null || parentCanvas == null)
            return;

        isOpeningPicker = true;

        GameObject pickerGO = Instantiate(datePickerPrefab, parentCanvas.transform);
        var picker = pickerGO.GetComponent<SimpleDatePicker>();
        if (picker == null)
        {
            Debug.LogError("[EnergyUsageLineChartBinder] SimpleDatePicker não encontrado no prefab.");
            Destroy(pickerGO);
            isOpeningPicker = false;
            return;
        }

        DateTime seedDate = isFrom
            ? (customFromDate ?? customToDate ?? DateTime.Today)
            : (customToDate ?? customFromDate ?? DateTime.Today);

        picker.Initialize(seedDate);

        picker.OnDateSelected += date =>
        {
            if (isFrom) customFromDate = date.Date;
            else customToDate = date.Date;

            NormalizeDateRange();
            RefreshDateLabels();

            activeFilter = FilterMode.CustomRange;
            RefreshOnce();

            Destroy(pickerGO);
            isOpeningPicker = false;
        };

        picker.OnCancel += () =>
        {
            Destroy(pickerGO);
            isOpeningPicker = false;
        };
    }

    private void RefreshCustomRange()
    {
        if (!customFromDate.HasValue || !customToDate.HasValue)
        {
            Debug.LogWarning("[EnergyUsageLineChartBinder] Range inválido: from/to não definido.");
            return;
        }

        NormalizeDateRange();

        controller.RequestPowerTimeseriesBetween(
            customFromDate.Value,
            customToDate.Value,
            payload =>
            {
                if (payload != null)
                {
                    payload.title = "Energy Usage Chart";
                    payload.subtitle = $"{customFromDate.Value:yyyy-MM-dd} → {customToDate.Value:yyyy-MM-dd}";
                }
                ApplyToLineChart(payload);
            },
            err => Debug.LogWarning("[EnergyUsageLineChartBinder] " + err)
        );
    }

    private void NormalizeDateRange()
    {
        if (!customFromDate.HasValue || !customToDate.HasValue)
            return;

        if (customFromDate.Value.Date > customToDate.Value.Date)
        {
            DateTime tmp = customFromDate.Value;
            customFromDate = customToDate.Value;
            customToDate = tmp;
        }
    }

    private void RefreshDateLabels()
    {
        if (fromLabel != null && customFromDate.HasValue)
            fromLabel.text = customFromDate.Value.ToString("yyyy-MM-dd");

        if (toLabel != null && customToDate.HasValue)
            toLabel.text = customToDate.Value.ToString("yyyy-MM-dd");
    }

    private bool IsReady()
    {
        if (controller == null) { Debug.LogWarning("[EnergyUsageLineChartBinder] controller missing"); return false; }
        if (chart == null || chartData == null) { Debug.LogWarning("[EnergyUsageLineChartBinder] chart/chartData missing"); return false; }
        return true;
    }

    private bool TryUseCache(FilterMode mode)
    {
        if (!cacheByFilter.TryGetValue(mode, out var cached) || cached == null || cached.payload == null)
            return false;

        if (Time.unscaledTime > cached.expireAt)
            return false;

        ApplyToLineChart(cached.payload);
        return true;
    }

    private void SetCache(FilterMode mode, EnergyPanelController.EnergyUsageChartPayload payload, float ttlSeconds)
    {
        if (payload == null) return;

        cacheByFilter[mode] = new CachedPayload
        {
            payload = payload,
            expireAt = Time.unscaledTime + Mathf.Max(1f, ttlSeconds)
        };
    }

    private EnergyPanelController.EnergyUsageChartPayload BuildTodayPayload(EnergyPanelController.TodayHourlyResponse data)
    {
        if (data == null || data.series == null) return null;

        DateTime now = DateTime.Now;
        int currentHour = now.Hour;

        var categories = new List<string>(24);
        for (int i = 0; i < 24; i++)
        {
            int h = (currentHour - 23 + i + 24) % 24;
            categories.Add($"{h:00}:00");
        }

        var outSeries = new List<EnergyPanelController.EnergyUsageSeries>(data.series.Length);

        for (int s = 0; s < data.series.Length; s++)
        {
            var src = data.series[s];
            if (src == null) continue;

            var byHour = new Dictionary<int, float>();
            if (src.hourly != null)
            {
                for (int p = 0; p < src.hourly.Length; p++)
                {
                    var pt = src.hourly[p];
                    if (pt == null) continue;
                    byHour[pt.hour] = Mathf.Max(0f, pt.kwh);
                }
            }

            var values = new List<float>(24);
            for (int i = 0; i < 24; i++)
            {
                int h = (currentHour - 23 + i + 24) % 24;
                values.Add(byHour.TryGetValue(h, out var v) ? v : 0f);
            }

            outSeries.Add(new EnergyPanelController.EnergyUsageSeries
            {
                name = string.IsNullOrWhiteSpace(src.label) ? src.meter_id : src.label,
                values = values
            });
        }

        return new EnergyPanelController.EnergyUsageChartPayload
        {
            title = "Energy Usage Chart - Today",
            subtitle = "Current day (hourly)",
            unit = string.IsNullOrWhiteSpace(data.unit) ? "kWh" : data.unit,
            categories = categories,
            timestamps = null,
            series = outSeries
        };
    }

    private EnergyPanelController.EnergyUsageChartPayload BuildWeekPayload(EnergyPanelController.WeekDailyResponse data)
    {
        if (data == null || data.series == null) return null;

        var categories = new List<string>(7);
        var axisDates = new List<DateTime>(7);

        DateTime today = DateTime.Today;
        for (int i = 0; i < 7; i++)
        {
            DateTime d = today.AddDays(-6 + i).Date;
            axisDates.Add(d);
            categories.Add(d.ToString("dd/MM"));
        }

        var outSeries = new List<EnergyPanelController.EnergyUsageSeries>(data.series.Length);

        for (int s = 0; s < data.series.Length; s++)
        {
            var src = data.series[s];
            if (src == null) continue;

            var byDate = new Dictionary<DateTime, float>();
            if (src.daily != null)
            {
                for (int p = 0; p < src.daily.Length; p++)
                {
                    var pt = src.daily[p];
                    if (pt == null || string.IsNullOrWhiteSpace(pt.date)) continue;

                    if (DateTime.TryParseExact(pt.date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        byDate[d.Date] = Mathf.Max(0f, pt.kwh);
                }
            }

            var values = new List<float>(7);
            for (int i = 0; i < axisDates.Count; i++)
                values.Add(byDate.TryGetValue(axisDates[i], out var v) ? v : 0f);

            outSeries.Add(new EnergyPanelController.EnergyUsageSeries
            {
                name = string.IsNullOrWhiteSpace(src.label) ? src.meter_id : src.label,
                values = values
            });
        }

        return new EnergyPanelController.EnergyUsageChartPayload
        {
            title = "Energy Usage Chart - This Week",
            subtitle = "Today + previous 6 days",
            unit = string.IsNullOrWhiteSpace(data.unit) ? "kWh" : data.unit,
            categories = categories,
            timestamps = null,
            series = outSeries
        };
    }

    private EnergyPanelController.EnergyUsageChartPayload BuildMonthPayload(EnergyPanelController.MonthWeeklyResponse data)
    {
        if (data == null || data.series == null) return null;

        // Usar diretamente as semanas devolvidas pelo backend.
        // Ordenação: mais antiga -> mais recente (esquerda -> direita).
        var allWeekStarts = new SortedSet<DateTime>();
        for (int s = 0; s < data.series.Length; s++)
        {
            var src = data.series[s];
            if (src == null || src.weekly == null) continue;

            for (int p = 0; p < src.weekly.Length; p++)
            {
                var pt = src.weekly[p];
                if (pt == null || string.IsNullOrWhiteSpace(pt.week_start)) continue;

                if (DateTime.TryParseExact(pt.week_start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    allWeekStarts.Add(d.Date);
            }
        }

        var axisWeeks = new List<DateTime>(allWeekStarts);
        if (axisWeeks.Count > 4)
            axisWeeks = axisWeeks.GetRange(axisWeeks.Count - 4, 4);

        var categories = new List<string>(axisWeeks.Count);
        for (int i = 0; i < axisWeeks.Count; i++)
            categories.Add(axisWeeks[i].ToString("dd/MM"));

        var outSeries = new List<EnergyPanelController.EnergyUsageSeries>(data.series.Length);

        for (int s = 0; s < data.series.Length; s++)
        {
            var src = data.series[s];
            if (src == null) continue;

            var byWeekStart = new Dictionary<DateTime, float>();
            if (src.weekly != null)
            {
                for (int p = 0; p < src.weekly.Length; p++)
                {
                    var pt = src.weekly[p];
                    if (pt == null || string.IsNullOrWhiteSpace(pt.week_start)) continue;

                    if (DateTime.TryParseExact(pt.week_start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        byWeekStart[d.Date] = Mathf.Max(0f, pt.kwh);
                }
            }

            var values = new List<float>(axisWeeks.Count);
            for (int i = 0; i < axisWeeks.Count; i++)
                values.Add(byWeekStart.TryGetValue(axisWeeks[i], out var v) ? v : 0f);

            outSeries.Add(new EnergyPanelController.EnergyUsageSeries
            {
                name = string.IsNullOrWhiteSpace(src.label) ? src.meter_id : src.label,
                values = values
            });
        }

        return new EnergyPanelController.EnergyUsageChartPayload
        {
            title = "Energy Usage Chart - This Month",
            subtitle = "Last 4 weeks",
            unit = string.IsNullOrWhiteSpace(data.unit) ? "kWh" : data.unit,
            categories = categories,
            timestamps = null,
            series = outSeries
        };
    }

    private void ApplyToLineChart(EnergyPanelController.EnergyUsageChartPayload payload)
    {
        if (payload == null || payload.categories == null || payload.series == null) return;
        if (payload.categories.Count == 0 || payload.series.Count == 0) return;

        chartData.title = payload.title;
        chartData.subtitle = payload.subtitle;

        if (chartData.categoriesX == null) chartData.categoriesX = new List<string>();
        chartData.categoriesX.Clear();
        chartData.categoriesX.AddRange(payload.categories);

        if (chartData.series == null) chartData.series = new List<E2ChartData.Series>();
        chartData.series.Clear();

        int n = payload.categories.Count;

        for (int i = 0; i < payload.series.Count; i++)
        {
            var src = payload.series[i];
            if (src == null || src.values == null) continue;

            var s = new E2ChartData.Series
            {
                name = string.IsNullOrWhiteSpace(src.name) ? $"Series {i + 1}" : src.name,
                show = true,
                dataName = new List<string>(n),
                dataShow = new List<bool>(n),
                dataY = new List<float>(n),
                dataX = new List<float>(n),
                dataZ = new List<float>(n),
                dateTimeTick = new List<long>(n),
                dateTimeString = new List<string>(n),
            };

            for (int k = 0; k < n; k++)
            {
                float value = k < src.values.Count ? Mathf.Max(0f, src.values[k]) : 0f;

                s.dataName.Add(payload.categories[k]);
                s.dataShow.Add(true);
                s.dataY.Add(value);
                s.dataX.Add(k);
                s.dataZ.Add(0f);
                s.dateTimeTick.Add(0);
                s.dateTimeString.Add("");
            }

            chartData.series.Add(s);
        }

        chartData.hasChanged = true;
        chart.UpdateChart();
    }
}
