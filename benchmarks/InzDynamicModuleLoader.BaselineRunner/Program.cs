using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using InzDynamicModuleLoader.Bench.Shared;
using InzDynamicModuleLoader.BaselineRunner;

static string Arg(string[] a, string name, string def)
{ var i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : def; }

var repoRoot = Path.GetFullPath(Arg(args, "--repo-root", Directory.GetCurrentDirectory()));
var outPath = Arg(args, "--out", Path.Combine(repoRoot, ".claude", "research", "baseline-results.md"));
var kRaw = Arg(args, "--k", "20");
if (!int.TryParse(kRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) || k < 1)
{
    Console.Error.WriteLine($"invalid --k value '{kRaw}': expected an integer >= 1");
    return 1;
}
var includeSynthetic = Arg(args, "--synthetic", "true") != "false";

var probeDll = Path.Combine(repoRoot, "benchmarks", "InzDynamicModuleLoader.StartupProbe",
    "bin", "Release", "net9.0", "InzDynamicModuleLoader.StartupProbe.dll");
var probeDir = Path.GetDirectoryName(probeDll)!;
var builtModules = Path.Combine(repoRoot, "BuiltModules");

// PREFLIGHT: Release build populates BuiltModules and produces the probe exe.
RunDotnet($"build \"{Path.Combine(repoRoot, "InzDynamicLoader.sln")}\" -c Release --nologo", repoRoot);
if (!File.Exists(probeDll)) throw new FileNotFoundException($"Probe not built at {probeDll}");
if (!Directory.Exists(builtModules)) throw new DirectoryNotFoundException($"BuiltModules missing at {builtModules}");

var configs = new List<(string Id, string SourceRoot, string[] Modules)>
{
    ("real", builtModules, ["Example.Module.EFCore.MySQL", "Example.Module.EFCore.Repositories"])
};

string? synthWork = null;
try
{
    string? synthOutputRoot = null;
    if (includeSynthetic)
    {
        synthWork = Path.Combine(Path.GetTempPath(), "inz-synth-" + Environment.ProcessId);
        var abstractions = Path.Combine(repoRoot, "InzDynamicModuleLoader.Abstractions", "InzDynamicModuleLoader.Abstractions.csproj");
        var gen = new SyntheticModuleGenerator(synthWork, abstractions);
        Console.WriteLine("generating synthetic modules (builds many small projects; slow)...");
        var byCount = new List<string>();
        for (var i = 0; i < 50; i++) byCount.Add(gen.Generate(i, depth: 1));
        foreach (var n in new[] { 1, 5, 20, 50 })
            configs.Add(($"synthN{n}", gen.OutputRoot, byCount.Take(n).ToArray()));
        foreach (var d in new[] { 0, 3 })
        {
            var mods = Enumerable.Range(1000 + d * 100, 5).Select(i => gen.Generate(i, depth: d)).ToArray();
            configs.Add(($"synthD{d}", gen.OutputRoot, mods));
        }
        synthOutputRoot = gen.OutputRoot;
    }

    var rows = new List<ReportWriter.Row>();
    var footprint = new Dictionary<string, long> { ["real (BuiltModules total)"] = DirSize(builtModules) };
    if (synthOutputRoot is not null) footprint["synthetic (OutputRoot total)"] = DirSize(synthOutputRoot);
    var failures = new List<string>();

    foreach (var cfg in configs)
    {
        // Staging is variant-independent, so stage once per config, then run both variants.
        ModuleStager.Stage(cfg.SourceRoot, probeDir, cfg.Modules);
        foreach (var variant in new[] { "logging-on", "logging-off" })
        {
            var runs = new List<ProbeResult>();
            for (var iter = 0; iter < k; iter++)
            {
                var r = RunProbe(cfg.Id, variant, iter, cfg.Modules);
                if (r is null) { failures.Add($"{cfg.Id}/{variant}/iter{iter}"); continue; }
                runs.Add(r);
            }
            if (runs.Count == 0) { Console.WriteLine($"SKIP {cfg.Id}/{variant}: all iterations failed"); continue; }
            if (cfg.Id == "real" && runs.All(r => r.ResolveCount == 0))
                throw new InvalidOperationException("real config produced ResolveCount=0 — EventListener captured nothing; aborting (numbers would be wrong).");
            rows.Add(ReportWriter.Summarize(runs));
            Console.WriteLine($"done {cfg.Id}/{variant} ({runs.Count}/{k} ok)");
        }
    }

    var prov = $"Machine: {Environment.MachineName}; OS: {Environment.OSVersion}; .NET: {Environment.Version}; " +
               $"cores: {Environment.ProcessorCount}; config: Release; K={k}; median+IQR; cold=iter0; " +
               $"warm=warm-FS-cache (JIT cold every run); failures={failures.Count}.";
    ReportWriter.Write(outPath, rows, prov, footprint, failures);
    Console.WriteLine($"wrote {outPath}");
}
finally
{
    if (synthWork is not null && Directory.Exists(synthWork)) { try { Directory.Delete(synthWork, recursive: true); } catch { } }
}

return 0;

ProbeResult? RunProbe(string id, string variant, int iter, string[] modules)
{
    var psi = new ProcessStartInfo("dotnet",
        $"\"{probeDll}\" --config {id} --variant {variant} --iteration {iter} --modules {string.Join(",", modules)}")
    { RedirectStandardOutput = true, RedirectStandardError = true };
    using var p = Process.Start(psi)!;
    // Drain both streams concurrently before blocking on exit to avoid a full-pipe deadlock.
    var outTask = p.StandardOutput.ReadToEndAsync();
    var errTask = p.StandardError.ReadToEndAsync();
    p.WaitForExit();
    var stdout = outTask.Result;
    var stderr = errTask.Result;
    if (p.ExitCode != 0) { Console.Error.WriteLine($"probe failed {id}/{variant}/{iter}: {stderr.Trim()}"); return null; }
    var line = stdout.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    if (string.IsNullOrEmpty(line)) return null;
    try
    {
        var result = JsonSerializer.Deserialize<ProbeResult>(line);
        if (result is null) Console.Error.WriteLine($"probe produced null JSON {id}/{variant}/{iter}");
        return result;
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"probe produced malformed JSON {id}/{variant}/{iter}: {ex.Message}");
        return null;
    }
}

void RunDotnet(string arguments, string cwd)
{
    var psi = new ProcessStartInfo("dotnet", arguments) { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
    using var p = Process.Start(psi)!;
    var op = p.StandardOutput.ReadToEndAsync();
    var ep = p.StandardError.ReadToEndAsync();
    p.WaitForExit();
    if (p.ExitCode != 0) throw new InvalidOperationException($"`dotnet {arguments}` failed:\n{op.Result}\n{ep.Result}");
}

static long DirSize(string dir) => !Directory.Exists(dir) ? 0
    : Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
