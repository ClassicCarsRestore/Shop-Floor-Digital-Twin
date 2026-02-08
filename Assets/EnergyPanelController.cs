using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking; 
using TMPro;

public class EnergyPanelController : MonoBehaviour
{
    [Header("API")]
     private string apiBaseUrl = "https://sensors.raimundobranco.com"; // muda isto
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

    // ---------- API DTOs ----------
    [Serializable]
    private class OverviewResponse
    {
        public string generated_at;
        public TotalBlock total;
        public MeterBlock[] meters;
    }

    [Serializable]
    private class TotalBlock
    {
        public float current_power_w;
        public float month_energy_kwh;
    }

    [Serializable]
    private class MeterBlock
    {
        public string id;
        public string label;
        public float current_power_w;
        public string current_power_time;
        public float month_energy_kwh;
        public bool is_running;
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
        StartCoroutine(FetchAndApply());
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return FetchAndApply();
            yield return new WaitForSeconds(refreshSeconds);
        }
    }

    private IEnumerator FetchAndApply()
    {
        string url = apiBaseUrl.TrimEnd('/') + "/energy/dashboard/overview";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10; // segundos

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[EnergyPanelController] API error: {req.error} ({url})");
                ApplyErrorState();
                yield break;
            }

            string json = req.downloadHandler.text;

            OverviewResponse data;
            try
            {
                // JsonUtility precisa que o JSON tenha campos simples (o teu tem)
                data = JsonUtility.FromJson<OverviewResponse>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnergyPanelController] JSON parse failed: {e.Message}\n{json}");
                ApplyErrorState();
                yield break;
            }

            if (data == null)
            {
                ApplyErrorState();
                yield break;
            }

            ApplyToUI(data);
        }
    }

    private void ApplyToUI(OverviewResponse data)
    {
        // Totais
        if (currentEnergyPanel != null)
            currentEnergyPanel.Set("Current Energy Consumption", FormatW(data.total.current_power_w));

        if (monthEnergyPanel != null)
            monthEnergyPanel.Set("This Month Energy Consumption", FormatKwh(data.total.month_energy_kwh));

        // Index por id
        Dictionary<string, MeterBlock> byId = new Dictionary<string, MeterBlock>();
        if (data.meters != null)
        {
            foreach (var m in data.meters)
            {
                if (m != null && !string.IsNullOrEmpty(m.id))
                    byId[m.id] = m;
            }
        }

        // Current (W)
        if (currentRightBoothPanel != null)
            currentRightBoothPanel.Set("Current Right Paint Booth Usage", FormatW(GetCurrentW(byId, ID_RIGHT)));

        if (currentLeftBoothPanel != null)
            currentLeftBoothPanel.Set("Current Left Paint Booth Usage", FormatW(GetCurrentW(byId, ID_LEFT)));

        if (currentSandBlastPanel != null)
            currentSandBlastPanel.Set("Current Sand Blasting Usage", FormatW(GetCurrentW(byId, ID_SAND)));

        // Month (kWh)
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
        // Podes pôr "--" para indicar falha
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

    private string FormatW(float w)
    {
        return $"{w:0.##} W";
    }

    private string FormatKwh(float kwh)
    {
        return $"{kwh:0.###} kWh";
    }
}
