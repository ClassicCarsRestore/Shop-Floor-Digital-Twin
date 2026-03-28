using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using E2C;

public class EnergyDonutMonthBinder : MonoBehaviour
{
    [Header("Controller (no GO Energy_Panel)")]
    [SerializeField] private EnergyPanelController controller;

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 60f;
    [SerializeField] private bool autoRefresh = true;

    [Header("E2Chart refs (no mesmo GO DonutMonth)")]
    [SerializeField] private E2Chart chart;
    [SerializeField] private E2ChartData chartData;

    private Coroutine co;

    private void OnEnable()
    {
        if (controller == null)
            controller = GetComponentInParent<EnergyPanelController>(); // tenta apanhar do Energy_Panel

        if (autoRefresh)
            co = StartCoroutine(Loop());
        else
            RefreshOnce();
    }

    private void OnDisable()
    {
        if (co != null) StopCoroutine(co);
        co = null;
    }

    [ContextMenu("Refresh Once")]
    public void RefreshOnce()
    {
        if (!IsReady()) return;

        controller.RequestBreakdownMonth(
            onSuccess: ApplyToChart,
            onError: err => Debug.LogWarning($"[EnergyDonutMonthBinder] {err}")
        );
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            RefreshOnce();
            yield return new WaitForSeconds(refreshSeconds);
        }
    }

    private bool IsReady()
    {
        if (controller == null)
        {
            Debug.LogWarning("[EnergyDonutMonthBinder] controller não atribuído.");
            return false;
        }
        if (chart == null || chartData == null)
        {
            Debug.LogWarning("[EnergyDonutMonthBinder] chart/chartData não atribuídos.");
            return false;
        }
        return true;
    }

    private void ApplyToChart(EnergyPanelController.BreakdownMonthResponse data)
    {
        if (data == null || data.labels == null || data.values == null) return;

        int n = Mathf.Min(data.labels.Count, data.values.Count);
        if (n <= 0) return;

        chartData.title = $"Consumption By Equipment ({data.period})";
        chartData.subtitle = $"Total: {data.total_kwh:0.###} kWh";

        if (chartData.series == null) chartData.series = new List<E2ChartData.Series>();
        chartData.series.Clear();

        var s = new E2ChartData.Series
        {
            name = "kWh",
            show = true,
            dataName = new List<string>(n),
            dataShow = new List<bool>(n),
            dataY = new List<float>(n),
            dataX = new List<float>(n),
            dataZ = new List<float>(n),
            dateTimeTick = new List<long>(n),
            dateTimeString = new List<string>(n),
        };

        for (int i = 0; i < n; i++)
        {
            s.dataName.Add(data.labels[i]);
            s.dataShow.Add(true);
            s.dataY.Add(Mathf.Max(0f, data.values[i]));
            s.dataX.Add(0f);
            s.dataZ.Add(0f);
            s.dateTimeTick.Add(0);
            s.dateTimeString.Add("");
        }

        chartData.series.Add(s);

        chartData.hasChanged = true;
        chart.UpdateChart();
    }
}
