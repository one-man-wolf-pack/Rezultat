using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Rezultat.Qualification;


const string PackageId = "Rezultat.Analyzers";
const string AnalyzerPath = "analyzers/dotnet/cs/Rezultat.Analyzers.dll";
const string ReadmePath = "README.md";

string? repositoryUrl = null;
string? sourceRevision = null;
while (args.Length > 1 && args[0] is "--repository-url" or "--source-revision") {
    if (args[0] == "--repository-url") {
        repositoryUrl = args[1];
    } else {
        sourceRevision = args[1];
    }

    args = args[2..];
}

int direct = Array.IndexOf(args, "--direct");
int absent = Array.IndexOf(args, "--absent");
if (repositoryUrl is null || !SourceMetadata.RepositoryUrl().IsMatch(repositoryUrl)
    || (sourceRevision is not null && !SourceMetadata.Commit().IsMatch(sourceRevision))
    || args.Length < 5 || direct != 5 || absent < direct) {
    Console.Error.WriteLine("usage: AnalyzerInspector --repository-url https://github.com/<owner>/<repo> [--source-revision <full commit>] <repo-root> <version> <nupkg> "
        + "<packages-folder> <library-nupkg> --direct <project.assets.json>... --absent <project.assets.json>...");
    return 2;
}

string repoRoot = Path.GetFullPath(args[0]);
string version = args[1];
string nupkgPath = Path.GetFullPath(args[2]);
string packagesFolder = Path.GetFullPath(args[3]);
var checks = new Checks();

Info("nupkg sha256", Sha256(File.ReadAllBytes(nupkgPath)));
using var package = ZipFile.OpenRead(nupkgPath);
string[] entries = [.. package.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal)];
Info("nupkg entries", string.Join(", ", entries));
string[] allowed = ["[Content_Types].xml", "_rels/.rels", "LICENSE", ReadmePath, $"{PackageId}.nuspec", AnalyzerPath];
string[] unexpected = [.. entries.Where(entry => !allowed.Contains(entry) && !entry.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal))];
checks.That($"nupkg has only the C# analyzer DLL, license, readme and metadata: unexpected [{string.Join(", ", unexpected)}]",
    unexpected.Length == 0 && entries.Contains(AnalyzerPath));
checks.That("nupkg has no lib/ref/runtimes/build/buildTransitive/buildMultiTargeting/content/tools assets or install scripts",
    !entries.Any(entry => entry.Split('/')[0] is "lib" or "ref" or "runtimes" or "build" or "buildTransitive" or "buildMultiTargeting"
        or "content" or "contentFiles" or "tools" || entry.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)));

var metadata = Metadata(package, PackageId);
string? Value(string name) => metadata.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value;
checks.Text("nuspec id", PackageId, Value("id") ?? "<none>");
checks.Text("nuspec version", version, Value("version") ?? "<none>");
checks.Text("nuspec developmentDependency", "true", Value("developmentDependency") ?? "<none>");
var license = metadata.Elements().Single(element => element.Name.LocalName == "license");
checks.That($"nuspec license expression MIT: {license}", license.Attribute("type")?.Value == "expression" && license.Value == "MIT");
checks.Text("nuspec readme", ReadmePath, Value("readme") ?? "<none>");
Info("nuspec description", Value("description") ?? "<none>");
Info("nuspec tags", Value("tags") ?? "<none>");
checks.That("nuspec has description and tags", !string.IsNullOrWhiteSpace(Value("description")) && !string.IsNullOrWhiteSpace(Value("tags")));
string[] forbidden = ["dependencies", "frameworkAssemblies", "frameworkReferences", "references", "contentFiles"];
string[] present = [.. metadata.Elements().Select(element => element.Name.LocalName).Where(forbidden.Contains)];
checks.That($"nuspec has no dependencies, framework/assembly references or content files: [{string.Join(", ", present)}]",
    present.Length == 0);
Info("source expectation", sourceRevision is null ? "bootstrap" : $"source-bound {sourceRevision}");
checks.That($"nuspec projectUrl is {repositoryUrl}: {SourceMetadata.Describe(Value("projectUrl"), repositoryUrl)}", Value("projectUrl") == repositoryUrl);
var repository = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == "repository");
string? repositoryType = repository?.Attribute("type")?.Value;
string? repositoryValue = repository?.Attribute("url")?.Value;
string? repositoryCommit = repository?.Attribute("commit")?.Value;
checks.That($"nuspec repository is git {repositoryUrl} with commit {sourceRevision ?? "<none>"}: type {SourceMetadata.Describe(repositoryType, "git")}, "
    + $"url {SourceMetadata.Describe(repositoryValue, repositoryUrl)}, commit {SourceMetadata.Describe(repositoryCommit, sourceRevision)}",
    repositoryType == "git" && repositoryValue == repositoryUrl && repositoryCommit == sourceRevision);
string informationalVersion = sourceRevision is null ? version : $"{version}+{sourceRevision}";

