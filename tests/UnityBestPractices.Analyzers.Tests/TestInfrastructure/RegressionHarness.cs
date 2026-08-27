using System;
using System.Collections.Generic;
using System.Threading.Tasks;

internal interface IRegressionSuite
{
    string Name { get; }

    Task RunAsync();
}

internal static class RegressionHarness
{
    internal static async Task<int> RunAsync(IEnumerable<IRegressionSuite> suites, string[] args)
    {
        if (args.Length != 0)
        {
            Console.Error.WriteLine($"Unknown regression suite: {string.Join(" ", args)}");
            return 2;
        }

        foreach (var suite in suites)
        {
            try
            {
                await suite.RunAsync();
            }
            catch (RegressionCaseException exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    $"Suite: {suite.Name}\nDiagnostic ID: <not available>\nCase: suite execution\n" +
                    $"Unexpected diagnostics: <not available>\nCompiler errors: <not available>\n" +
                    $"Transformed source: <not available>\n{exception}");
                return 1;
            }
        }

        return 0;
    }
}
