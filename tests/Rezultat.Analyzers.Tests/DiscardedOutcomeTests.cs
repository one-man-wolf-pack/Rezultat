using System.Globalization;
using Microsoft.CodeAnalysis;
using static Rezultat.Analyzers.Tests.AnalyzerHarness;

namespace Rezultat.Analyzers.Tests;

public sealed class DiscardedOutcomeTests
{
    private const string Prelude = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Rezultat;

        public sealed partial class Consumer
        {
            private Result<int, string> _field;

            public Consumer? Next { get; set; }

            public Result<int, string> Save() => Result<int, string>.Error("error");

            public Validation<int, string> Validate() => Validation<int, string>.Invalid("issue");

            public Result<Unit, string> Run() => Result<Unit, string>.Ok(Unit.Value);

            public Result<int, string> Default() => default;

            public Task<Result<int, string>> SaveAsync() => Task.FromResult(Save());

            public ValueTask<Validation<int, string>> ValidateAsync() => new(Validate());

            public Task<Task<Result<int, string>>> NestedAsync() => Task.FromResult(SaveAsync());

            public ValueTask<Task<Result<int, string>>> NestedValueAsync() => new(SaveAsync());

            public static Result<int, string> Wrap(Result<int, string> outcome) => outcome;

            public static void Consume(Result<int, string> outcome) { }

            public static void RunStep(Action step) => step();

            public static void RunStepAsync(Func<Task> step) { }

            public static void Defer(Func<Result<int, string>> producer) { }

            public Maybe<int> Find() => Maybe<int>.Some(1);

            public Presence<int> Patch() => Presence<int>.Value(1);
        }

