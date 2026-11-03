using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PdfOxideTests.Support;

internal sealed record CrashHostResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool ReportedOk => StandardOutput.Contains("result=ok", StringComparison.Ordinal);

    public bool ReportedError => StandardOutput.Contains("result=error", StringComparison.Ordinal);

    public bool ReportedManagedException =>
        StandardOutput.Contains("result=managed-exception", StringComparison.Ordinal);
}

internal static class CrashHostRunner
{
    private static readonly Lazy<string> HostPath = new(ResolveHostPath);

    public static CrashHostResult Run(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(HostPath.Value);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start FfiCrashHost.");

        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
        {
            TryKill(process);
            throw new TimeoutException(
                "FfiCrashHost did not exit within 30 seconds: " + string.Join(' ', arguments));
        }

        return new CrashHostResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            _ = process.WaitForExit(5000);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static string ResolveHostPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("FFI_CRASH_HOST");
        if (!string.IsNullOrEmpty(overridePath))
        {
            return overridePath;
        }

        string candidate = Path.Combine(AppContext.BaseDirectory, "ffi-crash-host", "FfiCrashHost.dll");
        return File.Exists(candidate)
            ? candidate
            : throw new FileNotFoundException(
                "FfiCrashHost.dll was not copied next to the test assembly. Build the test project first, " +
                "or set FFI_CRASH_HOST to the host dll.",
                candidate);
    }
}
