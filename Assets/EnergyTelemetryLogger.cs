using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

public class EnergyTelemetryLogger : MonoBehaviour
{
    [SerializeField] private EnergyPanelController energyPanelController;
    [SerializeField] private bool autoFindController = true;
    [SerializeField] private string outputFileName = "energy_unity_log.csv";
    [SerializeField] private bool appendIfExists = true;

    private string outputPath;

    private void Awake()
    {
        if (energyPanelController == null && autoFindController)
            energyPanelController = FindObjectOfType<EnergyPanelController>();

        outputPath = Path.Combine(Application.persistentDataPath, outputFileName);
        EnsureHeader();
    }

    private void OnEnable()
    {
        if (energyPanelController == null && autoFindController)
            energyPanelController = FindObjectOfType<EnergyPanelController>();

        if (energyPanelController != null)
            energyPanelController.OverviewUpdated += OnOverviewUpdated;
        else
            Debug.LogWarning("[EnergyTelemetryLogger] EnergyPanelController not found.");
    }

    private void OnDisable()
    {
        if (energyPanelController != null)
            energyPanelController.OverviewUpdated -= OnOverviewUpdated;
    }

    private void EnsureHeader()
    {
        if (appendIfExists && File.Exists(outputPath)) return;

        var header = "timestamp_utc,generated_at,total_power_w,total_month_kwh,right_power_w,left_power_w,sand_power_w,right_month_kwh,left_month_kwh,sand_month_kwh";
        WriteLineWithRetry(header, overwrite: true);
    }

    private void OnOverviewUpdated(EnergyPanelController.OverviewResponse data)
    {
        if (data == null || data.total == null)
            return;

        float rightPower = GetCurrentW(data, "shelly3EMPinturaDireita");
        float leftPower = GetCurrentW(data, "shelly3EMPinturaEsquerda");
        float sandPower = GetCurrentW(data, "shelly3EMJatoAreia");
        float rightMonth = GetMonthKwh(data, "shelly3EMPinturaDireita");
        float leftMonth = GetMonthKwh(data, "shelly3EMPinturaEsquerda");
        float sandMonth = GetMonthKwh(data, "shelly3EMJatoAreia");

        var line = string.Join(",",
            DateTime.UtcNow.ToString("O"),
            Escape(data.generated_at),
            ToInvariant(data.total.current_power_w),
            ToInvariant(data.total.month_energy_kwh),
            ToInvariant(rightPower),
            ToInvariant(leftPower),
            ToInvariant(sandPower),
            ToInvariant(rightMonth),
            ToInvariant(leftMonth),
            ToInvariant(sandMonth)
        );

        WriteLineWithRetry(line, overwrite: false);
    }

    private void WriteLineWithRetry(string line, bool overwrite)
    {
        const int maxAttempts = 5;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var mode = overwrite ? FileMode.Create : FileMode.Append;
                using (var stream = new FileStream(outputPath, mode, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.WriteLine(line);
                }
                return;
            }
            catch (IOException)
            {
                if (attempt == maxAttempts)
                {
                    Debug.LogWarning("[EnergyTelemetryLogger] Could not write to CSV after retries. Check if file is locked by another app.");
                    return;
                }

                Thread.Sleep(20 * attempt);
            }
        }
    }

    private static float GetCurrentW(EnergyPanelController.OverviewResponse data, string meterId)
    {
        if (data.meters == null) return 0f;
        for (int i = 0; i < data.meters.Length; i++)
        {
            var meter = data.meters[i];
            if (meter != null && meter.id == meterId)
                return Mathf.Max(0f, meter.current_power_w);
        }
        return 0f;
    }

    private static float GetMonthKwh(EnergyPanelController.OverviewResponse data, string meterId)
    {
        if (data.meters == null) return 0f;
        for (int i = 0; i < data.meters.Length; i++)
        {
            var meter = data.meters[i];
            if (meter != null && meter.id == meterId)
                return Mathf.Max(0f, meter.month_energy_kwh);
        }
        return 0f;
    }

    private static string ToInvariant(float value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace(",", " ");
    }

    [ContextMenu("Open Output Folder")]
    private void OpenOutputFolder()
    {
        var folder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(folder))
            Application.OpenURL("file:///" + folder.Replace("\\", "/"));
    }
}
