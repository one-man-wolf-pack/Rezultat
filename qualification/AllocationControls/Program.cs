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

Kernels.Consume();
return failures == 0 ? 0 : 1;

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

    public static void Consume() =>
        Console.WriteLine($"sinks: {_counter} {_intSink} {_stringSink} {_unitSink} {_maybeIntSink} {_maybeStringSink} {_maybeUnitSink} {_objectSink is not null}");
}
