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

    [DoesNotReturn]
    public static void ThrowUninitializedValidation() =>
        throw new InvalidOperationException("The Validation is uninitialized; create it with Valid, Invalid or InvalidMany.");

    [DoesNotReturn]
    public static void ThrowUninitializedReturnedValidation(string callback) =>
        throw new InvalidOperationException($"The Validation returned by {callback} is uninitialized; create it with Valid, Invalid or InvalidMany.");

    [DoesNotReturn]
    public static void ThrowEmptyIssues(string paramName) =>
        throw new ArgumentException("Invalid requires an initialized non-empty issue sequence.", paramName);

    [DoesNotReturn]
    public static void ThrowNullPresenceMapResult() =>
        throw new InvalidOperationException("The Presence Map selector returned null; choose Null, Missing or a failure explicitly instead.");

    [DoesNotReturn]
    public static void ThrowArgumentNull(string paramName) =>
        throw new ArgumentNullException(paramName);

    [DoesNotReturn]
    public static void ThrowMoreThanOneElement() =>
        throw new InvalidOperationException("The sequence contains more than one element.");

    [DoesNotReturn]
    public static void ThrowMoreThanOneMatch() =>
        throw new InvalidOperationException("The sequence contains more than one matching element.");
}
