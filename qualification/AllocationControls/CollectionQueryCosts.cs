using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rezultat;
using Rezultat.Collections;
using Rezultat.Linq;

internal static class CollectionQueryCosts
{
    private const int Length = 64;
    private const int ExpectedSum = Length * (Length - 1) / 2;

    private static readonly ImmutableArray<string> _singleIssues = ["a", "b"];
    private static readonly int[] _numbers = [.. Enumerable.Range(0, Length)];
    private static readonly Result<int, string>[] _oks = [.. _numbers.Select(static n => Result<int, string>.Ok(n))];
    private static readonly Maybe<int>[] _somes = [.. _numbers.Select(static n => Maybe<int>.Some(n))];
    private static readonly Validation<int, string>[] _valids = [.. _numbers.Select(static n => Validation<int, string>.Valid(n))];
    private static readonly Validation<int, string>[] _mixed = [.. _numbers.Select(static n => n % 8 == 3
        ? Validation<int, string>.InvalidMany([$"{n}a", $"{n}b"])
        : Validation<int, string>.Valid(n))];
    private static readonly Validation<int, string>[] _oneInvalid = [.. _numbers.Select(static n => n == 5
        ? Validation<int, string>.InvalidMany(_singleIssues)
        : Validation<int, string>.Valid(n))];
    private static readonly Validation<Large, string>[] _largeValids = [.. _numbers.Select(static n => Validation<Large, string>.Valid(CostMeasurement.CreateLarge(n)))];
    private static readonly Validation<string, string>[] _referenceValids = [.. _numbers.Select(static n => Validation<string, string>.Valid($"v{n}"))];
    private static readonly ImmutableArray<string> _mixedIssues = [.. _numbers.Where(static n => n % 8 == 3).SelectMany(static n => new[] { $"{n}a", $"{n}b" })];
    private static readonly Result<Large, string> _okLarge = Result<Large, string>.Ok(CostMeasurement.CreateLarge(3));
    private static readonly Maybe<string> _someReference = Maybe<string>.Some("reference");

    private static Result<int[], string> _resultArraySink;
    private static Maybe<int[]> _maybeArraySink;
    private static Validation<int[], string> _validationArraySink;
    private static Validation<Unit, string> _unitSink;
    private static Result<int, string> _resultSink;
    private static Result<long, string> _longResultSink;
    private static Maybe<int> _maybeSink;
    private static int _threshold = -1;