var readme = package.GetEntry(ReadmePath);
if (readme is null) {
    checks.Fail("nupkg contains root README.md");
} else {
    byte[] packaged = ReadAll(readme);
    checks.That("packaged README.md is the authored consumer readme",
        packaged.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(repoRoot, "src", PackageId, ReadmePath))));
    string text = Encoding.UTF8.GetString(packaged);
    string[] localLinks = [.. Patterns.LocalLink().Matches(text).Select(match => match.Value)];
    checks.That($"README.md links only to absolute https targets: [{string.Join(", ", localLinks)}]", localLinks.Length == 0);
    string[] checkoutMarkers = ["../", "scripts/", "qualify.sh", "rezultat-specification", "src/", "artifacts/"];
    string[] checkoutOnly = [.. checkoutMarkers.Where(marker => text.Contains(marker, StringComparison.Ordinal))];
    checks.That($"README.md has no checkout-only paths or tooling: [{string.Join(", ", checkoutOnly)}]", checkoutOnly.Length == 0);
    // The installation example may name another release than this candidate; the candidate's
    // own version is checked in the nuspec, restore assets and analyzer execution.
    var recipes = Patterns.PackageReference().Matches(text).Select(match => ParseReference(match.Value))
        .Where(reference => reference?.Attribute("Include")?.Value == PackageId).ToArray();
    var recipe = recipes.Length == 1 ? recipes[0] : null;
    string recipeVersion = recipe?.Attribute("Version")?.Value ?? "";
    checks.That($"README.md shows exactly one {PackageId} PackageReference: found {recipes.Length}", recipes.Length == 1);
    checks.That($"README.md recipe names an explicit version: '{recipeVersion}'", Patterns.ExplicitVersion().IsMatch(recipeVersion));
    checks.That("README.md recipe is private and analyzers-only",
        recipe?.Attribute("PrivateAssets")?.Value == "all" && recipe.Attribute("IncludeAssets")?.Value == "analyzers"
        && recipe.Attributes().Count() == 4);
    checks.That("README.md names RZT0001 and its severity key", text.Contains("dotnet_diagnostic.RZT0001.severity", StringComparison.Ordinal));
}

byte[] dll = ReadAll(package.GetEntry(AnalyzerPath)!);
Info("analyzer dll sha256", Sha256(dll));
using (var peReader = new PEReader(ImmutableArray.Create(dll))) {
    var reader = peReader.GetMetadataReader();
    var references = reader.AssemblyReferences
        .Select(handle => reader.GetAssemblyReference(handle))
        .Select(reference => (Name: reader.GetString(reference.Name), reference.Version))
        .Order()
        .ToArray();
    Info("analyzer assembly references", string.Join(", ", references.Select(reference => $"{reference.Name} {reference.Version}")));
    checks.That("analyzer references netstandard 2.0.0.0 and the Roslyn 4.14 API profile (Microsoft.CodeAnalysis 4.14.0.0)",
        references.Contains(("netstandard", new Version(2, 0, 0, 0))) && references.Contains(("Microsoft.CodeAnalysis", new Version(4, 14, 0, 0))));
    checks.That("analyzer references nothing beyond netstandard, Microsoft.CodeAnalysis and System.Collections.Immutable <= 9",
        references.All(reference => reference.Name is "netstandard" or "Microsoft.CodeAnalysis"
            || (reference.Name == "System.Collections.Immutable" && reference.Version.Major <= 9)));

    var attributes = reader.GetAssemblyDefinition().GetCustomAttributes().Select(handle => Attribute(reader, handle)).ToArray();
    checks.That("analyzer targets .NETStandard,Version=v2.0",
        attributes.Contains(("TargetFrameworkAttribute", ".NETStandard,Version=v2.0")));
    checks.That($"analyzer informational version is {informationalVersion}",
        attributes.Contains(("AssemblyInformationalVersionAttribute", informationalVersion)));

    string[] types = [.. reader.TypeDefinitions.Select(handle => reader.GetTypeDefinition(handle)).Select(type => $"{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}")];
    Info("analyzer types", string.Join(", ", types));
    checks.That("analyzer assembly defines no carrier types",
        !types.Any(type => type.StartsWith("Rezultat.", StringComparison.Ordinal) && type.Split('.')[^1] is "Result`2" or "Validation`2" or "Maybe`1" or "Presence`1" or "Unit"));
    checks.That("analyzer dll is deterministic", peReader.ReadDebugDirectory().Any(entry => entry.Type == DebugDirectoryEntryType.Reproducible));
    string dllText = Encoding.UTF8.GetString(dll);
    var roots = new[] { repoRoot, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }.Where(root => root.Length > 1);
    checks.That("analyzer dll bytes carry no physical checkout/home root", !roots.Any(root => dllText.Contains(root.TrimEnd('/'), StringComparison.Ordinal)));
}

using var libraryPackage = ZipFile.OpenRead(Path.GetFullPath(args[4]));
var library = Metadata(libraryPackage, "Rezultat.Qualification.AnalyzerLibrary");
string?[] libraryDependencies = [.. library.Descendants().Where(element => element.Name.LocalName == "dependency").Select(element => element.Attribute("id")?.Value)];
Info("packed library dependencies", string.Join(", ", libraryDependencies));
checks.That("packed library with the private reference depends on Rezultat but not on Rezultat.Analyzers",
    libraryDependencies.Contains("Rezultat") && !libraryDependencies.Contains(PackageId));

