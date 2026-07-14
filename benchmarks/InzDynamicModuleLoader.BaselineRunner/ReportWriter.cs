using System.Text;
using InzDynamicModuleLoader.Bench.Shared;

namespace InzDynamicModuleLoader.BaselineRunner;

public static class ReportWriter
{
    public sealed record Row(string ConfigId, string Variant, int ModuleCount,
        double ColdTotalMs, double WarmTotalMs, double WarmTotalP25, double WarmTotalP75,
        double WarmLoadMs, double WarmDiscoveryMs, double WarmResolveTotalMs, int ResolveCount,
        double CacheHitRatio, long ManagedMemDeltaBytes, long WorkingSetBytes);

    public static Row Summarize(IReadOnlyList<ProbeResult> runs)
    {
        var cold = runs.FirstOrDefault(r => r.Iteration == 0) ?? runs[0];
        var warm = runs.Where(r => r.Iteration > 0).ToList();
        if (warm.Count == 0) warm = runs.ToList();
        double[] warmTotals = warm.Select(r => r.TotalMs).ToArray();
        var f = runs[0];
        return new Row(f.ConfigId, f.Variant, f.ModuleCount,
            cold.TotalMs, Aggregation.Median(warmTotals),
            Aggregation.Percentile(warmTotals, 25), Aggregation.Percentile(warmTotals, 75),
            Aggregation.Median(warm.Select(r => r.LoadTotalMs).ToArray()),
            Aggregation.Median(warm.Select(r => r.DiscoveryMs).ToArray()),
            Aggregation.Median(warm.Select(r => r.ResolveTotalMs).ToArray()),
            (int)Aggregation.Median(warm.Select(r => (double)r.ResolveCount).ToArray()),
            Aggregation.Median(warm.Select(r => r.CacheHitRatio).ToArray()),
            (long)Aggregation.Median(warm.Select(r => (double)r.ManagedMemDeltaBytes).ToArray()),
            (long)Aggregation.Median(warm.Select(r => (double)r.WorkingSetBytes).ToArray()));
    }

    public static void Write(string mdPath, IReadOnlyList<Row> rows, string provenance,
        IReadOnlyDictionary<string, long> footprintBytes, IReadOnlyList<string> failures)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Baseline Results\n");
        sb.AppendLine($"> {provenance}\n");
        sb.AppendLine("| Config | Variant | Modules | Cold total (ms) | Warm total med (ms) | P25 | P75 | Load | Discovery | Resolve total | Resolves | Cache hit | Mem Δ (MB) | Working set (MB) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var r in rows)
            sb.AppendLine($"| {r.ConfigId} | {r.Variant} | {r.ModuleCount} | {r.ColdTotalMs:F1} | {r.WarmTotalMs:F1} | {r.WarmTotalP25:F1} | {r.WarmTotalP75:F1} | {r.WarmLoadMs:F1} | {r.WarmDiscoveryMs:F1} | {r.WarmResolveTotalMs:F1} | {r.ResolveCount} | {r.CacheHitRatio:P0} | {r.ManagedMemDeltaBytes / 1_048_576.0:F1} | {r.WorkingSetBytes / 1_048_576.0:F1} |");
        sb.AppendLine("\n## Footprint (BuiltModules size)\n");
        foreach (var (key, v) in footprintBytes) sb.AppendLine($"- **{key}**: {v / 1_048_576.0:F1} MB");
        if (failures.Count > 0)
        {
            sb.AppendLine("\n## Failed iterations (excluded from aggregates)\n");
            foreach (var fl in failures) sb.AppendLine($"- {fl}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(mdPath)!);
        File.WriteAllText(mdPath, sb.ToString());
        var csv = new StringBuilder("configId,variant,moduleCount,coldTotalMs,warmTotalMs,p25,p75,loadMs,discoveryMs,resolveTotalMs,resolveCount,cacheHitRatio,memDeltaBytes,workingSetBytes\n");
        foreach (var r in rows)
            csv.AppendLine($"{r.ConfigId},{r.Variant},{r.ModuleCount},{r.ColdTotalMs},{r.WarmTotalMs},{r.WarmTotalP25},{r.WarmTotalP75},{r.WarmLoadMs},{r.WarmDiscoveryMs},{r.WarmResolveTotalMs},{r.ResolveCount},{r.CacheHitRatio},{r.ManagedMemDeltaBytes},{r.WorkingSetBytes}");
        File.WriteAllText(Path.ChangeExtension(mdPath, ".csv"), csv.ToString());
    }
}
