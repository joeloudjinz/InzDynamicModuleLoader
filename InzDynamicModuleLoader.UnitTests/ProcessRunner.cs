using System.Diagnostics;

namespace InzDynamicModuleLoader.UnitTests;

/// <summary>
/// Starts a child process, waits with a timeout, and returns its combined output.
/// A test must never wait for ever on a child process.
/// </summary>
internal static class ProcessRunner
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    public sealed record Result(int ExitCode, string Output);

    /// <summary>Runs a process and returns the result. Does not throw when the process fails.</summary>
    public static Result Run(string fileName, string arguments, string workingDirectory, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start '{fileName} {arguments}'.");

        // Start both reads before waiting, so a full pipe cannot block the child.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)limit.TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw new TimeoutException(
                $"'{fileName} {arguments}' did not finish within {limit.TotalSeconds:F0} s and was stopped.");
        }

        return new Result(process.ExitCode, stdout.Result + stderr.Result);
    }

    /// <summary>Runs a dotnet command and throws when it fails. Use for setup steps.</summary>
    public static string Dotnet(string arguments, string workingDirectory, TimeSpan? timeout = null)
    {
        var result = Run("dotnet", arguments, workingDirectory, timeout);
        if (result.ExitCode != 0) throw new InvalidOperationException($"`dotnet {arguments}` failed (exit {result.ExitCode}):\n{result.Output}");
        return result.Output;
    }

    /// <summary>Finds the repository root by walking up to the solution file.</summary>
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "InzDynamicLoader.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new DirectoryNotFoundException($"Could not find InzDynamicLoader.sln above {AppContext.BaseDirectory}.");
    }

    /// <summary>The configuration this test assembly was built in.</summary>
    public static string CurrentConfiguration =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif
}
