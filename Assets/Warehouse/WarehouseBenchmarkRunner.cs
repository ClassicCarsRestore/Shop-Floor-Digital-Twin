using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public class WarehouseBenchmarkRunner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WarehouseLayoutController layoutController;
    [SerializeField] private StorageRepository storageRepository;
    [SerializeField] private WarehouseManager warehouseManager;

    [Header("Execution")]
    [SerializeField] private bool autoRunOnStart;
    [SerializeField] private KeyCode runSuiteKey = KeyCode.F7;
    [SerializeField] private KeyCode runSuiteSecondaryKey = KeyCode.B;
    [SerializeField] private bool enforceModifierRequirements = false;
    [SerializeField] private bool requireCtrl = false;
    [SerializeField] private bool requireShift = false;
    [SerializeField] private bool requireAlt = false;
    [SerializeField] private bool logHotkeyDebug = true;
    [SerializeField] private float delayBetweenIterationsSeconds = 0.5f;

    [Header("Editor URL Overrides (optional)")]
    [SerializeField] private bool useEditorUrlOverrides = true;
    [SerializeField] private string editorAllStorageUrlOverride = string.Empty;
    [SerializeField] private string editorItemsByCharterUrlTemplateOverride = string.Empty;

    [Header("Load + Visual")]
    [SerializeField] private int warmupIterations = 2;
    [SerializeField] private int loadIterations = 20;

    [Header("Highlight")]
    [SerializeField] private int highlightIterations = 20;
    [SerializeField] private int maxDistinctCarsForHighlight = 6;
    [SerializeField] private bool runPinnedCarChecks = true;
    [SerializeField] private string pinnedCarIdWithBoxes = "66d9b4207e1464d013f6c0ff";
    [SerializeField] private string pinnedCarIdWithoutBoxes = "66e156727e1464d013f6c11b";

    [Header("Error/Fallback")]
    [SerializeField] private bool runForcedErrorScenario = true;
    [SerializeField] private int forcedErrorIterations = 10;
    [SerializeField] private string forcedErrorAllStorageUrl = "/inventory/items/__benchmark_error__";

    private const string LastReportPlayerPrefsKey = "warehouse.benchmark.lastReport.v1";
    private const int ResultsLogChunkSize = 3500;

    private bool isRunning;
    private string lastReportJson;

    public bool IsRunning => isRunning;
    public string LastReportJson => lastReportJson;

    private void Start()
    {
        ApplyEditorOverridesIfNeeded();

        if (logHotkeyDebug)
        {
            Debug.Log($"[WarehouseBenchmark] Hotkeys ativos: primary={runSuiteKey}, secondary={runSuiteSecondaryKey}, requireCtrl={requireCtrl}, requireShift={requireShift}, requireAlt={requireAlt}");
        }

        if (autoRunOnStart)
            RunStandardSuite();
    }

    private void ApplyEditorOverridesIfNeeded()
    {
        if (!Application.isEditor || !useEditorUrlOverrides || storageRepository == null)
            return;

        bool hasAll = !string.IsNullOrWhiteSpace(editorAllStorageUrlOverride);
        bool hasByCar = !string.IsNullOrWhiteSpace(editorItemsByCharterUrlTemplateOverride);

        if (!hasAll && !hasByCar)
            return;

        storageRepository.SetRequestUrlOverrides(editorAllStorageUrlOverride, editorItemsByCharterUrlTemplateOverride);
        Debug.Log($"[WarehouseBenchmark] Editor URL overrides aplicados. all='{editorAllStorageUrlOverride}', byCarTemplate='{editorItemsByCharterUrlTemplateOverride}'");
    }

    private void Update()
    {
        bool hotkeyPressed = Input.GetKeyDown(runSuiteKey) || Input.GetKeyDown(runSuiteSecondaryKey);
        if (!hotkeyPressed)
            return;

        bool modifiersOk = !enforceModifierRequirements || AreRequiredModifiersPressed();
        if (logHotkeyDebug)
            Debug.Log($"[WarehouseBenchmark] Hotkey detetado. modifiersOk={modifiersOk} isRunning={isRunning}");

        if (!isRunning && modifiersOk)
            RunStandardSuite();
    }

    private bool AreRequiredModifiersPressed()
    {
        bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool altPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        if (requireCtrl && !ctrlPressed) return false;
        if (requireShift && !shiftPressed) return false;
        if (requireAlt && !altPressed) return false;

        return true;
    }

    [ContextMenu("Run Warehouse Benchmark Suite")]
    public void RunStandardSuite()
    {
        if (isRunning)
        {
            Debug.LogWarning("[WarehouseBenchmark] Suite já em execução.");
            return;
        }

        if (layoutController == null || storageRepository == null || warehouseManager == null)
        {
            Debug.LogWarning("[WarehouseBenchmark] Referências em falta (layoutController/storageRepository/warehouseManager).");
            return;
        }

        StartCoroutine(RunSuiteCoroutine());
    }

    private IEnumerator RunSuiteCoroutine()
    {
        isRunning = true;

        var report = new WarehouseBenchmarkReport
        {
            runId = Guid.NewGuid().ToString("N"),
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            platform = Application.platform.ToString(),
            unityVersion = Application.unityVersion,
            warmupIterations = Mathf.Max(0, warmupIterations),
            loadIterations = Mathf.Max(1, loadIterations),
            highlightIterations = Mathf.Max(0, highlightIterations),
            forcedErrorIterations = runForcedErrorScenario ? Mathf.Max(0, forcedErrorIterations) : 0,
            loadSamples = new List<LoadSample>(),
            highlightSamples = new List<HighlightSample>(),
            errorSamples = new List<LoadSample>()
        };

        Debug.Log($"[WarehouseBenchmark] START runId={report.runId}");

        for (int i = 0; i < report.warmupIterations; i++)
        {
            yield return RunLoadSample(report.loadSamples, "warmup", i + 1);
            if (delayBetweenIterationsSeconds > 0f)
                yield return new WaitForSecondsRealtime(delayBetweenIterationsSeconds);
        }

        for (int i = 0; i < report.loadIterations; i++)
        {
            yield return RunLoadSample(report.loadSamples, "normal", i + 1);
            if (delayBetweenIterationsSeconds > 0f)
                yield return new WaitForSecondsRealtime(delayBetweenIterationsSeconds);
        }

        if (report.highlightIterations > 0)
            yield return RunHighlightSamples(report);

        if (runForcedErrorScenario && report.forcedErrorIterations > 0)
        {
            Debug.Log("[WarehouseBenchmark] Running forced-error scenario...");
            storageRepository.SetRequestUrlOverrides(forcedErrorAllStorageUrl, string.Empty);

            for (int i = 0; i < report.forcedErrorIterations; i++)
            {
                yield return RunLoadSample(report.errorSamples, "forced_error", i + 1);
                if (delayBetweenIterationsSeconds > 0f)
                    yield return new WaitForSecondsRealtime(delayBetweenIterationsSeconds);
            }

            storageRepository.ClearRequestUrlOverrides();
        }

        report.summary = BuildSummary(report);
        lastReportJson = JsonConvert.SerializeObject(report, Formatting.Indented);

        PlayerPrefs.SetString(LastReportPlayerPrefsKey, lastReportJson);
        PlayerPrefs.Save();

        LogResultsJsonChunked(lastReportJson);
        Debug.Log($"[WarehouseBenchmark] END runId={report.runId}");

        isRunning = false;
    }

    private void LogResultsJsonChunked(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.Log("[WarehouseBenchmark][RESULTS_JSON] <empty>");
            return;
        }

        int totalParts = Mathf.CeilToInt((float)json.Length / ResultsLogChunkSize);
        Debug.Log($"[WarehouseBenchmark][RESULTS_JSON][START] length={json.Length} parts={totalParts}");

        for (int i = 0; i < totalParts; i++)
        {
            int start = i * ResultsLogChunkSize;
            int len = Mathf.Min(ResultsLogChunkSize, json.Length - start);
            string chunk = json.Substring(start, len);
            Debug.Log($"[WarehouseBenchmark][RESULTS_JSON][PART {i + 1}/{totalParts}]\n{chunk}");
        }

        Debug.Log("[WarehouseBenchmark][RESULTS_JSON][END]");
    }

    private IEnumerator RunLoadSample(List<LoadSample> target, string scenario, int iteration)
    {
        var sample = new LoadSample
        {
            scenario = scenario,
            iteration = iteration,
            startedAtUtc = DateTime.UtcNow.ToString("o")
        };

        double t0 = Time.realtimeSinceStartupAsDouble;

        bool layoutDone = false;
        layoutController.LoadLayoutAndThen(() => { layoutDone = true; });

        while (!layoutDone)
            yield return null;

        double tLayoutDone = Time.realtimeSinceStartupAsDouble;

        List<StorageRowDTO> rows = null;
        string storageError = null;

        double tStorageStart = Time.realtimeSinceStartupAsDouble;
        yield return StartCoroutine(storageRepository.GetAllStorage(
            onSuccess: result => rows = result ?? new List<StorageRowDTO>(),
            onError: err => storageError = err
        ));
        double tStorageDone = Time.realtimeSinceStartupAsDouble;

        double tRenderStart = Time.realtimeSinceStartupAsDouble;
        if (rows != null)
        {
            warehouseManager.ShowAllStorage(rows);
            yield return new WaitForEndOfFrame();
        }
        double tRenderDone = Time.realtimeSinceStartupAsDouble;

        sample.layoutLoadMs = ToMs(tLayoutDone - t0);
        sample.dataFetchMs = ToMs(tStorageDone - tStorageStart);
        sample.visualApplyMs = rows != null ? ToMs(tRenderDone - tRenderStart) : 0d;
        sample.totalMs = ToMs(tRenderDone - t0);
        sample.rowsCount = rows?.Count ?? 0;
        sample.success = rows != null;
        sample.hadError = !string.IsNullOrWhiteSpace(storageError) || storageRepository.LastGetAllHadError;
        sample.usedCacheFallback = storageRepository.LastGetAllUsedCacheFallback;
        sample.errorMessage = storageError;

        target.Add(sample);

        Debug.Log($"[WarehouseBenchmark][LOAD] scenario={sample.scenario} iter={sample.iteration} totalMs={sample.totalMs:F2} rows={sample.rowsCount} success={sample.success} hadError={sample.hadError} cacheFallback={sample.usedCacheFallback}");
    } 

    private IEnumerator RunHighlightSamples(WarehouseBenchmarkReport report)
    {
        List<StorageRowDTO> allRows = null;
        string allRowsError = null;

        yield return StartCoroutine(storageRepository.GetAllStorage(
            onSuccess: rows => allRows = rows ?? new List<StorageRowDTO>(),
            onError: err => allRowsError = err
        ));

        if (allRows == null || allRows.Count == 0)
        {
            Debug.LogWarning("[WarehouseBenchmark][HIGHLIGHT] Sem dataset para highlight. Erro: " + allRowsError);
            yield break;
        }

        var carPool = BuildCarPool(allRows, Mathf.Max(1, maxDistinctCarsForHighlight));
        if (carPool.Count == 0 && !runPinnedCarChecks)
        {
            Debug.LogWarning("[WarehouseBenchmark][HIGHLIGHT] Sem carIds válidos no dataset.");
            yield break;
        }

        if (runPinnedCarChecks)
        {
            if (!string.IsNullOrWhiteSpace(pinnedCarIdWithBoxes))
            {
                Debug.Log($"[WarehouseBenchmark][HIGHLIGHT][PINNED] carIdWithBoxes={pinnedCarIdWithBoxes}");
                yield return RunSingleHighlightSample(report, allRows, pinnedCarIdWithBoxes, "pinned_with_boxes", report.highlightSamples.Count + 1);
            }

            if (!string.IsNullOrWhiteSpace(pinnedCarIdWithoutBoxes))
            {
                Debug.Log($"[WarehouseBenchmark][HIGHLIGHT][PINNED] carIdWithoutBoxes={pinnedCarIdWithoutBoxes}");
                yield return RunSingleHighlightSample(report, allRows, pinnedCarIdWithoutBoxes, "pinned_without_boxes", report.highlightSamples.Count + 1);
            }
        }

        for (int i = 0; i < report.highlightIterations; i++)
        {
            string carId = carPool[i % carPool.Count];
            yield return RunSingleHighlightSample(report, allRows, carId, "dynamic", i + 1);

            if (delayBetweenIterationsSeconds > 0f)
                yield return new WaitForSecondsRealtime(delayBetweenIterationsSeconds);
        }
    }

    private IEnumerator RunSingleHighlightSample(
        WarehouseBenchmarkReport report,
        List<StorageRowDTO> allRows,
        string carId,
        string scenario,
        int iteration)
    {
        var sample = new HighlightSample
        {
            scenario = scenario,
            iteration = iteration,
            carId = carId,
            startedAtUtc = DateTime.UtcNow.ToString("o")
        };

        double t0 = Time.realtimeSinceStartupAsDouble;

        List<StorageRowDTO> carRows = null;
        string carError = null;

        double tCarFetchStart = Time.realtimeSinceStartupAsDouble;
        yield return StartCoroutine(storageRepository.GetStorageForCar(
            carId,
            onSuccess: rows => carRows = rows ?? new List<StorageRowDTO>(),
            onError: err => carError = err
        ));
        double tCarFetchDone = Time.realtimeSinceStartupAsDouble;

        var rowsForHighlight = carRows;
        if (rowsForHighlight == null && !string.IsNullOrWhiteSpace(carError))
            rowsForHighlight = allRows;

        warehouseManager.HighlightCarBoxes(carId, rowsForHighlight);
        yield return new WaitForEndOfFrame();

        double tEnd = Time.realtimeSinceStartupAsDouble;

        sample.fetchCarRowsMs = ToMs(tCarFetchDone - tCarFetchStart);
        sample.highlightApplyMs = ToMs(tEnd - tCarFetchDone);
        sample.totalMs = ToMs(tEnd - t0);
        sample.hadError = !string.IsNullOrWhiteSpace(carError);
        sample.errorMessage = carError;
        sample.targetRowsCount = CountRowsForCar(allRows, carId);
        sample.returnedRowsCount = carRows?.Count ?? 0;

        report.highlightSamples.Add(sample);

        Debug.Log($"[WarehouseBenchmark][HIGHLIGHT] scenario={sample.scenario} iter={sample.iteration} carId={sample.carId} totalMs={sample.totalMs:F2} targetRows={sample.targetRowsCount} returnedRows={sample.returnedRowsCount} hadError={sample.hadError}");
    }

    private static List<string> BuildCarPool(List<StorageRowDTO> rows, int maxCars)
    {
        var distinct = rows
            .Where(r => r != null && !string.IsNullOrWhiteSpace(r.carId) && r.carId != "unknown")
            .GroupBy(r => r.carId)
            .OrderByDescending(g => g.Count())
            .Take(maxCars)
            .Select(g => g.Key)
            .ToList();

        return distinct;
    }

    private static int CountRowsForCar(List<StorageRowDTO> rows, string carId)
    {
        if (rows == null || string.IsNullOrWhiteSpace(carId)) return 0;

        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row != null && row.carId == carId)
                count++;
        }

        return count;
    }

    private static WarehouseBenchmarkSummary BuildSummary(WarehouseBenchmarkReport report)
    {
        var summary = new WarehouseBenchmarkSummary
        {
            loadTotalMs = BuildStats(report.loadSamples.Select(s => s.totalMs).ToList()),
            loadDataFetchMs = BuildStats(report.loadSamples.Select(s => s.dataFetchMs).ToList()),
            loadVisualApplyMs = BuildStats(report.loadSamples.Select(s => s.visualApplyMs).ToList()),
            highlightTotalMs = BuildStats(report.highlightSamples.Select(s => s.totalMs).ToList()),
            highlightFetchCarRowsMs = BuildStats(report.highlightSamples.Select(s => s.fetchCarRowsMs).ToList()),
            highlightApplyMs = BuildStats(report.highlightSamples.Select(s => s.highlightApplyMs).ToList()),
            errorTotalMs = BuildStats(report.errorSamples.Select(s => s.totalMs).ToList()),
            loadErrorRate = Ratio(report.loadSamples.Count(s => s.hadError), report.loadSamples.Count),
            loadSuccessRate = Ratio(report.loadSamples.Count(s => s.success), report.loadSamples.Count),
            loadCacheFallbackRate = Ratio(report.loadSamples.Count(s => s.usedCacheFallback), report.loadSamples.Count),
            forcedErrorRecoveryRate = Ratio(report.errorSamples.Count(s => s.success), report.errorSamples.Count),
            forcedErrorFallbackRate = Ratio(report.errorSamples.Count(s => s.usedCacheFallback), report.errorSamples.Count)
        };

        return summary;
    }

    private static StatsSummary BuildStats(List<double> values)
    {
        if (values == null || values.Count == 0)
            return new StatsSummary();

        values.Sort();

        double sum = 0d;
        for (int i = 0; i < values.Count; i++)
            sum += values[i];

        double mean = sum / values.Count;

        double varianceSum = 0d;
        for (int i = 0; i < values.Count; i++)
        {
            double diff = values[i] - mean;
            varianceSum += diff * diff;
        }

        double std = Math.Sqrt(varianceSum / values.Count);

        int p95Index = Mathf.Clamp(Mathf.CeilToInt(values.Count * 0.95f) - 1, 0, values.Count - 1);

        return new StatsSummary
        {
            count = values.Count,
            min = values[0],
            max = values[values.Count - 1],
            mean = mean,
            std = std,
            p95 = values[p95Index]
        };
    }

    private static double Ratio(int numerator, int denominator)
    {
        if (denominator <= 0) return 0d;
        return (double)numerator / denominator;
    }

    private static double ToMs(double seconds)
    {
        return seconds * 1000d;
    }

    [Serializable]
    private class WarehouseBenchmarkReport
    {
        public string runId;
        public string generatedAtUtc;
        public string platform;
        public string unityVersion;
        public int warmupIterations;
        public int loadIterations;
        public int highlightIterations;
        public int forcedErrorIterations;
        public List<LoadSample> loadSamples;
        public List<HighlightSample> highlightSamples;
        public List<LoadSample> errorSamples;
        public WarehouseBenchmarkSummary summary;
    }

    [Serializable]
    private class LoadSample
    {
        public string scenario;
        public int iteration;
        public string startedAtUtc;
        public double layoutLoadMs;
        public double dataFetchMs;
        public double visualApplyMs;
        public double totalMs;
        public int rowsCount;
        public bool success;
        public bool hadError;
        public bool usedCacheFallback;
        public string errorMessage;
    }

    [Serializable]
    private class HighlightSample
    {
        public string scenario;
        public int iteration;
        public string carId;
        public string startedAtUtc;
        public double fetchCarRowsMs;
        public double highlightApplyMs;
        public double totalMs;
        public int targetRowsCount;
        public int returnedRowsCount;
        public bool hadError;
        public string errorMessage;
    }

    [Serializable]
    private class WarehouseBenchmarkSummary
    {
        public StatsSummary loadTotalMs;
        public StatsSummary loadDataFetchMs;
        public StatsSummary loadVisualApplyMs;
        public StatsSummary highlightTotalMs;
        public StatsSummary highlightFetchCarRowsMs;
        public StatsSummary highlightApplyMs;
        public StatsSummary errorTotalMs;
        public double loadSuccessRate;
        public double loadErrorRate;
        public double loadCacheFallbackRate;
        public double forcedErrorRecoveryRate;
        public double forcedErrorFallbackRate;
    }

    [Serializable]
    private class StatsSummary
    {
        public int count;
        public double min;
        public double max;
        public double mean;
        public double std;
        public double p95;
    }
}
