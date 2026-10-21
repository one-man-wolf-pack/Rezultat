using System;
using System.Collections.Immutable;
using System.Text;

namespace Rezultat.Testing.Xunit;

internal static class FailureText
{
    public static string Uninitialized(string expected, string carrier) =>
        $"Expected {expected}; actual {carrier}.Uninitialized.";

    public static string WrongBranch<TPayload>(string expected, string actual, TPayload payload) =>
        $"Expected {expected}; actual {actual}({Payload(payload)}).";

    public static string Invalid<TIssue>(string expected, ImmutableArray<TIssue> issues)
    {
        var text = new StringBuilder($"Expected {expected}; actual Validation.Invalid; issues=")
            .Append(issues.Length)
            .Append(" [");
        for (var index = 0; index < issues.Length; index++)
        {
            text.Append(index == 0 ? string.Empty : ", ").Append(index).Append('=').Append(Payload(issues[index]));
        }

        return text.Append("].").ToString();
    }

    private static string Payload<TPayload>(TPayload payload)
    {
        if (payload is null)
        {
            return "null";
        }

        try
        {
            return payload.ToString() ?? string.Empty;
        }
        catch (Exception exception)
        {
            return $"<ToString threw {exception.GetType().FullName}>";
        }
    }
}
