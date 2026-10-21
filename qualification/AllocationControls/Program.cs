using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rezultat;

const int Iterations = 100_000;
const int WarmupRounds = 50;

Console.WriteLine($"runtime: {RuntimeInformation.FrameworkDescription}; os: {RuntimeInformation.OSDescription}; " +
    $"rid: {RuntimeInformation.RuntimeIdentifier}; arch: {RuntimeInformation.ProcessArchitecture}; " +
    $"server GC: {System.Runtime.GCSettings.IsServerGC}; TieredPGO switch: {(AppContext.TryGetSwitch("System.Runtime.TieredPGO", out var pgo) ? pgo.ToString() : "runtime default")}");

var optimized = IsOptimized(typeof(Kernels).Assembly) && IsOptimized(typeof(Result<,>).Assembly);
Console.WriteLine($"optimized consumer and Rezultat: {optimized}");

(string Name, Action<int> Kernel, bool Allocates)[] controls =
[
    ("empty control", Kernels.Empty, false),
    ("allocating control", Kernels.Allocating, true),
    ("Ok<int> construction", Kernels.OkInt, false),
    ("Error<int> construction", Kernels.ErrorInt, false),
    ("Ok<string> construction", Kernels.OkString, false),
    ("Error<string> construction", Kernels.ErrorString, false),
    ("Ok<Unit> construction", Kernels.OkUnit, false),
    ("IsInitialized/IsSuccess int", Kernels.StateInt, false),
    ("IsInitialized/IsSuccess string", Kernels.StateString, false),
    ("IsInitialized uninitialized", Kernels.StateUninitialized, false),
    ("TryGetValue Ok<int>", Kernels.TryGetOkInt, false),
    ("TryGetValue Error<int>", Kernels.TryGetErrorInt, false),
    ("TryGetValue Ok<string>", Kernels.TryGetOkString, false),
    ("TryGetValue Error<string>", Kernels.TryGetErrorString, false),
    ("Maybe Some<int> construction", Kernels.SomeInt, false),
    ("Maybe Some<string> construction", Kernels.SomeString, false),
    ("Maybe Some<Unit> construction", Kernels.SomeUnit, false),
    ("Maybe None<int> construction", Kernels.NoneInt, false),
    ("Maybe None<string> construction", Kernels.NoneString, false),
    ("Maybe IsSome int", Kernels.IsSomeInt, false),
    ("Maybe IsSome string", Kernels.IsSomeString, false),
    ("Maybe TryGetValue Some<int>", Kernels.TryGetSomeInt, false),
    ("Maybe TryGetValue None<int>", Kernels.TryGetNoneInt, false),
    ("Maybe TryGetValue Some<string>", Kernels.TryGetSomeString, false),
    ("Maybe TryGetValue None<string>", Kernels.TryGetNoneString, false),
    ("Presence Value<int> construction", Kernels.PresenceValueInt, false),
    ("Presence Value<string> construction", Kernels.PresenceValueString, false),
    ("Presence Value<Unit> construction", Kernels.PresenceValueUnit, false),
    ("Presence Missing/Null<int> construction", Kernels.PresenceEmptyInt, false),
    ("Presence Missing/Null<string> construction", Kernels.PresenceEmptyString, false),
    ("Presence flags int", Kernels.PresenceFlagsInt, false),
    ("Presence flags string", Kernels.PresenceFlagsString, false),
    ("Presence TryGetValue int", Kernels.PresenceTryGetInt, false),
    ("Presence TryGetValue string", Kernels.PresenceTryGetString, false),
    ("Validation Valid<int> construction", Kernels.ValidationValidInt, false),
    ("Validation Valid<string> construction", Kernels.ValidationValidString, false),
    ("Validation InvalidMany prepared issues", Kernels.ValidationInvalidMany, false),
    ("Validation IsInitialized/IsValid", Kernels.ValidationState, false),
    ("Validation TryGetValue int", Kernels.ValidationTryGetInt, false),
    ("Validation TryGetValue string", Kernels.ValidationTryGetString, false),
];

