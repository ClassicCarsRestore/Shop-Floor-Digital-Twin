using System.Collections.Generic;
using E2C;
using UnityEngine;

public class HeatmapZoneChartBinder : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private HeatmapModeController heatmapController;

    [Header("Chart refs (no mesmo GO do chart)")]
    [SerializeField] private E2Chart chart;
    [SerializeField] private E2ChartData chartData;

    [Header("Display")]
    [SerializeField] private bool sortDescending = true;
    [SerializeField] private bool hideZeroZones = false;
    [SerializeField] private int maxZones = 0; // 0 = all

    private void OnEnable()
    {
        if (heatmapController == null)
            heatmapController = FindObjectOfType<HeatmapModeController>(true);

        if (heatmapController != null)
        {
            heatmapController.OnHeatmapChartUpdated += HandleHeatmapChartUpdated;

            if (heatmapController.TryGetLastChartPayload(out var payload))
                HandleHeatmapChartUpdated(payload);
        }
    }

    private void OnDisable()
    {
        if (heatmapController != null)
            heatmapController.OnHeatmapChartUpdated -= HandleHeatmapChartUpdated;
    }

    private void HandleHeatmapChartUpdated(HeatmapModeController.HeatmapChartPayload payload)
    {
        if (!IsReady() || payload == null)
            return;

        ApplyPayload(payload);
    }

    private bool IsReady()
    {
        if (chart == null || chartData == null)
        {
            Debug.LogWarning("[HeatmapZoneChartBinder] chart/chartData missing");
            return false;
        }
        return true;
    }

    private void ApplyPayload(HeatmapModeController.HeatmapChartPayload payload)
    {
        var points = new List<HeatmapModeController.HeatmapZonePoint>();

        if (payload.points != null)
        {
            for (int i = 0; i < payload.points.Count; i++)
            {
                var p = payload.points[i];
                if (p == null) continue;
                if (hideZeroZones && p.value <= 0f) continue;
                points.Add(p);
            }
        }

        if (sortDescending)
        {
            points.Sort((a, b) =>
            {
                int byValue = b.value.CompareTo(a.value);
                if (byValue != 0) return byValue;
                return string.CompareOrdinal(a.zoneName, b.zoneName);
            });
        }

        if (maxZones > 0 && points.Count > maxZones)
            points = points.GetRange(0, maxZones);

        chartData.title = payload.metric == HeatmapMetric.Frequency
            ? "Heatmap by Zone - Frequency"
            : "Heatmap by Zone - Occupation";

        chartData.subtitle = $"{payload.from:dd-MM-yyyy} a {payload.to:dd-MM-yyyy} | Total={payload.total:0.##}";

        if (chartData.categoriesX == null) chartData.categoriesX = new List<string>();
        chartData.categoriesX.Clear();

        if (chartData.series == null) chartData.series = new List<E2ChartData.Series>();
        chartData.series.Clear();

        var series = new E2ChartData.Series
        {
            name = payload.metric == HeatmapMetric.Frequency ? "Entries" : "Time (s)",
            show = true,
            dataName = new List<string>(points.Count),
            dataShow = new List<bool>(points.Count),
            dataY = new List<float>(points.Count),
            dataX = new List<float>(points.Count),
            dataZ = new List<float>(points.Count),
            dateTimeTick = new List<long>(points.Count),
            dateTimeString = new List<string>(points.Count),
        };

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            string label = string.IsNullOrWhiteSpace(p.zoneName) ? p.locationId : p.zoneName;

            chartData.categoriesX.Add(label);

            series.dataName.Add(label);
            series.dataShow.Add(true);
            series.dataY.Add(Mathf.Max(0f, p.value));
            series.dataX.Add(i);
            series.dataZ.Add(0f);
            series.dateTimeTick.Add(0);
            series.dateTimeString.Add("");
        }

        chartData.series.Add(series);
        chartData.hasChanged = true;
        chart.UpdateChart();
    }
}
