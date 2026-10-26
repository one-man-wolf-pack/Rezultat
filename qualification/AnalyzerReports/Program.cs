using System.Globalization;
using System.Text.RegularExpressions;
using Rezultat.Qualification;


if (args.Length < 5 || args[1] is not ("warning" or "error" or "none" or "wae") || !int.TryParse(args[2], out int exitCode)) {
    Console.Error.WriteLine("usage: AnalyzerReports <label> <warning|error|none|wae> <build-exit-code> <build-log> <source-directory>...");
    return 2;
}

const string RuleId = "RZT0001";
string label = args[0];
string mode = args[1];
var checks = new Checks();
string[] log = File.ReadAllLines(args[3]);

var expected = new List<Expected>();
foreach (string? file in args[4..].SelectMany(SourceFiles)) {
    string[] lines = File.ReadAllLines(file);
    for (int index = 0; index < lines.Length; index++) {
        foreach (Match run in Patterns.MarkerRun().Matches(lines[index])) {
            var (line, column) = (index + 1, run.Index + run.Length + 1);
            if (lines[index].Trim() == run.Value && index + 1 < lines.Length) {
                (line, column) = (index + 2, lines[index + 1].Length - lines[index + 1].TrimStart().Length + 1);
            }

            foreach (Match marker in Patterns.MarkerId().Matches(run.Value)) {
                expected.Add(new Expected(file, line, column, marker.Value));
            }
        }

        var trailing = Patterns.TrailingMarker().Match(lines[index]);
        if (trailing.Success) {
            expected.Add(new Expected(file, index + 1, null, trailing.Groups[1].Value));
        }
    }
}

string? Severity(string id) => (mode, id) switch {
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

var diagnosticLine = Patterns.DiagnosticLine();
var anyDiagnostic = Patterns.AnyDiagnostic();
var faults = Patterns.Faults();
var actual = new List<Actual>();
var unparsed = new List<string>();
foreach (string line in log) {
    var match = diagnosticLine.Match(line);
    if (match.Success) {
        actual.Add(new Actual(
            Path.GetFullPath(match.Groups["path"].Value),
            int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["column"].Value, CultureInfo.InvariantCulture),
            match.Groups["severity"].Value,
            match.Groups["id"].Value));
    } else if (anyDiagnostic.IsMatch(line)) {
        unparsed.Add(line.Trim());
    }
}

checks.That($"{label}: no analyzer load or execution faults (AD0001/CS8032/CS8033/CS8034/CS9057): "
    + string.Join(" | ", log.Where(line => faults.IsMatch(line)).Distinct()), !log.Any(line => faults.IsMatch(line)));
checks.That($"{label}: no diagnostics outside source files: {string.Join(" | ", unparsed.Distinct())}", unparsed.Count == 0);
checks.That($"{label}: every diagnostic is reported once ({actual.Count} lines)", actual.Distinct().Count() == actual.Count);

var unmatched = actual.Distinct().ToList();
foreach (var (item, severity) in required) {
    int found = unmatched.FindIndex(diagnostic =>
        diagnostic.Path == item.Path && diagnostic.Line == item.Line && diagnostic.Id == item.Id
        && (item.Column is null || diagnostic.Column == item.Column));
    if (found < 0) {
        checks.Fail($"{label}: missing {severity} {item.Id} at {item.Path}({item.Line},{item.Column?.ToString(CultureInfo.InvariantCulture) ?? "*"})");
        continue;
    }

    checks.That($"{label}: {item.Id} at {item.Path}({item.Line}) has severity {severity}, got {unmatched[found].Severity}",
        unmatched[found].Severity == severity);
    unmatched.RemoveAt(found);
}

foreach (var diagnostic in unmatched) {
    checks.Fail($"{label}: unexpected {diagnostic.Severity} {diagnostic.Id} at {diagnostic.Path}({diagnostic.Line},{diagnostic.Column})");
}

bool expectsError = required.Any(item => item.Severity == "error");
checks.That($"{label}: build exit code {exitCode} {(expectsError ? "reports failure" : "reports success")}", expectsError ? exitCode != 0 : exitCode == 0);
int rules = required.Count(item => item.item.Id == RuleId);
Console.WriteLine($"INFO {label} ({mode}): {rules} {RuleId}, "
    + $"{required.Count - rules} other expected diagnostics, exit {exitCode}");
return checks.Complete();

static IEnumerable<string> SourceFiles(string directory) =>
    Directory.EnumerateFiles(Path.GetFullPath(directory), "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
        .Order(StringComparer.Ordinal);

internal sealed record Expected(string Path, int Line, int? Column, string Id);

internal sealed record Actual(string Path, int Line, int Column, string Severity, string Id);

// The match timeout bounds a pathological log or source line; a timeout fails the check.
internal static partial class Patterns {
    [GeneratedRegex(@"(?:/\*[A-Z]+[0-9]+\*/)+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex MarkerRun();

    [GeneratedRegex(@"[A-Z]+[0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex MarkerId();

    [GeneratedRegex(@"// ([A-Z]+[0-9]+)\s*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex TrailingMarker();

    [GeneratedRegex(@"^\s*(?<path>[^(\s][^(]*\.cs)\((?<line>\d+),(?<column>\d+)\): (?<severity>warning|error) (?<id>[A-Z]+[0-9]+):", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex DiagnosticLine();

    [GeneratedRegex(@"\b(?:warning|error) [A-Z]+[0-9]+\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex AnyDiagnostic();

    [GeneratedRegex(@"\b(?:AD0001|CS8032|CS8033|CS8034|CS9057)\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Faults();
}
