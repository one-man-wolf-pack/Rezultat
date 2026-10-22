using System.Text.Json;
using System.Xml.Linq;

if (args.Length < 3 || args[0] is not ("v2" or "v3") || (args[0] == "v3" && args.Length < 4))
{
    Console.Error.WriteLine("usage: RunnerReports v2 <pair> <trx> | RunnerReports v3 <xunit.v3-version> <xunit.xml> <native-json-log>");
    return 2;
}

var pair = args[0] == "v3" ? $"xunit.v3 {args[1]}" : args[1];
var framework = $"xUnit.net v3 {args[1]}+";
var checks = new Checks();
var family = args[0] == "v2" ? "Family.FailureIsTheV3FailExceptionNotTheV2AssertionFamily" : "Family.FailureIsTheV3FailException";
string[] passing = ["Positive.ExtractsAllFourBranches", "Positive.ExtractsAllFourBranchesWithoutPayload", "Positive.KeepsNullablePayloads", "Positive.FailureStopsOnlyTheCurrentMultipleLambda", family];
var failing = new Dictionary<string, string>
{
    ["ExpectedFailures.WrongBranch"] = "actual Result.Error(wrong-branch-evidence)",
    ["ExpectedFailures.Uninitialized"] = "actual Validation.Uninitialized",
    ["ExpectedFailures.FormatterFault"] = "<ToString threw System.InvalidOperationException>",
    ["ExpectedFailures.InsideMultiple"] = "actual Result.Ok(7)",
    ["ExpectedFailures.WrongBranchWithoutPayload"] = "actual Result.Error.",
    ["ExpectedFailures.InvalidWithoutPayload"] = "actual Validation.Invalid.",
    ["ExpectedFailures.UninitializedWithoutPayload"] = "actual Result.Uninitialized",
};
string[] undisclosed = ["secret-sentinel", "ToString", "issues"];

var report = args[0] == "v2" ? ReadTrx(args[2]) : ReadXunitXml(args[2]);
if (args[0] == "v3")
{
    checks.That($"{pair}: dotnet test ran {framework}: {report.Framework}", report.Framework?.StartsWith(framework, StringComparison.Ordinal) == true);
}

checks.That($"{pair}: report has no runner errors ({report.Errors})", report.Errors == 0);
checks.That($"{pair}: report counts total {report.Results.Count}, expected {passing.Length + failing.Count}",
    report.Results.Count == passing.Length + failing.Count);
foreach (var name in passing)
{
    checks.That($"{pair}: {name} passed", report.Results.TryGetValue(name, out var result) && result.Outcome == Outcome.Passed);
}

foreach (var (name, evidence) in failing)
{
    if (!report.Results.TryGetValue(name, out var result))
    {
        checks.Fail($"{pair}: {name} missing from report");
        continue;
    }

    checks.That($"{pair}: {name} is a failed test", result.Outcome == Outcome.Failed);
    checks.That($"{pair}: {name} message carries '{evidence}': {result.Message}", result.Message.Contains(evidence, StringComparison.Ordinal));
    if (name.EndsWith("WithoutPayload", StringComparison.Ordinal))
    {
        checks.That($"{pair}: {name} message names only the branches: {result.Message}",
            !undisclosed.Any(text => result.Message.Contains(text, StringComparison.Ordinal)) && !result.Message.Any(char.IsDigit));
    }

    if (result.ExceptionType is not null)
    {
        checks.Text($"{pair}: {name} exception type", "Xunit.Sdk.FailException", result.ExceptionType);
    }

    var top = result.StackTrace.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    var method = name.Split('.')[1];
    checks.That($"{pair}: {name} top frame is the test caller with source line: {top}",
        top.StartsWith("at RunnerConsumer.ExpectedFailures.", StringComparison.Ordinal)
        && top.Contains(method, StringComparison.Ordinal)
        && top.Contains("RunnerScenarios.cs:line ", StringComparison.Ordinal));
    checks.That($"{pair}: {name} stack has no helper frames", !result.StackTrace.Contains("Rezultat.Testing.Xunit.", StringComparison.Ordinal));
}

