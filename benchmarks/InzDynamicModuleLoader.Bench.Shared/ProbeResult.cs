namespace InzDynamicModuleLoader.Bench.Shared;

public sealed record ProbeResult
{
    public required string ConfigId { get; init; }
    public required string Variant { get; init; }          // "logging-on" | "logging-off"
    public required int Iteration { get; init; }
    public required int ModuleCount { get; init; }
    public required double RegisterMs { get; init; }
    public required double InitializeMs { get; init; }
    public required double TotalMs { get; init; }
    public required double LoadTotalMs { get; init; }
    public required double DiscoveryMs { get; init; }
    public required double ResolveTotalMs { get; init; }
    public required double ResolveAvgMs { get; init; }
    public required double ResolveMaxMs { get; init; }
    public required int ResolveCount { get; init; }
    public required double CacheHitRatio { get; init; }
    public required long ManagedMemDeltaBytes { get; init; }
    public required long PeakWorkingSetBytes { get; init; }
}