var failures = optimized ? 0 : 1;
foreach (var (name, kernel, allocates) in controls)
{
    for (var round = 0; round < WarmupRounds; round++)
    {
        kernel(Iterations / 10);
    }

    var before = GC.GetAllocatedBytesForCurrentThread();
    kernel(Iterations);
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

    var passed = allocates ? allocated >= (long)Iterations * IntPtr.Size : allocated == 0;
    failures += passed ? 0 : 1;
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}: {allocated} B / {Iterations} iterations");
}

var directZip3 = Measure(Kernels.ZipThreeInvalid);
var chainedZip3 = Measure(Kernels.ChainedZipThreeInvalid);
var directZip4 = Measure(Kernels.ZipFourInvalid);
var chainedZip4 = Measure(Kernels.ChainedZipFourInvalid);
failures += Witness("direct Zip3 of three Invalid allocates less than binary chain", directZip3 < chainedZip3, $"{directZip3} vs {chainedZip3} B/op");
failures += Witness("direct Zip4 of four Invalid allocates less than binary chain", directZip4 < chainedZip4, $"{directZip4} vs {chainedZip4} B/op");
var traverse64 = Measure(Kernels.Traverse64Invalid);
var traverse256 = Measure(Kernels.Traverse256Invalid);
failures += Witness("Traverse of singleton Invalid grows linearly (256 vs 64 within 6x)", traverse256 <= traverse64 * 6, $"{traverse64} vs {traverse256} B/op");
var released = Kernels.TraverseReleasesSuccessPrefix(out var attempts);
failures += Witness("optimized Traverse releases its success prefix after the first Invalid", released, $"{attempts} attempt(s)");

Kernels.Consume();
return failures == 0 ? 0 : 1;

static double Measure(Action<int> kernel)
{
    const int CostIterations = 2_000;
    for (var round = 0; round < WarmupRounds; round++)
    {
        kernel(CostIterations / 10);
    }

    var before = GC.GetAllocatedBytesForCurrentThread();
    kernel(CostIterations);
    return (double)(GC.GetAllocatedBytesForCurrentThread() - before) / CostIterations;
}

static int Witness(string name, bool passed, string detail)
{
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}: {detail}");
    return passed ? 0 : 1;
}

static bool IsOptimized(Assembly assembly) =>
    assembly.GetCustomAttribute<DebuggableAttribute>() is not { IsJITOptimizerDisabled: true };

internal static class Kernels
{
    private static readonly string _text = "payload";
    private static readonly int _number = 42;
    private static readonly Result<int, int> _okInt = Result<int, int>.Ok(1);
    private static readonly Result<int, int> _errorInt = Result<int, int>.Error(2);
    private static readonly Result<string, string> _okString = Result<string, string>.Ok("ok");
    private static readonly Result<string, string> _uninitialized = default;
    private static readonly Result<string, string> _errorString = Result<string, string>.Error("error");

    private static readonly Maybe<int> _someInt = Maybe<int>.Some(3);
    private static readonly Maybe<int> _noneInt = Maybe<int>.None;
    private static readonly Maybe<string> _someString = Maybe<string>.Some("some");
    private static readonly Maybe<string> _noneString = default;

    private static readonly Presence<int> _presenceInt = Presence<int>.Value(5);
    private static readonly Presence<string> _presenceString = Presence<string>.Value("present");
    private static readonly Presence<int>[] _presenceInts = [Presence<int>.Missing, Presence<int>.Null, _presenceInt];
    private static readonly Presence<string>[] _presenceStrings = [Presence<string>.Missing, Presence<string>.Null, _presenceString];

