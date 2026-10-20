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

    private static Result<int, int> _intSink;
    private static Result<string, string> _stringSink;
    private static Result<Unit, string> _unitSink;
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

    public static void Consume() =>
        Console.WriteLine($"sinks: {_counter} {_intSink} {_stringSink} {_unitSink} {_objectSink is not null}");
}
