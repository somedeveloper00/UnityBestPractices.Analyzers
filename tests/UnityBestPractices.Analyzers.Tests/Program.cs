
if (args.Length == 2 && args[0] == "--generate-rule-docs")
{
    RuleDocumentationGenerator.Generate(args[1]);
    return 0;
}

IRegressionSuite[] suites =
[
    new AnalyzerTests(),
];

return await RegressionHarness.RunAsync(suites, args);
