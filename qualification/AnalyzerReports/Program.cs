using System.Text.RegularExpressions;

if (args.Length < 5 || args[1] is not ("warning" or "error" or "none" or "wae") || !int.TryParse(args[2], out var exitCode))
{
    Console.Error.WriteLine("usage: AnalyzerReports <label> <warning|error|none|wae> <build-exit-code> <build-log> <source-directory>...");
    return 2;
}

const string RuleId = "RZT0001";
var label = args[0];
var mode = args[1];
var checks = new Checks();
var log = File.ReadAllLines(args[3]);

var expected = new List<Expected>();
foreach (var file in args[4..].SelectMany(SourceFiles))
{
    var lines = File.ReadAllLines(file);
    for (var index = 0; index < lines.Length; index++)
    {
        foreach (Match run in Regex.Matches(lines[index], @"(?:/\*[A-Z]+[0-9]+\*/)+"))
        {
            var (line, column) = (index + 1, run.Index + run.Length + 1);
            if (lines[index].Trim() == run.Value && index + 1 < lines.Length)
            {
                (line, column) = (index + 2, lines[index + 1].Length - lines[index + 1].TrimStart().Length + 1);
            }

            foreach (Match marker in Regex.Matches(run.Value, @"[A-Z]+[0-9]+"))
            {
                expected.Add(new Expected(file, line, column, marker.Value));
            }
        }

        var trailing = Regex.Match(lines[index], @"// ([A-Z]+[0-9]+)\s*$");
        if (trailing.Success)
        {
            expected.Add(new Expected(file, index + 1, null, trailing.Groups[1].Value));
        }
    }
}

string? Severity(string id) => (mode, id) switch
{
    ("none", RuleId) => null,
    ("warning", _) or ("none", _) => "warning",
    ("error", RuleId) => "error",
    ("error", _) => "warning",
    _ => "error",
};

var required = expected
    .Select(item => (item, Severity: Severity(item.Id)))
    .Where(item => item.Severity is not null)
    .ToList();

var diagnosticLine = new Regex(@"^\s*(?<path>[^(\s][^(]*\.cs)\((?<line>\d+),(?<column>\d+)\): (?<severity>warning|error) (?<id>[A-Z]+[0-9]+):");
var anyDiagnostic = new Regex(@"\b(?:warning|error) [A-Z]+[0-9]+\b");
var faults = new Regex(@"\b(?:AD0001|CS8032|CS8033|CS8034|CS9057)\b");
var actual = new List<Actual>();
var unparsed = new List<string>();
foreach (var line in log)
{
    var match = diagnosticLine.Match(line);
    if (match.Success)
    {
        actual.Add(new Actual(
            Path.GetFullPath(match.Groups["path"].Value),
            int.Parse(match.Groups["line"].Value),
            int.Parse(match.Groups["column"].Value),
            match.Groups["severity"].Value,
            match.Groups["id"].Value));
    }
    else if (anyDiagnostic.IsMatch(line))
    {
        unparsed.Add(line.Trim());
    }
}

checks.That($"{label}: no analyzer load or execution faults (AD0001/CS8032/CS8033/CS8034/CS9057): "
    + string.Join(" | ", log.Where(line => faults.IsMatch(line)).Distinct()), !log.Any(line => faults.IsMatch(line)));
checks.That($"{label}: no diagnostics outside source files: {string.Join(" | ", unparsed.Distinct())}", unparsed.Count == 0);
checks.That($"{label}: every diagnostic is reported once ({actual.Count} lines)", actual.Distinct().Count() == actual.Count);

var unmatched = actual.Distinct().ToList();
foreach (var (item, severity) in required)
{
    var found = unmatched.FindIndex(diagnostic =>
        diagnostic.Path == item.Path && diagnostic.Line == item.Line && diagnostic.Id == item.Id
        && (item.Column is null || diagnostic.Column == item.Column));
    if (found < 0)
    {
        checks.Fail($"{label}: missing {severity} {item.Id} at {item.Path}({item.Line},{item.Column?.ToString() ?? "*"})");
        continue;
    }

    checks.That($"{label}: {item.Id} at {item.Path}({item.Line}) has severity {severity}, got {unmatched[found].Severity}",
        unmatched[found].Severity == severity);
    unmatched.RemoveAt(found);
}

foreach (var diagnostic in unmatched)
{
    checks.Fail($"{label}: unexpected {diagnostic.Severity} {diagnostic.Id} at {diagnostic.Path}({diagnostic.Line},{diagnostic.Column})");
}

var expectsError = required.Any(item => item.Severity == "error");
checks.That($"{label}: build exit code {exitCode} {(expectsError ? "reports failure" : "reports success")}", expectsError ? exitCode != 0 : exitCode == 0);
var rules = required.Count(item => item.item.Id == RuleId);
Console.WriteLine($"INFO {label} ({mode}): {rules} {RuleId}, "
    + $"{required.Count - rules} other expected diagnostics, exit {exitCode}");
return checks.Complete();

static IEnumerable<string> SourceFiles(string directory) =>
    Directory.EnumerateFiles(Path.GetFullPath(directory), "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
        .Order(StringComparer.Ordinal);

internal sealed record Expected(string Path, int Line, int? Column, string Id);

internal sealed record Actual(string Path, int Line, int Column, string Severity, string Id);
