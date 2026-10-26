using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Rezultat.Analyzers.Tests;

internal static class AnalyzerHarness {
    public const string ConsumerPath = "Consumer.cs";

    private static readonly CSharpParseOptions _parseOptions = new(LanguageVersion.CSharp13);

    private static readonly CSharpCompilationOptions _options =
        new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);

    public static ImmutableArray<MetadataReference> Framework { get; } =
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).StartsWith("Rezultat", StringComparison.Ordinal))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];

    public static MetadataReference Core { get; } = MetadataReference.CreateFromFile(typeof(Unit).Assembly.Location);

    public static ImmutableArray<MetadataReference> WithCore { get; } = Framework.Add(Core);

    public static MetadataReference Library(string assemblyName, string source, IEnumerable<MetadataReference> references) {
        var compilation = CSharpCompilation.Create(
            assemblyName, [CSharpSyntaxTree.ParseText(source, _parseOptions)], references, _options);
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        emitted.Success.ShouldBeTrue(string.Join(Environment.NewLine, emitted.Diagnostics));
        return MetadataReference.CreateFromImage(image.ToArray());
    }

    public static Markup Parse(string markup) {
        var source = new System.Text.StringBuilder();
        var spans = ImmutableArray.CreateBuilder<TextSpan>();
        int start = -1;
        int index = 0;
        while (index < markup.Length) {
            if (string.CompareOrdinal(markup, index, "[|", 0, 2) == 0) {
                start.ShouldBe(-1);
                start = source.Length;
                index += 2;
            } else if (string.CompareOrdinal(markup, index, "|]", 0, 2) == 0) {
                start.ShouldNotBe(-1);
                spans.Add(TextSpan.FromBounds(start, source.Length));
                start = -1;
                index += 2;
            } else {
                source.Append(markup[index]);
                index++;
            }
        }

        start.ShouldBe(-1);
        return new Markup(source.ToString(), spans.ToImmutable());
    }

    public static CSharpCompilation Compilation(IEnumerable<MetadataReference> references, params (string Path, string Source)[] files) =>
        Compilation("Consumer", references, files);

    public static CSharpCompilation Compilation(
        string assemblyName, IEnumerable<MetadataReference> references, params (string Path, string Source)[] files) =>
        CSharpCompilation.Create(
            assemblyName,
            files.Select(file => CSharpSyntaxTree.ParseText(file.Source, _parseOptions, file.Path)),
            references,
            _options);

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Compilation compilation) {
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        (errors.Length == 0).ShouldBeTrue(string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

        var exceptions = new List<Exception>();
        var withAnalyzers = compilation.WithAnalyzers(
            [new DiscardedOutcomeAnalyzer()],
            new CompilationWithAnalyzersOptions(
                new AnalyzerOptions([]),
                (exception, _, _) => exceptions.Add(exception),
                concurrentAnalysis: true,
                logAnalyzerExecutionTime: false));
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        exceptions.ShouldBeEmpty();
        diagnostics.ShouldNotContain(diagnostic => diagnostic.Id == "AD0001");
        return diagnostics;
    }

    public static void AssertReported(ImmutableArray<Diagnostic> diagnostics, string path, ImmutableArray<TextSpan> expected) {
        foreach (var diagnostic in diagnostics) {
            diagnostic.Id.ShouldBe(DiscardedOutcomeAnalyzer.DiagnosticId);
        }
        var actual = diagnostics
            .Select(diagnostic => (diagnostic.Location.SourceTree?.FilePath, diagnostic.Location.SourceSpan))
            .OrderBy(location => location.SourceSpan.Start)
            .ToArray();
        actual.ShouldBe(expected.Select(span => ((string?)path, span)).OrderBy(location => location.span.Start).ToArray());
    }
}

internal sealed record Markup(string Source, ImmutableArray<TextSpan> Spans);
