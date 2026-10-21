using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rezultat;

internal static class ScalarCosts
{
    private static readonly ImmutableArray<string> _issues = ["a", "b"];
    private static readonly Validation<int, string> _valid = Validation<int, string>.Valid(6);
    private static readonly Validation<int, string> _invalid = Validation<int, string>.InvalidMany(_issues);
    private static readonly Result<int, string> _ok = Result<int, string>.Ok(7);
    private static readonly Result<int, string> _error = Result<int, string>.Error("e");
    private static readonly Result<Large, string> _okLarge = Result<Large, string>.Ok(CostMeasurement.CreateLarge(3));
    private static readonly Result<string, string> _okReference = Result<string, string>.Ok("reference");
    private static readonly Presence<int> _value = Presence<int>.Value(5);
    private static readonly Presence<Large> _valueLarge = Presence<Large>.Value(CostMeasurement.CreateLarge(3));
    private static readonly Presence<string> _valueReference = Presence<string>.Value("reference");

    private static Validation<int, string> _validationSink;
    private static Result<int, string> _resultSink;
    private static Result<Large, string> _largeResultSink;
    private static Result<string, string> _referenceResultSink;
    private static Result<int, ImmutableArray<string>> _bridgedSink;
    private static Result<Presence<int>, string> _traversedSink;
    private static Presence<int> _presenceSink;
    private static Presence<string> _presenceStringSink;
    private static Presence<Large> _presenceLargeSink;
    private static long _counter;

    public static int Run()
    {
        (string Name, Action<int> Library, Action<int> Recipe, Func<bool> Expected)[] pairs =
        [
            ("Validation.Map Valid vs AndThen+Valid", ValidationMap, ValidationMapRecipe,
                static () => _validationSink == Validation<int, string>.Valid(7)),
            ("Validation.Map Invalid vs AndThen+Valid", ValidationMapInvalid, ValidationMapInvalidRecipe,
                static () => KeepsIssueBuffer(_validationSink)),
            ("Validation.Ensure false (singleton issue) vs AndThen", ValidationEnsureFails, ValidationEnsureFailsRecipe,
                static () => _validationSink == Validation<int, string>.Invalid("negative")),
            ("Validation.Ensure Invalid vs AndThen", ValidationEnsureInvalid, ValidationEnsureInvalidRecipe,
                static () => KeepsIssueBuffer(_validationSink)),
            ("Presence.AndThen vs Match", PresenceAndThen, PresenceAndThenRecipe,
                static () => _presenceSink == Presence<int>.Value(5)),
            ("Presence.FilterToMissing vs Match", PresenceFilter, PresenceFilterRecipe,
                static () => _presenceSink == Presence<int>.Value(5)),
            ("Result.Inspect prepared int vs ObserveSuccess", Inspect, InspectRecipe,
                static () => _resultSink == Result<int, string>.Ok(7) && _counter == 7),
            ("Result.Inspect captured int vs ObserveSuccess", InspectCaptured, InspectCapturedRecipe,
                static () => _resultSink == Result<int, string>.Ok(7) && _counter == 7),
            ("Result.Inspect 512-byte payload vs ObserveSuccess", InspectLarge, InspectLargeRecipe,
                static () => _largeResultSink.TryGetValue(out var value, out _) && value[0] == 3 && _counter == 3),
            ("Result.Inspect reference payload vs ObserveSuccess", InspectReference, InspectReferenceRecipe,
                static () => _referenceResultSink.TryGetValue(out var value, out _) && ReferenceEquals(value, "reference") && _counter == 9),
            ("Presence.FilterToNull 512-byte payload vs Match", FilterLarge, FilterLargeRecipe,
                static () => _presenceLargeSink.TryGetValue(out var value) && value[0] == 3),
            ("Presence.FilterToNull reference payload vs Match", FilterReference, FilterReferenceRecipe,
                static () => _presenceStringSink.TryGetValue(out var value) && ReferenceEquals(value, "reference")),
            ("ToValidation Error vs Match", ToValidationError, ToValidationErrorRecipe,
                static () => _validationSink == Validation<int, string>.Invalid("e")),
            ("ToResult Invalid vs Match", ToResultInvalid, ToResultInvalidRecipe,
                static () => !_bridgedSink.TryGetValue(out _, out var issues) && SameBuffer(issues)),
            ("Presence.Traverse vs ConvertField", PresenceTraverse, PresenceTraverseRecipe,
                static () => _traversedSink == Result<Presence<int>, string>.Ok(Presence<int>.Value(6))),
        ];

        var failures = 0;
        foreach (var (name, library, recipe, expected) in pairs)
        {
            failures += Validate($"{name} (library)", library, expected) + Validate($"{name} (recipe)", recipe, expected);
        }

        failures += Validate("Validation.Invalid singleton baseline", DirectInvalid, static () => _validationSink == Validation<int, string>.Invalid("e"));

        if (failures != 0)
        {
            Console.WriteLine("FAIL scalar cost workloads differ from their expected results; comparisons skipped");
            return failures;
        }

        foreach (var (name, library, recipe, _) in pairs)
        {
            var (libraryBytes, libraryNanoseconds) = CostMeasurement.Measure(library);
            var (recipeBytes, recipeNanoseconds) = CostMeasurement.Measure(recipe);
            Console.WriteLine($"INFO {name}: allocation {libraryBytes} vs {recipeBytes} B/op (no budget)");
            Console.WriteLine($"INFO {name}: time {libraryNanoseconds:F1} vs {recipeNanoseconds:F1} ns/op (no budget)");
        }

        var (singletonBytes, singletonNanoseconds) = CostMeasurement.Measure(DirectInvalid);
        Console.WriteLine($"INFO Validation.Invalid singleton baseline: allocation {singletonBytes} B/op, time {singletonNanoseconds:F1} ns/op (no budget)");

        _counter += _validationSink.IsInitialized ? 1 : 0;
        _counter += _resultSink.IsInitialized && _largeResultSink.IsInitialized && _referenceResultSink.IsInitialized ? 1 : 0;
        _counter += _bridgedSink.IsInitialized && _traversedSink.IsInitialized ? 1 : 0;
        _counter += _presenceSink.IsValue || _presenceStringSink.IsValue || _presenceLargeSink.IsValue ? 1 : 0;
        Console.WriteLine($"scalar cost sink: {_counter}");
        return 0;
    }