if (args[0] == "v3")
{
    var (nativeFramework, causes) = ReadNativeCauses(args[3]);
    checks.That($"{pair}: native run used {framework}: {nativeFramework}", nativeFramework?.StartsWith(framework, StringComparison.Ordinal) == true);
    checks.That($"{pair}: native report lists exactly the expected failures",
        causes.Keys.Order(StringComparer.Ordinal).SequenceEqual(failing.Keys.Order(StringComparer.Ordinal)));
    foreach (var (name, (cause, exception)) in causes)
    {
        checks.Text($"{pair}: {name} native failure cause", "Assertion", cause);
        checks.Text($"{pair}: {name} native exception", "Xunit.Sdk.FailException", exception);
    }
}

return checks.Complete();

static string ShortName(string fullName) => fullName.StartsWith("RunnerConsumer.", StringComparison.Ordinal) ? fullName["RunnerConsumer.".Length..] : fullName;

static Report ReadXunitXml(string path)
{
    var assembly = XDocument.Load(path).Root!.Element("assembly")!;
    var results = assembly.Descendants("test").ToDictionary(
        test => ShortName((string)test.Attribute("name")!),
        test =>
        {
            var failure = test.Element("failure");
            var outcome = (string)test.Attribute("result")! switch
            {
                "Pass" => Outcome.Passed,
                "Fail" => Outcome.Failed,
                _ => Outcome.Other,
            };
            return new TestResult(outcome, failure?.Element("message")?.Value ?? string.Empty,
                failure?.Element("stack-trace")?.Value ?? string.Empty, (string?)failure?.Attribute("exception-type"));
        });
    return new Report((int)assembly.Attribute("errors")!, results, (string?)assembly.Attribute("test-framework"));
}

static Report ReadTrx(string path)
{
    var root = XDocument.Load(path).Root!;
    XNamespace ns = root.Name.NamespaceName;
    var results = root.Descendants(ns + "UnitTestResult").ToDictionary(
        test => ShortName((string)test.Attribute("testName")!),
        test =>
        {
            var outcome = (string)test.Attribute("outcome")! switch
            {
                "Passed" => Outcome.Passed,
                "Failed" => Outcome.Failed,
                _ => Outcome.Other,
            };
            var error = test.Descendants(ns + "ErrorInfo").FirstOrDefault();
            return new TestResult(outcome, error?.Element(ns + "Message")?.Value ?? string.Empty,
                error?.Element(ns + "StackTrace")?.Value ?? string.Empty, null);
        });
    var counters = root.Descendants(ns + "Counters").Single();
    var runnerMessages = root.Descendants(ns + "RunInfo")
        .Select(info => info.Value.Trim())
        .Count(text => !text.EndsWith("[FAIL]", StringComparison.Ordinal) || !text.Contains("RunnerConsumer.ExpectedFailures.", StringComparison.Ordinal));
    return new Report((int)counters.Attribute("error")! + runnerMessages, results, null);
}

static (string? Framework, Dictionary<string, (string Cause, string Exception)> Causes) ReadNativeCauses(string path)
{
    string? framework = null;
    var names = new Dictionary<string, string>();
    var causes = new Dictionary<string, (string, string)>();
    foreach (var line in File.ReadLines(path).Where(line => line.StartsWith('{')))
    {
        using var message = JsonDocument.Parse(line);
        var root = message.RootElement;
        var type = root.GetProperty("$type").GetString();
        var id = root.TryGetProperty("TestUniqueID", out var value) ? value.GetString()! : string.Empty;
        if (type == "test-assembly-starting")
        {
            framework = root.GetProperty("TestFrameworkDisplayName").GetString();
        }
        else if (type == "test-starting")
        {
            names[id] = ShortName(root.GetProperty("TestDisplayName").GetString()!);
        }
        else if (type == "test-failed")
        {
            causes[names[id]] = (root.GetProperty("Cause").GetString()!, root.GetProperty("ExceptionTypes")[0].GetString()!);
        }
    }

    return (framework, causes);
}

internal enum Outcome
{
    Passed,
    Failed,
    Other,
}

internal sealed record TestResult(Outcome Outcome, string Message, string StackTrace, string? ExceptionType);

internal sealed record Report(int Errors, Dictionary<string, TestResult> Results, string? Framework);