foreach (string? assetsPath in args[(direct + 1)..absent]) {
    using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
    var root = assets.RootElement;
    string project = Path.GetFileNameWithoutExtension(assetsPath);
    string key = $"{PackageId}/{version}";
    var libraryEntry = root.GetProperty("libraries").EnumerateObject().Where(item => item.Name == key).ToArray();
    checks.That($"{project}: {key} resolved as a package with the analyzer file",
        libraryEntry.Length == 1 && libraryEntry[0].Value.GetProperty("type").GetString() == "package"
        && libraryEntry[0].Value.GetProperty("files").EnumerateArray().Any(file => file.GetString() == AnalyzerPath));

    foreach (var framework in root.GetProperty("project").GetProperty("frameworks").EnumerateObject()) {
        var reference = framework.Value.GetProperty("dependencies").GetProperty(PackageId);
        checks.That($"{project} [{framework.Name}]: reference keeps only analyzers and suppresses all assets for dependents",
            reference.GetProperty("include").GetString() == "Analyzers" && reference.GetProperty("suppressParent").GetString() == "All");
    }

    foreach (var target in root.GetProperty("targets").EnumerateObject()) {
        var entry = target.Value.GetProperty(key);
        string[] sections = [.. entry.EnumerateObject().Select(item => item.Name).Where(name => name != "type")];
        checks.That($"{project} [{target.Name}]: analyzer package brings no compile/runtime/build/dependency assets: [{string.Join(", ", sections)}]",
            sections.Length == 0);
    }

    string[] folders = [.. root.GetProperty("packageFolders").EnumerateObject().Select(folder => Path.GetFullPath(folder.Name).TrimEnd('/'))];
    checks.That($"{project}: isolated package folder only", folders.SequenceEqual([packagesFolder.TrimEnd('/')]));
    string installed = Path.Combine(packagesFolder, PackageId.ToLowerInvariant(), version);
    string installedDll = Path.Combine(installed, AnalyzerPath);
    checks.That($"{project}: installed analyzer is the inspected package dll",
        File.Exists(installedDll) && File.ReadAllBytes(installedDll).AsSpan().SequenceEqual(dll));
    using var restore = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(installed, ".nupkg.metadata")));
    string source = Path.GetFullPath(restore.RootElement.GetProperty("source").GetString() ?? string.Empty).TrimEnd('/');
    checks.That($"{project}: analyzer source is the package feed {source}", source == Path.GetDirectoryName(nupkgPath));
}

foreach (string? assetsPath in args[(absent + 1)..]) {
    using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
    string project = Path.GetFileNameWithoutExtension(assetsPath);
    string[] libraries = [.. assets.RootElement.GetProperty("libraries").EnumerateObject().Select(item => item.Name)];
    Info($"{project} libraries", string.Join(", ", libraries));
    checks.That($"{project}: Rezultat.Analyzers is absent from the dependency graph",
        !libraries.Any(name => name.StartsWith($"{PackageId}/", StringComparison.OrdinalIgnoreCase)));
}

return checks.Complete();

static XElement Metadata(ZipArchive archive, string id) {
    var nuspec = XDocument.Load(archive.GetEntry($"{id}.nuspec")!.Open());
    return nuspec.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
}

static (string Type, string? Value) Attribute(MetadataReader reader, CustomAttributeHandle handle) {
    var attribute = reader.GetCustomAttribute(handle);
    var constructor = (MemberReferenceHandle)attribute.Constructor;
    string type = reader.GetString(reader.GetTypeReference((TypeReferenceHandle)reader.GetMemberReference(constructor).Parent).Name);
    if (type is not ("TargetFrameworkAttribute" or "AssemblyInformationalVersionAttribute")) {
        return (type, null);
    }

    var blob = reader.GetBlobReader(attribute.Value);
    blob.ReadUInt16();
    return (type, blob.ReadSerializedString());
}

static byte[] ReadAll(ZipArchiveEntry entry) {
    using var stream = entry.Open();
    using var buffer = new MemoryStream();
    stream.CopyTo(buffer);
    return buffer.ToArray();
}

static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

static void Info(string name, string value) => Console.WriteLine($"INFO {name}: {value}");

// A malformed element is not a usable recipe; it counts as no reference at all.
static XElement? ParseReference(string element) {
    try {
        return XElement.Parse(element);
    } catch (System.Xml.XmlException) {
        return null;
    }
}

// The match timeout bounds a pathological README line; a timeout fails the check.
internal static partial class Patterns {
    [GeneratedRegex(@"\]\((?!https://)[^)]*\)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex LocalLink();

    [GeneratedRegex(@"<PackageReference\b[^>]*>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex PackageReference();

    // A literal NuGet version: no floating range, wildcard or MSBuild property.
    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex ExplicitVersion();
}