    public static int Run()
    {
        (string Name, (string Form, Action<int> Kernel)[] Forms, Func<bool> Expected)[] comparisons =
        [
            ("Result.Sequence vs identity Traverse over 64 Ok", [("Sequence", ResultSequence), ("Traverse", ResultTraverse)],
                static () => _resultArraySink.TryGetValue(out var values, out _) && values.Sum() == ExpectedSum && values.Length == Length),
            ("Maybe.Sequence vs identity Traverse over 64 Some", [("Sequence", MaybeSequence), ("Traverse", MaybeTraverse)],
                static () => _maybeArraySink.TryGetValue(out var values) && values.Sum() == ExpectedSum && values.Length == Length),
            ("Validation.Sequence vs identity Traverse over 64 with 8 Invalid", [("Sequence", ValidationSequence), ("Traverse", ValidationTraverse)],
                static () => !_validationArraySink.TryGetValue(out _, out var issues) && issues.SequenceEqual(_mixedIssues)),
            ("ValidateAll vs Traverse+Map over 64 Valid int", [("ValidateAll", ValidateAllValid), ("Traverse+Map", ValidateAllValidRecipe)],
                static () => _unitSink == Validation<Unit, string>.Valid(Unit.Value)),
            ("ValidateAll vs Traverse+Map over 64 Valid 512-byte payload", [("ValidateAll", ValidateAllLarge), ("Traverse+Map", ValidateAllLargeRecipe)],
                static () => _unitSink == Validation<Unit, string>.Valid(Unit.Value)),
            ("ValidateAll vs Traverse+Map over 64 Valid reference payload", [("ValidateAll", ValidateAllReference), ("Traverse+Map", ValidateAllReferenceRecipe)],
                static () => _unitSink == Validation<Unit, string>.Valid(Unit.Value)),
            ("ValidateAll vs Traverse+Map vs issue-only builder over 64 with 8 Invalid",
                [("ValidateAll", ValidateAllMixed), ("Traverse+Map", ValidateAllMixedRecipe), ("builder", ValidateAllMixedBuilder)],
                static () => !_unitSink.TryGetValue(out _, out var issues) && issues.SequenceEqual(_mixedIssues)),
            ("ValidateAll vs Traverse+Map over 64 with one Invalid", [("ValidateAll", ValidateAllOne), ("Traverse+Map", ValidateAllOneRecipe)],
                static () => !_unitSink.TryGetValue(out _, out var issues) && SameBuffer(issues)),
            ("ValidateAll selector prepared vs Traverse+Map over 64 int", [("ValidateAll", ValidateAllSelector), ("Traverse+Map", ValidateAllSelectorRecipe)],
                static () => _unitSink == Validation<Unit, string>.Valid(Unit.Value)),
            ("ValidateAll selector captured vs Traverse+Map over 64 int", [("ValidateAll", ValidateAllCaptured), ("Traverse+Map", ValidateAllCapturedRecipe)],
                static () => _unitSink == Validation<Unit, string>.Valid(Unit.Value)),
            ("Result two from int", [("query", ResultQuery2), ("direct", ResultDirect2), ("native", ResultNative2)],
                static () => _resultSink == Result<int, string>.Ok(15)),
            ("Result three from and let int", [("query", ResultQuery3), ("direct", ResultDirect3), ("native", ResultNative3)],
                static () => _resultSink == Result<int, string>.Ok(27)),
            ("Result two from first Error int", [("query", ResultQueryError), ("native", ResultNativeError)],
                static () => _resultSink == Result<int, string>.Error("e")),
            ("Result two from 512-byte payload", [("query", ResultQueryLarge), ("native", ResultNativeLarge)],
                static () => _longResultSink == Result<long, string>.Ok(11)),
            ("Maybe from-where-from reference payload", [("query", MaybeQuery), ("direct", MaybeDirect), ("native", MaybeNative)],
                static () => _maybeSink == Maybe<int>.Some(18)),
        ];

        var failures = 0;
        foreach (var (name, forms, expected) in comparisons)
        {
            foreach (var (form, kernel) in forms)
            {
                failures += Validate($"{name} ({form})", kernel, expected);
            }
        }

        if (failures != 0)
        {
            Console.WriteLine("FAIL collection/query cost workloads differ from their expected results; comparisons skipped");
            return failures;
        }

        foreach (var (name, forms, _) in comparisons)
        {
            var measured = forms.Select(static form => (form.Form, CostMeasurement.Measure(form.Kernel))).ToArray();
            Console.WriteLine($"INFO {name}: allocation {string.Join(" vs ", measured.Select(static m => $"{m.Form} {m.Item2.Bytes}"))} B/op (no budget)");
            Console.WriteLine($"INFO {name}: time {string.Join(" vs ", measured.Select(static m => $"{m.Form} {m.Item2.Nanoseconds:F1}"))} ns/op (no budget)");
        }

        Console.WriteLine($"collection/query cost sink: {(_threshold < 0 && _longResultSink.IsInitialized ? 1 : 0)}");
        return failures;
    }

