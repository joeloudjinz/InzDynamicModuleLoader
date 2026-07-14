using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using InzDynamicModuleLoader.Bench.Shared;
using InzDynamicModuleLoader.Core;
using InzDynamicModuleLoader.StartupProbe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

static string Arg(string[] a, string name, string def = "")
{
    var i = Array.IndexOf(a, name);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
}

var configId = Arg(args, "--config", "unknown");
var variant = Arg(args, "--variant", "logging-on");
var iterationRaw = Arg(args, "--iteration", "0");
if (!int.TryParse(iterationRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iteration))
{
    Console.Error.WriteLine($"Probe failed [config={configId}, variant={variant}]: invalid --iteration value '{iterationRaw}'");
    return 1;
}

var modules = Arg(args, "--modules").Split(',', StringSplitOptions.RemoveEmptyEntries);

if (variant == "logging-off") Console.SetOut(TextWriter.Null);

var dict = new Dictionary<string, string?> { ["Database:ConnectionString"] = "Server=localhost;Database=none;Uid=none;Pwd=none;" };
for (var i = 0; i < modules.Length; i++) dict[$"Modules:{i}"] = modules[i];
var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

using var listener = new ProbeEventListener();

ProbeResult result;
try
{
    var memBefore = GC.GetTotalMemory(forceFullCollection: true);
    var services = new ServiceCollection();

    var swRegister = Stopwatch.StartNew();
    services.RegisterModules(config);
    swRegister.Stop();

    var provider = services.BuildServiceProvider();

    var swInit = Stopwatch.StartNew();
    provider.InitializeModules(config);
    swInit.Stop();

    // Scope boundary: do NOT resolve ITestRepository/DbContext — no DB round-trip.

    var memAfter = GC.GetTotalMemory(forceFullCollection: true);
    using var proc = Process.GetCurrentProcess();
    var workingSet = proc.WorkingSet64;

    result = new ProbeResult
    {
        ConfigId = configId, Variant = variant, Iteration = iteration, ModuleCount = modules.Length,
        RegisterMs = swRegister.Elapsed.TotalMilliseconds,
        InitializeMs = swInit.Elapsed.TotalMilliseconds,
        TotalMs = swRegister.Elapsed.TotalMilliseconds + swInit.Elapsed.TotalMilliseconds,
        LoadTotalMs = listener.LoadTotalMs, DiscoveryMs = listener.DiscoveryMs,
        ResolveTotalMs = listener.ResolveTotalMs, ResolveAvgMs = listener.ResolveAvgMs,
        ResolveMaxMs = listener.ResolveMaxMs, ResolveCount = listener.ResolveCount,
        CacheHitRatio = listener.CacheHitRatio,
        ManagedMemDeltaBytes = memAfter - memBefore, WorkingSetBytes = workingSet
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Probe failed [config={configId}, variant={variant}, iteration={iteration}]: {ex.Message}");
    return 1;
}

var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
stdout.WriteLine(JsonSerializer.Serialize(result));
return 0;
