using Microsoft.CodeAnalysis;
using static Rezultat.Analyzers.Tests.AnalyzerHarness;

namespace Rezultat.Analyzers.Tests;

public sealed class CarrierIdentityTests {
    private const string SameNamedCarriers = """
        namespace Rezultat
        {
            public readonly struct Result<T, TError>
            {
                public static Result<T, TError> Ok(T value) => default;
            }

            public readonly struct Validation<T, TIssue>
            {
                public static Validation<T, TIssue> Valid(T value) => default;
            }
        }
        """;

    private const string SameNamedCalls = """
        public static class Consumer
        {
            public static void M()
            {
                Rezultat.Result<int, string>.Ok(1);
                Rezultat.Validation<int, string>.Valid(1);
            }
        }
        """;

    [Fact]
    public async Task IgnoresSameNamedCarriersFromAnotherAssembly() {
        var foreign = Library("Foreign", SameNamedCarriers, Framework);

        var diagnostics = await AnalyzeAsync(Compilation(Framework.Add(foreign), (ConsumerPath, SameNamedCalls)));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task IgnoresSameNamedCarriersDeclaredInTheConsumer() {
        var diagnostics = await AnalyzeAsync(Compilation(
            Framework, ("Carriers.cs", SameNamedCarriers), (ConsumerPath, SameNamedCalls)));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task KeepsCanonicalCarriersWhenTheConsumerDeclaresSameNamedTypes() {
        var markup = Parse("""
            public static class Consumer
            {
                public static void M()
                {
                    [|global::Rezultat.Result<int, string>.Ok(1)|];
                    Local.Result<int, string>.Ok(1);
                }
            }
            """);
        string local = """
            namespace Local
            {
                public readonly struct Result<T, TError>
                {
                    public static Result<T, TError> Ok(T value) => default;
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(Compilation(WithCore, ("Local.cs", local), (ConsumerPath, markup.Source)));

        AssertReported(diagnostics, ConsumerPath, markup.Spans);
    }

    [Fact]
    public async Task ReportsNothingWithoutTheCoreAssembly() {
        string source = """
            using System.Threading.Tasks;

            public static class Consumer
            {
                public static async Task M()
                {
                    Task.FromResult(1);
                    await Task.FromResult(1);
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(Compilation(Framework, (ConsumerPath, source)));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReportsNothingWhenTheCanonicalCarrierIsAmbiguous() {
        var producers = Library("Producers", """
            using Rezultat;

            public static class Orders
            {
                public static Result<int, string> Submit() => Result<int, string>.Ok(1);
            }
            """, WithCore);
        string source = """
            public static class Consumer
            {
                public static void M()
                {
                    Orders.Submit();
                    global::Rezultat.Result<int, string>.Ok(1);
                }
            }
            """;
        var compilation = Compilation(
            "Rezultat", WithCore.Add(producers), ("Carriers.cs", SameNamedCarriers), (ConsumerPath, source));
        compilation.GetTypesByMetadataName("Rezultat.Result`2").Length.ShouldBe(2);

        var diagnostics = await AnalyzeAsync(compilation);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task KeepsTheOtherCarrierWhenOneCarrierIsAmbiguous() {
        string localResult = """
            namespace Rezultat
            {
                public readonly struct Result<T, TError>
                {
                    public static Result<T, TError> Ok(T value) => default;
                }
            }
            """;
        var markup = Parse("""
            public static class Consumer
            {
                public static void M()
                {
                    global::Rezultat.Result<int, string>.Ok(1);
                    [|global::Rezultat.Validation<int, string>.Valid(1)|];
                }
            }
            """);
        var compilation = Compilation("Rezultat", WithCore, ("Carriers.cs", localResult), (ConsumerPath, markup.Source));
        compilation.GetTypesByMetadataName("Rezultat.Result`2").Length.ShouldBe(2);

        var diagnostics = await AnalyzeAsync(compilation);

        AssertReported(diagnostics, ConsumerPath, markup.Spans);
    }

    private const string SameNamedWrappers = """
        namespace System.Threading.Tasks
        {
            public sealed class Task<T> { }

            public readonly struct ValueTask<T> { }
        }

        public static class Fakes
        {
            public static System.Threading.Tasks.Task<Rezultat.Result<int, string>> SaveAsync() => new();

            public static System.Threading.Tasks.ValueTask<Rezultat.Validation<int, string>> ValidateAsync() => default;
        }
        """;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RecognizesOnlyFrameworkTaskWrappersNextToSameNamedOnes(bool declaredInConsumer) {
        var producers = Library("Producers", """
            using System.Threading.Tasks;
            using Rezultat;

            public static class Orders
            {
                public static Task<Result<int, string>> SaveAsync() => Task.FromResult(Result<int, string>.Ok(1));

                public static ValueTask<Validation<int, string>> ValidateAsync() => new(Validation<int, string>.Valid(1));
            }
            """, WithCore);
        var markup = Parse("""
            public static class Consumer
            {
                public static void M()
                {
                    Fakes.SaveAsync();
                    Fakes.ValidateAsync();
                    [|Orders.SaveAsync()|];
                    [|Orders.ValidateAsync()|];
                }
            }
            """);
        var references = WithCore.Add(producers);
        var compilation = declaredInConsumer
            ? Compilation(references, ("Wrappers.cs", SameNamedWrappers), (ConsumerPath, markup.Source))
            : Compilation(references.Add(Library("Wrappers", SameNamedWrappers, WithCore)), (ConsumerPath, markup.Source));

        var diagnostics = await AnalyzeAsync(compilation);

        AssertReported(diagnostics, ConsumerPath, markup.Spans);
    }

    [Theory]
    [InlineData("Generated.g.cs", "")]
    [InlineData("Generated.designer.cs", "")]
    [InlineData("Ordinary.cs", "// <auto-generated/>\n")]
    public async Task ExcludesGeneratedCode(string path, string header) {
        var markup = Parse("""
            using Rezultat;

            public static partial class Consumer
            {
                public static void Authored() { [|Result<int, string>.Ok(1)|]; }
            }
            """);
        string generated = header + """
            using Rezultat;

            public static partial class Consumer
            {
                public static void Generated() { Result<int, string>.Ok(1); }
            }
            """;

        var diagnostics = await AnalyzeAsync(Compilation(WithCore, (ConsumerPath, markup.Source), (path, generated)));

        AssertReported(diagnostics, ConsumerPath, markup.Spans);
    }

    [Fact]
    public async Task ExcludesCodeMarkedAsGenerated() {
        string source = """
            using Rezultat;

            [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
            public static class Consumer
            {
                public static void M() { Result<int, string>.Ok(1); }
            }
            """;

        var diagnostics = await AnalyzeAsync(Compilation(WithCore, (ConsumerPath, source)));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void SuppliesOnlyTheRztWarningDescriptor() {
        var descriptor = new DiscardedOutcomeAnalyzer().SupportedDiagnostics.ShouldHaveSingleItem();

        descriptor.Id.ShouldBe("RZT0001");
        descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.ShouldBeTrue();
        descriptor.CustomTags.ShouldBeEmpty();
    }
}