    private static int Validate(string name, Action<int> kernel, Func<bool> expected)
    {
        _resultArraySink = default;
        _maybeArraySink = default;
        _validationArraySink = default;
        _unitSink = default;
        _resultSink = default;
        _longResultSink = default;
        _maybeSink = default;
        kernel(1);
        var passed = expected();
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name} produces the expected outcome");
        return passed ? 0 : 1;
    }

    private static bool SameBuffer(ImmutableArray<string> issues) =>
        ReferenceEquals(ImmutableCollectionsMarshal.AsArray(issues), ImmutableCollectionsMarshal.AsArray(_singleIssues));

    private static Result<int, string> Step(int value) => Result<int, string>.Ok(value + 5);

    private static Maybe<int> TextLength(string text) => Maybe<int>.Some(text.Length);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultSequence(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _resultArraySink = _oks.Sequence();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultTraverse(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _resultArraySink = _oks.Traverse(static item => item);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MaybeSequence(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _maybeArraySink = _somes.Sequence();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MaybeTraverse(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _maybeArraySink = _somes.Traverse(static item => item);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidationSequence(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationArraySink = _mixed.Sequence();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidationTraverse(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationArraySink = _mixed.Traverse(static item => item);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllValid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _valids.ValidateAll();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllValidRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _valids.Traverse(static item => item).Map(static _ => Unit.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllLarge(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _largeValids.ValidateAll();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllLargeRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _largeValids.Traverse(static item => item).Map(static _ => Unit.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllReference(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _referenceValids.ValidateAll();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllReferenceRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _referenceValids.Traverse(static item => item).Map(static _ => Unit.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllMixed(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _mixed.ValidateAll();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllMixedRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _mixed.Traverse(static item => item).Map(static _ => Unit.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllMixedBuilder(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var builder = ImmutableArray.CreateBuilder<string>();
            foreach (var outcome in _mixed)
            {
                if (!outcome.TryGetValue(out _, out var issues))
                {
                    builder.AddRange(issues);
                }
            }

            _unitSink = builder.Count == 0
                ? Validation<Unit, string>.Valid(Unit.Value)
                : Validation<Unit, string>.InvalidMany(builder.ToImmutable());
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllOne(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _oneInvalid.ValidateAll();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllOneRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _oneInvalid.Traverse(static item => item).Map(static _ => Unit.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllSelector(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _numbers.ValidateAll(static value => value >= 0 ? Validation<int, string>.Valid(value) : Validation<int, string>.Invalid("negative"));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllSelectorRecipe(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = _numbers.Traverse(static value => value >= 0 ? Validation<int, string>.Valid(value) : Validation<int, string>.Invalid("negative"))
                .Map(static _ => Unit.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllCaptured(int n)
    {
        var threshold = _threshold;
        for (var i = 0; i < n; i++)
        {
            _unitSink = _numbers.ValidateAll(value => value > threshold ? Validation<int, string>.Valid(value) : Validation<int, string>.Invalid("small"));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ValidateAllCapturedRecipe(int n)
    {
        var threshold = _threshold;
        for (var i = 0; i < n; i++)
        {
            _unitSink = _numbers.Traverse(value => value > threshold ? Validation<int, string>.Valid(value) : Validation<int, string>.Invalid("small"))
                .Map(static _ => Unit.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultQuery2(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _resultSink = from x in _oks[5] from y in Step(x) select x + y;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultDirect2(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _resultSink = _oks[5].AndThen(static x => Step(x).Map(y => x + y));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultNative2(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _resultSink = Native2(_oks[5]);
        }
    }

    private static Result<int, string> Native2(Result<int, string> source)
    {
        if (!source.TryGetValue(out var x, out var error))
        {
            return Result<int, string>.Error(error);
        }

        if (!Step(x).TryGetValue(out var y, out error))
        {
            return Result<int, string>.Error(error);
        }

        return Result<int, string>.Ok(x + y);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultQuery3(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _resultSink =
                from x in _oks[3]
                let doubled = x * 2
                from y in Step(x)
                from z in Step(y)
                select doubled + y + z;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultDirect3(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _resultSink = _oks[3].Map(static x => (x, doubled: x * 2))
                .AndThen(static t => Step(t.x).AndThen(y => Step(y).Map(z => t.doubled + y + z)));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultNative3(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _resultSink = Native3(_oks[3]);
        }
    }

    private static Result<int, string> Native3(Result<int, string> source)
    {
        if (!source.TryGetValue(out var x, out var error))
        {
            return Result<int, string>.Error(error);
        }

        var doubled = x * 2;
        if (!Step(x).TryGetValue(out var y, out error) || !Step(y).TryGetValue(out var z, out error))
        {
            return Result<int, string>.Error(error);
        }

        return Result<int, string>.Ok(doubled + y + z);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultQueryError(int n)
    {
        var error = Result<int, string>.Error("e");
        for (var i = 0; i < n; i++)
        {
            _resultSink = from x in error from y in Step(x) select x + y;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultNativeError(int n)
    {
        var error = Result<int, string>.Error("e");
        for (var i = 0; i < n; i++)
        {
            _resultSink = Native2(error);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultQueryLarge(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _longResultSink = from large in _okLarge from y in Step((int)large[0]) select large[0] + y;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResultNativeLarge(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _longResultSink = _okLarge.TryGetValue(out var large, out var error)
                ? Step((int)large[0]).TryGetValue(out var y, out error) ? Result<long, string>.Ok(large[0] + y) : Result<long, string>.Error(error)
                : Result<long, string>.Error(error);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MaybeQuery(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _maybeSink = from text in _someReference where text.Length > 3 from length in TextLength(text) select length * 2;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MaybeDirect(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _maybeSink = _someReference.Filter(static text => text.Length > 3).AndThen(static text => TextLength(text).Map(static length => length * 2));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MaybeNative(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _maybeSink = _someReference.TryGetValue(out var text) && text.Length > 3 && TextLength(text).TryGetValue(out var length)
                ? Maybe<int>.Some(length * 2)
                : Maybe<int>.None;
        }
    }
}
