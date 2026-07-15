using System.Text.Json;
using InzDynamicModuleLoader.Bench.Shared;

namespace InzDynamicModuleLoader.Bench.Tests;

public class ProbeResultSchemaTests
{
    [Fact]
    public void ProbeResult_RoundTrips_ThroughJson()
    {
        var r = new ProbeResult
        {
            ConfigId = "real", Variant = "logging-off", Iteration = 1, ModuleCount = 2,
            RegisterMs = 10, InitializeMs = 1, TotalMs = 11, LoadTotalMs = 4, DiscoveryMs = 0.5,
            ResolveTotalMs = 2, ResolveAvgMs = 0.2, ResolveMaxMs = 0.9, ResolveCount = 10,
            CacheHitRatio = 0.5, ManagedMemDeltaBytes = 123, WorkingSetBytes = 456
        };
        var back = JsonSerializer.Deserialize<ProbeResult>(JsonSerializer.Serialize(r))!;
        Assert.Equal(r, back);
    }
}
