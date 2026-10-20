using System;
using System.Diagnostics.CodeAnalysis;

namespace Rezultat;

internal static class ThrowHelper
{
    [DoesNotReturn]
    public static void ThrowUninitializedResult() =>
        throw new InvalidOperationException("The Result is uninitialized; create it with Ok or Error.");

    [DoesNotReturn]
    public static void ThrowUninitializedReturnedResult(string callback) =>
        throw new InvalidOperationException($"The Result returned by {callback} is uninitialized; create it with Ok or Error.");
}
