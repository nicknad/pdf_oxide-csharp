using System;
using System.Collections.Generic;

namespace FfiCrashHost;

internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitManagedFailure = 2;
    private const int ExitUsage = 3;

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            WriteUsage();
            return ExitUsage;
        }

        string scenarioName = args[0];
        IReadOnlyList<string> scenarioArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();

        if (!Scenarios.TryGet(scenarioName, out ScenarioRunner? scenario) || scenario is null)
        {
            Console.Error.WriteLine($"unknown scenario: {scenarioName}");
            WriteUsage();
            return ExitUsage;
        }

        try
        {
            ScenarioResult result = scenario(scenarioArgs);
            (string status, string? error, string? detail) = result switch
            {
                ScenarioSucceeded succeeded => ("ok", null, succeeded.Detail),
                ScenarioFailed failed => ("error", failed.Error, failed.Detail),
            };

            Console.Out.WriteLine($"scenario={scenarioName}");
            Console.Out.WriteLine($"result={status}");
            if (error is not null)
            {
                Console.Out.WriteLine($"error={Escape(error)}");
            }

            if (detail is not null)
            {
                Console.Out.WriteLine($"detail={Escape(detail)}");
            }

            return ExitOk;
        }
        catch (Exception ex)
        {
            Console.Out.WriteLine("result=managed-exception");
            Console.Out.WriteLine($"error={Escape(ex.GetType().Name + ": " + ex.Message)}");
            Console.Error.WriteLine(ex);
            return ExitManagedFailure;
        }
    }

    private static string Escape(string value) =>
        value.Replace("\r", "\\r", StringComparison.Ordinal)
             .Replace("\n", "\\n", StringComparison.Ordinal);

    private static void WriteUsage()
    {
        Console.Error.WriteLine("usage: dotnet FfiCrashHost.dll <scenario> [args...]");
        Console.Error.WriteLine("scenarios: " + string.Join(", ", Scenarios.Names));
    }
}
