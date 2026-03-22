using API;
using Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum HeatmapSyntheticScenario
{
    Uniform = 0,
    Skewed = 1,
    MetricDifferentiation = 2
}

public class HeatmapSyntheticDataProvider : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private bool forceSyntheticData = false;
    [SerializeField] private bool useSyntheticFallbackWhenEmpty = true;
    [SerializeField] private bool regenerateEachRequest = false;

    [Header("Scenario")]
    [SerializeField] private HeatmapSyntheticScenario scenario = HeatmapSyntheticScenario.Uniform;
    [SerializeField, Min(1)] private int syntheticCars = 60;
    [SerializeField, Min(1)] private int entriesPerCar = 120;
    [SerializeField, Min(1)] private int lookbackDays = 30;
    [SerializeField] private int randomSeed = 20260227;

    [Header("Scenario shape")]
    [SerializeField, Range(0f, 1f)] private float skewedHotspotProbability = 0.72f;
    [SerializeField, Range(0f, 1f)] private float metricDiffFrequencyGroupProbability = 0.65f;

    [Header("Runtime ingestion")]
    [SerializeField, Min(100)] private int ingestionBatchSize = 1000;

    [Header("Benchmark")]
    [SerializeField] private int[] benchmarkEventCounts = new[] { 1000, 5000, 25000, 100000 };

    private bool hasInjectedSynthetic;
    private string lastGroundTruthSummaryLine = string.Empty;
    private string lastGroundTruthTopFrequencyLine = string.Empty;
    private string lastGroundTruthTopOccupancyLine = string.Empty;

    public bool ForceSyntheticData => forceSyntheticData;
    public bool RegenerateEachRequest => regenerateEachRequest;
    public bool HasInjectedSynthetic => hasInjectedSynthetic;
    public HeatmapSyntheticScenario Scenario => scenario;
    public IReadOnlyList<int> BenchmarkEventCounts => benchmarkEventCounts;
    public string LastGroundTruthSummaryLine => lastGroundTruthSummaryLine;
    public string LastGroundTruthTopFrequencyLine => lastGroundTruthTopFrequencyLine;
    public string LastGroundTruthTopOccupancyLine => lastGroundTruthTopOccupancyLine;

    public void SetScenario(HeatmapSyntheticScenario targetScenario)
    {
        scenario = targetScenario;
        hasInjectedSynthetic = false;
    }

    public bool ShouldInject(APIscript api, bool backendMissing)
    {
        if (forceSyntheticData)
            return regenerateEachRequest || !hasInjectedSynthetic;

        if (!useSyntheticFallbackWhenEmpty)
            return false;

        return backendMissing;
    }

    public HeatmapSyntheticScenario CycleScenario()
    {
        scenario = scenario switch
        {
            HeatmapSyntheticScenario.Uniform => HeatmapSyntheticScenario.Skewed,
            HeatmapSyntheticScenario.Skewed => HeatmapSyntheticScenario.MetricDifferentiation,
            _ => HeatmapSyntheticScenario.Uniform
        };
        hasInjectedSynthetic = false;
        return scenario;
    }

    public IEnumerator InjectIntoApi(
        APIscript api,
        List<VirtualMapLocation> locations,
        int? overrideTotalEvents = null,
        HeatmapSyntheticScenario? overrideScenario = null)
    {
        if (api == null)
        {
            Debug.LogWarning("[HeatmapSynthetic] APIscript nulo, impossível injetar.");
            yield break;
        }

        if (locations == null || locations.Count == 0)
        {
            Debug.LogWarning("[HeatmapSynthetic] Sem localizações, impossível gerar dados.");
            yield break;
        }

        var validZones = new List<VirtualMapLocation>();
        foreach (var loc in locations)
        {
            if (loc != null && !string.IsNullOrWhiteSpace(loc.id))
                validZones.Add(loc);
        }

        if (validZones.Count == 0)
        {
            Debug.LogWarning("[HeatmapSynthetic] Sem zonas válidas (id), impossível gerar dados.");
            yield break;
        }

        var selectedScenario = overrideScenario ?? scenario;
        int totalTargetEvents = Mathf.Max(1, overrideTotalEvents ?? (syntheticCars * entriesPerCar));
        int cars = Mathf.Max(1, syntheticCars);
        int perCar = Mathf.Max(1, Mathf.CeilToInt(totalTargetEvents / (float)cars));

        var rng = new System.Random(randomSeed + (int)selectedScenario + totalTargetEvents);
        var tasks = new List<TaskDTO>(cars * perCar);
        var histories = new List<ActivityAndLocationHistory>(cars);

        DateTime startWindow = DateTime.UtcNow.AddDays(-Mathf.Max(1, lookbackDays));
        DateTime endWindow = DateTime.UtcNow;
        double windowSeconds = Math.Max(1.0, (endWindow - startWindow).TotalSeconds);

        int hotspotCount = Mathf.Clamp(Mathf.CeilToInt(validZones.Count * 0.2f), 1, validZones.Count);
        int freqDominantCount = Mathf.Clamp(Mathf.CeilToInt(validZones.Count * 0.3f), 1, validZones.Count);
        int occDominantCount = Mathf.Clamp(Mathf.CeilToInt(validZones.Count * 0.3f), 1, validZones.Count);

        int created = 0;

        for (int carIndex = 0; carIndex < cars; carIndex++)
        {
            string caseId = $"syn-car-{carIndex + 1:0000}";
            var history = new ActivityAndLocationHistory
            {
                Id = $"syn-history-{carIndex + 1:0000}",
                CaseInstanceId = caseId,
                History = new List<ActivityAndLocation>(perCar)
            };

            DateTime cursor = startWindow.AddSeconds(rng.NextDouble() * windowSeconds * 0.95);

            for (int i = 0; i < perCar; i++)
            {
                if (cursor >= endWindow.AddHours(-1))
                    cursor = startWindow.AddSeconds(rng.NextDouble() * windowSeconds * 0.95);

                int zoneIdx = PickZoneIndex(
                    rng,
                    selectedScenario,
                    validZones.Count,
                    hotspotCount,
                    freqDominantCount,
                    occDominantCount,
                    Mathf.Clamp01(skewedHotspotProbability),
                    Mathf.Clamp01(metricDiffFrequencyGroupProbability));
                var zone = validZones[zoneIdx];

                int durationSeconds = PickDurationSeconds(rng, selectedScenario, zoneIdx, hotspotCount, freqDominantCount);
                int gapSeconds = PickGapSeconds(rng, selectedScenario, zoneIdx, freqDominantCount);

                DateTime start = cursor;
                DateTime end = start.AddSeconds(durationSeconds);
                if (end > endWindow) end = endWindow;
                if (end <= start) end = start.AddSeconds(1);

                string id = $"syn-task-{carIndex + 1:0000}-{i + 1:00000}";

                tasks.Add(new TaskDTO
                {
                    id = id,
                    activityId = id,
                    processInstanceId = caseId,
                    startTime = start,
                    completionTime = end
                });

                history.History.Add(new ActivityAndLocation
                {
                    Id = $"syn-entry-{carIndex + 1:0000}-{i + 1:00000}",
                    ActivityId = id,
                    LocationId = zone.id
                });

                cursor = end.AddSeconds(gapSeconds);
                created++;

                if (created % Mathf.Max(100, ingestionBatchSize) == 0)
                    yield return null;
            }

            histories.Add(history);

            if ((carIndex + 1) % 25 == 0)
                yield return null;
        }

        api.tasks = tasks;
        api.activities = histories;
        hasInjectedSynthetic = true;

        Debug.Log($"[HeatmapSynthetic] Ingestão concluída | cenário={selectedScenario} | carros={cars} | eventos={created} | zonas={validZones.Count}");
        LogGroundTruthSummary(selectedScenario, validZones, histories, tasks);
    }

    private static int PickZoneIndex(
        System.Random rng,
        HeatmapSyntheticScenario scenario,
        int totalZones,
        int hotspotCount,
        int freqDominantCount,
        int occDominantCount,
        float hotspotProbability,
        float metricDiffFreqProbability)
    {
        if (totalZones <= 1)
            return 0;

        switch (scenario)
        {
            case HeatmapSyntheticScenario.Uniform:
                return rng.Next(0, totalZones);

            case HeatmapSyntheticScenario.Skewed:
                if (rng.NextDouble() < hotspotProbability)
                    return rng.Next(0, hotspotCount);
                return rng.Next(hotspotCount, totalZones);

            case HeatmapSyntheticScenario.MetricDifferentiation:
                if (rng.NextDouble() < metricDiffFreqProbability)
                    return rng.Next(0, freqDominantCount);

                int startOcc = Mathf.Clamp(totalZones - occDominantCount, 0, totalZones - 1);
                return rng.Next(startOcc, totalZones);

            default:
                return rng.Next(0, totalZones);
        }
    }

    private static int PickDurationSeconds(
        System.Random rng,
        HeatmapSyntheticScenario scenario,
        int zoneIdx,
        int hotspotCount,
        int freqDominantCount)
    {
        return scenario switch
        {
            HeatmapSyntheticScenario.Uniform => rng.Next(180, 1200),
            HeatmapSyntheticScenario.Skewed => zoneIdx < hotspotCount
                ? rng.Next(600, 2400)
                : rng.Next(120, 900),
            HeatmapSyntheticScenario.MetricDifferentiation => zoneIdx < freqDominantCount
                ? rng.Next(45, 210)
                : rng.Next(1200, 5400),
            _ => rng.Next(180, 1200)
        };
    }

    private static int PickGapSeconds(
        System.Random rng,
        HeatmapSyntheticScenario scenario,
        int zoneIdx,
        int freqDominantCount)
    {
        if (scenario == HeatmapSyntheticScenario.MetricDifferentiation && zoneIdx < freqDominantCount)
            return rng.Next(30, 180);

        return rng.Next(60, 480);
    }

    private static void LogGroundTruthSummary(
        HeatmapSyntheticScenario selectedScenario,
        List<VirtualMapLocation> zones,
        List<ActivityAndLocationHistory> histories,
        List<TaskDTO> tasks)
    {
        if (zones == null || zones.Count == 0 || histories == null || tasks == null)
            return;

        var taskById = new Dictionary<string, TaskDTO>(tasks.Count);
        foreach (var t in tasks)
        {
            if (t == null || string.IsNullOrWhiteSpace(t.id))
                continue;
            taskById[t.id] = t;
        }

        var freqByZone = new Dictionary<string, double>(zones.Count);
        var occByZone = new Dictionary<string, double>(zones.Count);
        double totalFreq = 0;
        double totalOccSeconds = 0;

        foreach (var h in histories)
        {
            if (h?.History == null)
                continue;

            foreach (var entry in h.History)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.LocationId))
                    continue;

                var loc = entry.LocationId;
                freqByZone[loc] = freqByZone.GetValueOrDefault(loc) + 1.0;
                totalFreq += 1.0;

                if (string.IsNullOrWhiteSpace(entry.ActivityId) || !taskById.TryGetValue(entry.ActivityId, out var task) || task == null)
                    continue;

                var dur = (task.completionTime - task.startTime).TotalSeconds;
                if (dur <= 0) dur = 1;

                occByZone[loc] = occByZone.GetValueOrDefault(loc) + dur;
                totalOccSeconds += dur;
            }
        }

        string topFreq = BuildTop3String(freqByZone, totalFreq);
        string topOcc = BuildTop3String(occByZone, totalOccSeconds);
        double freqHhi = ComputeHhi(freqByZone, totalFreq);
        double occHhi = ComputeHhi(occByZone, totalOccSeconds);

        string summary = string.Format(
            CultureInfo.InvariantCulture,
            "[HeatmapSynthetic][GroundTruth] cenário={0} | zonas={1} | eventos={2:0} | totalOccSeconds={3:0} | freqHHI={4:0.0000} | occHHI={5:0.0000}",
            selectedScenario,
            zones.Count,
            totalFreq,
            totalOccSeconds,
            freqHhi,
            occHhi);
        string topFreqLine = $"[HeatmapSynthetic][GroundTruth][TopFrequency] {topFreq}";
        string topOccLine = $"[HeatmapSynthetic][GroundTruth][TopOccupancy] {topOcc}";

        Debug.Log(summary);
        Debug.Log(topFreqLine);
        Debug.Log(topOccLine);

        var provider = FindObjectOfType<HeatmapSyntheticDataProvider>(true);
        if (provider != null)
        {
            provider.lastGroundTruthSummaryLine = summary;
            provider.lastGroundTruthTopFrequencyLine = topFreqLine;
            provider.lastGroundTruthTopOccupancyLine = topOccLine;
        }
    }

    private static string BuildTop3String(Dictionary<string, double> valuesByZone, double total)
    {
        if (valuesByZone == null || valuesByZone.Count == 0 || total <= 0)
            return "n/a";

        var list = new List<KeyValuePair<string, double>>(valuesByZone);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));

        int take = Mathf.Min(3, list.Count);
        var parts = new List<string>(take);
        for (int i = 0; i < take; i++)
        {
            var kv = list[i];
            double pct = (kv.Value / total) * 100.0;
            parts.Add($"{kv.Key}={pct:0.00}%");
        }

        return string.Join(" | ", parts);
    }

    private static double ComputeHhi(Dictionary<string, double> valuesByZone, double total)
    {
        if (valuesByZone == null || valuesByZone.Count == 0 || total <= 0)
            return 0;

        double hhi = 0;
        foreach (var kv in valuesByZone)
        {
            if (kv.Value <= 0)
                continue;

            double p = kv.Value / total;
            hhi += p * p;
        }

        return hhi;
    }
}
