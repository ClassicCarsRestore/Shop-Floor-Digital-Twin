using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using E2C;

public class EnergyTrendMonthlyBarBinder : MonoBehaviour
{
    [Header("Controller")]
    [SerializeField] private EnergyPanelController controller;

    [Header("Chart refs (no mesmo GO do chart)")]
    [SerializeField] private E2Chart chart;
    [SerializeField] private E2ChartData chartData;

    [Header("Config")]
    [SerializeField] private int lastMonths = 5;

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 60f;
    [SerializeField] private bool autoRefresh = true;

    private Coroutine co;

    private void OnEnable()
    {
        if (controller == null) controller = GetComponentInParent<EnergyPanelController>();
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

        controller.RequestMonthlyTrendLastMonths(
            lastMonths,
            payload => ApplyToBarChart(payload),
            err => Debug.LogWarning("[EnergyTrendMonthlyBarBinder] " + err)
        );
    }

    private bool IsReady()
    {
        if (controller == null) { Debug.LogWarning("[EnergyTrendMonthlyBarBinder] controller missing"); return false; }
        if (chart == null || chartData == null) { Debug.LogWarning("[EnergyTrendMonthlyBarBinder] chart/chartData missing"); return false; }
        return true;
    }

    private void ApplyToBarChart(EnergyPanelController.EnergyTrendPayload p)
    {
        if (p == null || p.categories == null || p.values == null) return;

        int n = Mathf.Min(p.categories.Count, p.values.Count);
        if (n <= 0) return;

        chartData.title = p.title;
        chartData.subtitle = p.subtitle;

        // X categories (labels dos meses)
        if (chartData.categoriesX == null) chartData.categoriesX = new List<string>();
        chartData.categoriesX.Clear();
        for (int i = 0; i < n; i++) chartData.categoriesX.Add(p.categories[i]);

        // 1 série para bar chart (kWh)
        if (chartData.series == null) chartData.series = new List<E2ChartData.Series>();
        chartData.series.Clear();

        var s = new E2ChartData.Series
        {
            name = p.unit ?? "kWh",
            show = true,
            dataName = new List<string>(n),
            dataShow = new List<bool>(n),
            dataY = new List<float>(n),

            // listas não usadas aqui mas o E2 pede existirem
            dataX = new List<float>(n),
            dataZ = new List<float>(n),
            dateTimeTick = new List<long>(n),
            dateTimeString = new List<string>(n),
        };

        for (int i = 0; i < n; i++)
        {
            s.dataName.Add(p.categories[i]);
            s.dataShow.Add(true);
            s.dataY.Add(Mathf.Max(0f, p.values[i])); // kWh
            s.dataX.Add(i);
            s.dataZ.Add(0f);
            s.dateTimeTick.Add(0);
            s.dateTimeString.Add("");
        }

        chartData.series.Add(s);

        chartData.hasChanged = true;
        chart.UpdateChart();
    }
}