    private static readonly ImmutableArray<string> _issues = ["a", "b"];
    private static readonly Validation<int, string> _validInt = Validation<int, string>.Valid(6);
    private static readonly Validation<int, string> _invalidInt = Validation<int, string>.InvalidMany(_issues);
    private static readonly Validation<string, string> _validString = Validation<string, string>.Valid("valid");
    private static readonly Validation<string, string> _invalidString = Validation<string, string>.InvalidMany(_issues);
    private static readonly Validation<int, string>[] _invalidOutcomes64 = Enumerable.Repeat(Validation<int, string>.Invalid("e"), 64).ToArray();
    private static readonly Validation<int, string>[] _invalidOutcomes256 = Enumerable.Repeat(Validation<int, string>.Invalid("e"), 256).ToArray();

    private static Presence<int> _presenceIntSink;
    private static Presence<string> _presenceStringSink;
    private static Presence<Unit> _presenceUnitSink;
    private static Validation<int, string> _validationIntSink;
    private static Validation<string, string> _validationStringSink;
    private static Validation<int[], string> _validationArraySink;
    private static Result<int, int> _intSink;
    private static Result<string, string> _stringSink;
    private static Result<Unit, string> _unitSink;
    private static Maybe<int> _maybeIntSink;
    private static Maybe<string> _maybeStringSink;
    private static Maybe<Unit> _maybeUnitSink;
    private static object? _objectSink;
    private static long _counter;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Empty(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += i;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Allocating(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _objectSink = new object();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void OkInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _intSink = Result<int, int>.Ok(i);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ErrorInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _intSink = Result<int, int>.Error(_number + i);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void OkString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _stringSink = Result<string, string>.Ok(_text);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ErrorString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _stringSink = Result<string, string>.Error(_text);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void OkUnit(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _unitSink = Result<Unit, string>.Ok(Unit.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void StateInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var result = (i & 1) == 0 ? _okInt : _errorInt;
            _counter += (result.IsInitialized ? 1 : 0) + (result.IsSuccess ? 2 : 0);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void StateString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var result = (i & 1) == 0 ? _okString : _errorString;
            _counter += (result.IsInitialized ? 1 : 0) + (result.IsSuccess ? 2 : 0);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void StateUninitialized(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _uninitialized.IsInitialized ? 1 : 0;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TryGetOkInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _okInt.TryGetValue(out var value, out var error) ? value : error;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TryGetErrorInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _errorInt.TryGetValue(out var value, out var error) ? value : error;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TryGetOkString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _okString.TryGetValue(out var value, out var error) ? value.Length : error.Length;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TryGetErrorString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _errorString.TryGetValue(out var value, out var error) ? value.Length : error.Length;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SomeInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _maybeIntSink = Maybe<int>.Some(i);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SomeString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _maybeStringSink = Maybe<string>.Some(_text);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SomeUnit(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _maybeUnitSink = Maybe<Unit>.Some(Unit.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void NoneInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _maybeIntSink = (i & 1) == 0 ? Maybe<int>.None : default;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void NoneString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _maybeStringSink = (i & 1) == 0 ? Maybe<string>.None : default;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void IsSomeInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var maybe = (i & 1) == 0 ? _someInt : _noneInt;
            _counter += maybe.IsSome ? 1 : 0;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void IsSomeString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var maybe = (i & 1) == 0 ? _someString : _noneString;
            _counter += maybe.IsSome ? 1 : 0;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TryGetSomeInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _someInt.TryGetValue(out var value) ? value : -1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TryGetNoneInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _noneInt.TryGetValue(out var value) ? value : -1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TryGetSomeString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _someString.TryGetValue(out var value) ? value.Length : -1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TryGetNoneString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _noneString.TryGetValue(out var value) ? value.Length : -1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PresenceValueInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _presenceIntSink = Presence<int>.Value(i);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PresenceValueString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _presenceStringSink = Presence<string>.Value(_text);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PresenceValueUnit(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _presenceUnitSink = Presence<Unit>.Value(Unit.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PresenceEmptyInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _presenceIntSink = (i & 1) == 0 ? Presence<int>.Missing : Presence<int>.Null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PresenceEmptyString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _presenceStringSink = (i & 1) == 0 ? Presence<string>.Missing : Presence<string>.Null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PresenceFlagsInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var presence = _presenceInts[i % 3];
            _counter += (presence.IsMissing ? 1 : 0) + (presence.IsNull ? 2 : 0) + (presence.IsValue ? 4 : 0);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PresenceFlagsString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var presence = _presenceStrings[i % 3];
            _counter += (presence.IsMissing ? 1 : 0) + (presence.IsNull ? 2 : 0) + (presence.IsValue ? 4 : 0);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PresenceTryGetInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _presenceInts[i % 3].TryGetValue(out var value) ? value : -1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PresenceTryGetString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _counter += _presenceStrings[i % 3].TryGetValue(out var value) ? value.Length : -1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ValidationValidInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationIntSink = Validation<int, string>.Valid(i);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ValidationValidString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationStringSink = Validation<string, string>.Valid(_text);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ValidationInvalidMany(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationStringSink = Validation<string, string>.InvalidMany(_issues);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ValidationState(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var validation = (i & 1) == 0 ? _validInt : _invalidInt;
            _counter += (validation.IsInitialized ? 1 : 0) + (validation.IsValid ? 2 : 0);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ValidationTryGetInt(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var validation = (i & 1) == 0 ? _validInt : _invalidInt;
            _counter += validation.TryGetValue(out var value, out var issues) ? value : issues.Length;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ValidationTryGetString(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var validation = (i & 1) == 0 ? _validString : _invalidString;
            _counter += validation.TryGetValue(out var value, out var issues) ? value.Length : issues.Length;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ZipThreeInvalid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationIntSink = _invalidInt.Zip(_invalidInt, _invalidInt, static (a, b, c) => a + b + c);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ChainedZipThreeInvalid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationIntSink = _invalidInt.Zip(_invalidInt, static (a, b) => a + b).Zip(_invalidInt, static (ab, c) => ab + c);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ZipFourInvalid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationIntSink = _invalidInt.Zip(_invalidInt, _invalidInt, _invalidInt, static (a, b, c, d) => a + b + c + d);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ChainedZipFourInvalid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationIntSink = _invalidInt.Zip(_invalidInt, static (a, b) => a + b)
                .Zip(_invalidInt, static (ab, c) => ab + c)
                .Zip(_invalidInt, static (abc, d) => abc + d);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Traverse64Invalid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationArraySink = _invalidOutcomes64.Traverse(static outcome => outcome);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Traverse256Invalid(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _validationArraySink = _invalidOutcomes256.Traverse(static outcome => outcome);
        }
    }

    public static bool TraverseReleasesSuccessPrefix(out int attempts)
    {
        const int MaxAttempts = 30;
        object[] warmup = ["a", "b"];
        for (attempts = 1; attempts <= MaxAttempts; attempts++)
        {
            for (var call = 0; call < 100; call++)
            {
                _objectSink = warmup.Traverse(static item => Validation<object, string>.Valid(item));
            }

            if (!ProbeRetainedPrefix())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ProbeRetainedPrefix()
    {
        var prefix = new WeakReference[1];
        var retained = true;
        _objectSink = Enumerable.Range(0, 3).Select(static index => (object)index).Traverse(item =>
        {
            switch ((int)item)
            {
                case 0:
                    return Validation<object, string>.Valid(CreateTracked(prefix));
                case 1:
                    return Validation<object, string>.Invalid("stop");
                default:
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    retained = prefix[0].IsAlive;
                    return Validation<object, string>.Valid(item);
            }
        });
        return retained;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object CreateTracked(WeakReference[] prefix)
    {
        var payload = new object();
        prefix[0] = new WeakReference(payload);
        return payload;
    }

    public static void Consume() =>
        Console.WriteLine($"sinks: {_counter} {_intSink} {_stringSink} {_unitSink} {_maybeIntSink} {_maybeStringSink} {_maybeUnitSink} " +
            $"{_presenceIntSink} {_presenceStringSink} {_presenceUnitSink} {_validationIntSink} {_validationStringSink} {_validationArraySink.IsInitialized} {_objectSink is not null}");
}