        """;

    public static TheoryData<string, string> Lost => new()
    {
        { "bare Result call", "void M() { [|Save()|]; }" },
        { "bare Validation call", "void M() { [|Validate()|]; }" },
        { "ready Ok factory", "void M() { [|Result<int, string>.Ok(1)|]; }" },
        { "ready Error factory", "void M() { [|Result<int, string>.Error(\"e\")|]; }" },
        { "ready Valid factory", "void M() { [|Validation<int, string>.Valid(1)|]; }" },
        { "ready Invalid factory", "void M() { [|Validation<int, string>.Invalid(\"i\")|]; }" },
        { "new Result", "void M() { [|new Result<int, string>()|]; }" },
        { "new Validation", "void M() { [|new Validation<int, string>()|]; }" },
        { "method returning default", "void M() { [|Default()|]; }" },
        { "Unit payload", "void M() { [|Run()|]; }" },
        { "Task payload", "void M() { [|Result<Task<int>, string>.Ok(Task.FromResult(1))|]; }" },
        { "nullable payload", "void M() { [|Result<string?, string?>.Ok(null)|]; }" },
        { "delegate invocation", "void M(Func<Result<int, string>> producer) { [|producer()|]; }" },
        { "static producer", "void M() { [|Wrap(Save())|]; }" },
        { "nested await argument reported once", "async Task M() { [|Wrap(await SaveAsync())|]; }" },
        { "expression-bodied void method", "void M() => [|Save()|];" },
        { "expression-bodied local function", "void M() { void Local() => [|Save()|]; Local(); }" },
        { "expression-bodied setter", "int P { set => [|Save()|]; }" },
        { "expression-bodied constructor", "Consumer(int value) => [|Save()|];" },
        { "Action expression lambda", "void M() { RunStep(() => [|Save()|]); }" },
        { "Action block lambda", "void M() { RunStep(() => { [|Save()|]; }); }" },
        { "Action anonymous method", "void M() { RunStep(delegate { [|Save()|]; }); }" },
        { "async Func<Task> lambda", "void M() { RunStepAsync(async () => [|await SaveAsync()|]); }" },
        { "async void Action lambda", "void M() { RunStep(async () => [|await SaveAsync()|]); }" },
        { "await Task", "async Task M() { [|await SaveAsync()|]; }" },
        { "await ValueTask", "async Task M() { [|await ValidateAsync()|]; }" },
        { "await ConfigureAwait", "async Task M() { [|await SaveAsync().ConfigureAwait(false)|]; }" },
        { "await ValueTask ConfigureAwait", "async Task M() { [|await ValidateAsync().ConfigureAwait(false)|]; }" },
        { "unawaited Task in sync method", "void M() { [|SaveAsync()|]; }" },
        { "unawaited ValueTask in sync method", "void M() { [|ValidateAsync()|]; }" },
        { "unawaited Task in async method", "async Task M() { [|SaveAsync()|]; await Task.Yield(); }" },
        { "conditional access", "void M() { [|Next?.Save()|]; }" },
        { "nested conditional access", "void M() { [|Next?.Next?.Validate()|]; }" },
        { "conditional access to Task", "void M() { [|Next?.SaveAsync()|]; }" },
        { "conditional access to ValueTask", "void M() { [|Next?.ValidateAsync()|]; }" },
        { "guarded await", "async Task M() { if (Next is not null) [|await Next.SaveAsync()|]; }" },
        { "two losses in one body", "void M() { [|Save()|]; [|Validate()|]; }" },
    };

    public static TheoryData<string, string> Kept => new()
    {
        { "explicit discard", "void M() { _ = Save(); }" },
        { "explicit discard of await", "async Task M() { _ = await SaveAsync(); }" },
        { "explicit discard of task", "void M() { _ = SaveAsync(); }" },
        { "explicit discard in Action lambda", "void M() { RunStep(() => { _ = Save(); }); }" },
        { "return", "Result<int, string> M() => Save();" },
        { "async return", "async Task<Result<int, string>> M() => await SaveAsync();" },
        { "return of default", "Result<int, string> M() { return default; }" },
        { "local", "void M() { var unused = Save(); }" },
        { "field", "void M() { _field = Save(); }" },
        { "compound coalescing assignment", "void M(Result<int, string>? held) { held ??= Save(); }" },
        { "argument", "void M() { Consume(Save()); }" },
        { "Func lambda returns the outcome", "void M() { Defer(() => Save()); }" },
        { "boxing to object", "void M() { object boxed = Save(); }" },
        { "dynamic receiver", "void M(dynamic receiver) { receiver.Save(); }" },
        { "nullable carrier", "void M(Func<Result<int, string>?> find) { find(); }" },
        { "awaited nullable carrier", "async Task M(Func<Task<Validation<int, string>?>> find) { await find(); }" },
        { "two Task levels", "void M() { NestedAsync(); }" },
        { "ValueTask over Task", "void M() { NestedValueAsync(); }" },
        { "await yields a Task of the outcome", "async Task M() { await NestedAsync(); }" },
        { "collection of outcomes", "void M() { new List<Result<int, string>> { Save() }; }" },
        { "array of outcomes", "void M() { Array.Empty<Validation<int, string>>(); }" },
        { "Maybe", "void M() { Find(); Maybe<int>.Some(1); }" },
        { "Presence", "void M() { Patch(); Presence<int>.Value(1); }" },
        { "flag observation", "void M() { _ = Save().IsSuccess; }" },
        { "void call", "void M() { Consume(default); }" },
        { "foreign same-named Result", "void M() { Other.Result<int, string>.Make(); }" },
    };

    [Theory]
    [MemberData(nameof(Lost))]
    public async Task ReportsEachDirectlyDiscardedOutcome(string name, string member)
    {
        var markup = Parse(Prelude + Wrap(member));
        Assert.True(markup.Spans.Length > 0, name);

        var diagnostics = await AnalyzeAsync(Compilation(WithCore, (ConsumerPath, markup.Source)));

        AssertReported(diagnostics, ConsumerPath, markup.Spans);
    }

    [Theory]
    [MemberData(nameof(Kept))]
    public async Task DoesNotReportKeptOrOutOfScopeValues(string name, string member)
    {
        var markup = Parse(Prelude + Wrap(member));

        var diagnostics = await AnalyzeAsync(Compilation(WithCore, (ConsumerPath, markup.Source)));

        Assert.True(diagnostics.IsEmpty, $"{name}: {string.Join("; ", diagnostics)}");
    }

    [Fact]
    public async Task ReportsOutcomeOfProducerFromAnotherAssembly()
    {
        var producers = Library("Producers", """
            using System.Threading.Tasks;
            using Rezultat;

            namespace Producers;

            public static class Orders
            {
                public static Result<int, string> Submit(int order) => Result<int, string>.Ok(order);

                public static Task<Validation<int, string>> CheckAsync(int order) => Task.FromResult(Validation<int, string>.Valid(order));
            }
            """, WithCore);
        var markup = Parse("""
            using System.Threading.Tasks;
            using Producers;

            public static class Consumer
            {
                public static async Task M()
                {
                    [|Orders.Submit(1)|];
                    [|await Orders.CheckAsync(1)|];
                    [|Orders.CheckAsync(2)|];
                    _ = Orders.Submit(3);
                }
            }
            """);

        var diagnostics = await AnalyzeAsync(Compilation(WithCore.Add(producers), (ConsumerPath, markup.Source)));

        AssertReported(diagnostics, ConsumerPath, markup.Spans);
    }

    [Fact]
    public async Task ReportsMessageWithTheDiscardedCarrierType()
    {
        var markup = Parse(Prelude + Wrap("async Task M() { [|await ValidateAsync()|]; }"));

        var diagnostic = Assert.Single(await AnalyzeAsync(Compilation(WithCore, (ConsumerPath, markup.Source))));

        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Validation<int, string>", message, StringComparison.Ordinal);
        Assert.Contains("_ =", message, StringComparison.Ordinal);
    }

    private static string Wrap(string member) => $$"""
        public sealed partial class Consumer
        {
            {{member}}
        }

        namespace Other
        {
            public readonly struct Result<T, E>
            {
                public static Result<T, E> Make() => default;
            }
        }
        """;
}
