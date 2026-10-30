using System.Text.RegularExpressions;

namespace Rezultat.Qualification;

internal static partial class SourceMetadata {
    // Package metadata is untrusted until it matches the agreed values; a credentialed URL in it must not reach the log.
    public static string Describe(string? actual, string? expected) =>
        actual is null ? "absent" : actual == expected ? "as expected" : "differs";

    [GeneratedRegex(@"^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex RepositoryUrl();

    [GeneratedRegex(@"^[0-9a-f]{40}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Commit();
}
