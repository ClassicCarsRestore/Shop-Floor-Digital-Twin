using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using E2C;

public class EnergyDonutMonthBinder : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string apiBaseUrl = "https://sensors.raimundobranco.com";
    [SerializeField] private float refreshSeconds = 60f;
    [SerializeField] private bool autoRefresh = true;

    [Header("E2Chart refs (no mesmo GO DonutMonth)")]
    [SerializeField] private E2Chart chart;
    [SerializeField] private E2ChartData chartData;

    // Se precisares de auth mais tarde
    [Header("Auth (opcional)")]
    [SerializeField] private bool useAuth = false;
    [SerializeField] private string bearerToken = "";

    private Coroutine co;

    [Serializable]
    private class BreakdownMonthResponse
    {
        public string period;
        public string unit;
        public List<string> labels;
        public List<float> values;
        public float total_kwh;
    }

    private void OnEnable()
    {
        if (autoRefresh)
            co = StartCoroutine(Loop());
        else
            StartCoroutine(FetchAndApply());
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
            yield return FetchAndApply();
            yield return new WaitForSeconds(refreshSeconds);
        }
    }

    private IEnumerator FetchAndApply()
    {
        if (chart == null || chartData == null)
        {
            Debug.LogWarning("[EnergyDonutMonthBinder] chart/chartData não estão atribuídos no Inspector.");
            yield break;
        }

        string url = apiBaseUrl.TrimEnd('/') + "/energy/dashboard/breakdown/month";

        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;

            if (useAuth && !string.IsNullOrWhiteSpace(bearerToken))
                req.SetRequestHeader("Authorization", "Bearer " + bearerToken);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[EnergyDonutMonthBinder] API error: {req.responseCode} {req.error} ({url})");
                yield break;
            }

            var json = req.downloadHandler.text;

            BreakdownMonthResponse data = null;
            try
            {
                data = JsonUtility.FromJson<BreakdownMonthResponse>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnergyDonutMonthBinder] JSON parse failed: {e.Message}\n{json}");
                yield break;
            }

            if (data == null || data.labels == null || data.values == null)
                yield break;

            ApplyToChart(data);
        }
    }

    private void ApplyToChart(BreakdownMonthResponse data)
    {
        // Garantias mínimas
        int n = Mathf.Min(data.labels.Count, data.values.Count);
        if (n <= 0) return;

        // Title/subtitle
        chartData.title = $"Energy Consumption By Equipment ({data.period})";
        chartData.subtitle = $"Total: {data.total_kwh:0.###} kWh";

        // E2ChartData precisa de pelo menos 1 série para Pie/Donut
        if (chartData.series == null) chartData.series = new List<E2ChartData.Series>();
        chartData.series.Clear();

        var s = new E2ChartData.Series();
        s.name = "kWh";
        s.show = true;

        s.dataName = new List<string>(n);
        s.dataShow = new List<bool>(n);
        s.dataY = new List<float>(n);

        // listas que podem existir noutros chart types (não usadas aqui)
        s.dataX = new List<float>(n);
        s.dataZ = new List<float>(n);
        s.dateTimeTick = new List<long>(n);
        s.dateTimeString = new List<string>(n);

        for (int i = 0; i < n; i++)
        {
            s.dataName.Add(data.labels[i]);
            s.dataShow.Add(true);
            s.dataY.Add(Mathf.Max(0f, data.values[i])); // kWh
            s.dataX.Add(0f);
            s.dataZ.Add(0f);
            s.dateTimeTick.Add(0);
            s.dateTimeString.Add("");
        }

        chartData.series.Add(s);

        // Marca “changed” e rebuild
        chartData.hasChanged = true;
        chart.UpdateChart();
    }
}
