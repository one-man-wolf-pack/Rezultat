using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Rezultat.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedOutcomeAnalyzer : DiagnosticAnalyzer {
    public const string DiagnosticId = "RZT0001";

    private const string CarrierAssembly = "Rezultat";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Result or Validation outcome is discarded",
        "The {0} outcome is discarded here; store, return, pass or handle it, or discard it explicitly with '_ = ...'",
        "Reliability",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Reports a Rezultat Result or Validation value, or a Task or ValueTask of one, that C# drops at this "
            + "expression. An explicit discard, return, assignment or argument does not trigger it. The rule does not "
            + "analyze how a kept outcome is handled later.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context) {
        var compilation = context.Compilation;
        bool IsCore(IAssemblySymbol assembly) => assembly.Name == CarrierAssembly;
        var result = Definition(compilation, "Rezultat.Result`2", IsCore);
        var validation = Definition(compilation, "Rezultat.Validation`2", IsCore);
        if (result is null && validation is null) {
            return;
        }

        // The framework wrappers are defined next to System.Object (System.Runtime for the net10.0 consumers
        // of the core); a consumer-declared or referenced homonym must neither be reported nor hide them.
        var corlib = compilation.ObjectType.ContainingAssembly;
        bool IsCorlib(IAssemblySymbol assembly) => SymbolEqualityComparer.Default.Equals(assembly, corlib);
        var carriers = new Carriers(
            result,
            validation,
            Definition(compilation, "System.Threading.Tasks.Task`1", IsCorlib),
            Definition(compilation, "System.Threading.Tasks.ValueTask`1", IsCorlib));
        context.RegisterOperationAction(carriers.AnalyzeStatement, OperationKind.ExpressionStatement);
    }

    // Not GetTypeByMetadataName: it prefers a same-named type declared in the consumer and returns null when
    // several references define one. A missing or ambiguous definition disables only that type.
    private static INamedTypeSymbol? Definition(Compilation compilation, string metadataName, Func<IAssemblySymbol, bool> owner) {
        var candidates = compilation.GetTypesByMetadataName(metadataName)
            .Where(type => type.ContainingAssembly is { } assembly && owner(assembly))
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private sealed class Carriers(
        INamedTypeSymbol? result,
        INamedTypeSymbol? validation,
        INamedTypeSymbol? task,
        INamedTypeSymbol? valueTask) {
        public void AnalyzeStatement(OperationAnalysisContext context) {
            var expression = ((IExpressionStatementOperation)context.Operation).Operation;
            if (expression is not (IInvocationOperation or IObjectCreationOperation or IAwaitOperation or IConditionalAccessOperation)) {
                return;
            }

            var value = expression;
            while (value is IConditionalAccessOperation conditional) {
                value = conditional.WhenNotNull;
            }

            var type = value.Type as INamedTypeSymbol;
            if (value is not IAwaitOperation && type is not null && (Is(type, task) || Is(type, valueTask))) {
                type = type.TypeArguments[0] as INamedTypeSymbol;
            }

            if (type is not null && (Is(type, result) || Is(type, validation))) {
                context.ReportDiagnostic(Diagnostic.Create(
                    _rule,
                    expression.Syntax.GetLocation(),
                    type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        private static bool Is(INamedTypeSymbol type, INamedTypeSymbol? definition) =>
            definition is not null && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, definition);
    }
}