    private static int Validate(string name, Action<int> kernel, Func<bool> expected)
    {
        _validationSink = default;
        _resultSink = default;
        _largeResultSink = default;
        _referenceResultSink = default;
        _bridgedSink = default;
        _traversedSink = default;
        _presenceSink = default;
        _presenceStringSink = default;
        _presenceLargeSink = default;
        _counter = 0;
        kernel(1);
        var passed = expected();
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name} produces the expected outcome");
        return passed ? 0 : 1;
    }

    private static bool KeepsIssueBuffer(Validation<int, string> validation) =>
        !validation.TryGetValue(out _, out var issues) && SameBuffer(issues);

    private static bool SameBuffer(ImmutableArray<string> issues) =>
        ReferenceEquals(ImmutableCollectionsMarshal.AsArray(issues), ImmutableCollectionsMarshal.AsArray(_issues));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidationMap(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = _valid.Map(static x => x + 1);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidationMapRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = _valid.AndThen(static x => Validation<int, string>.Valid(x + 1));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidationMapInvalid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = _invalid.Map(static x => x + 1);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidationMapInvalidRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = _invalid.AndThen(static x => Validation<int, string>.Valid(x + 1));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidationEnsureFails(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = _valid.Ensure(static x => x < 0, static _ => "negative");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidationEnsureFailsRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = _valid.AndThen(static x => x < 0 ? Validation<int, string>.Valid(x) : Validation<int, string>.Invalid("negative"));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidationEnsureInvalid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = _invalid.Ensure(static x => x < 0, static _ => "negative");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidationEnsureInvalidRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = _invalid.AndThen(static x => x < 0 ? Validation<int, string>.Valid(x) : Validation<int, string>.Invalid("negative"));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PresenceAndThen(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _presenceSink = _value.AndThen(static x => x > 0 ? Presence<int>.Value(x) : Presence<int>.Null);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PresenceAndThenRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _presenceSink = _value.Match(
                static x => x > 0 ? Presence<int>.Value(x) : Presence<int>.Null,
                static () => Presence<int>.Null,
                static () => Presence<int>.Missing);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PresenceFilter(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _presenceSink = _value.FilterToMissing(static x => x > 0);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PresenceFilterRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var source = _value;
            _presenceSink = source.Match(
                static x => x > 0 ? Presence<int>.Value(x) : Presence<int>.Missing,
                static () => Presence<int>.Null,
                static () => Presence<int>.Missing);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Inspect(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _resultSink = _ok.Inspect(static x => _counter += x);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InspectRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _resultSink = ObserveSuccess(_ok, static x => _counter += x);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InspectCaptured(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var offset = i;
            _resultSink = _ok.Inspect(x => _counter += x + offset);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InspectCapturedRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var offset = i;
            _resultSink = ObserveSuccess(_ok, x => _counter += x + offset);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InspectLarge(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _largeResultSink = _okLarge.Inspect(static x => _counter += x[0]);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InspectLargeRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _largeResultSink = ObserveSuccess(_okLarge, static x => _counter += x[0]);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InspectReference(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _referenceResultSink = _okReference.Inspect(static x => _counter += x.Length);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InspectReferenceRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _referenceResultSink = ObserveSuccess(_okReference, static x => _counter += x.Length);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FilterLarge(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _presenceLargeSink = _valueLarge.FilterToNull(static x => x[0] == 3);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FilterLargeRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var source = _valueLarge;
            _presenceLargeSink = source.TryGetValue(out var value) ? value[0] == 3 ? source : Presence<Large>.Null : source;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FilterReference(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _presenceStringSink = _valueReference.FilterToNull(static x => x.Length > 0);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FilterReferenceRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var source = _valueReference;
            _presenceStringSink = source.TryGetValue(out var value) ? value.Length > 0 ? source : Presence<string>.Null : source;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ToValidationError(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = _error.ToValidation();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ToValidationErrorRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = _error.Match(Validation<int, string>.Valid, Validation<int, string>.Invalid);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DirectInvalid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationSink = Validation<int, string>.Invalid("e");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ToResultInvalid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _bridgedSink = _invalid.ToResult();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ToResultInvalidRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _bridgedSink = _invalid.Match(Result<int, ImmutableArray<string>>.Ok, Result<int, ImmutableArray<string>>.Error);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PresenceTraverse(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _traversedSink = _value.Traverse(static x => Result<int, string>.Ok(x + 1));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PresenceTraverseRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _traversedSink = _value.Match(
                static x => Result<int, string>.Ok(x + 1).Map(Presence<int>.Value),
                static () => Result<Presence<int>, string>.Ok(Presence<int>.Null),
                static () => Result<Presence<int>, string>.Ok(Presence<int>.Missing));
        }
    }

    private static Result<T, E> ObserveSuccess<T, E>(Result<T, E> result, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.TryGetValue(out var value, out _))
        {
            action(value);
        }

        return result;
    }
}
