using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

const string PackageId = "Rezultat.Analyzers";
const string AnalyzerPath = "analyzers/dotnet/cs/Rezultat.Analyzers.dll";
const string ReadmePath = "README.md";

var direct = Array.IndexOf(args, "--direct");
var absent = Array.IndexOf(args, "--absent");
if (args.Length < 5 || direct != 5 || absent < direct)
{
    Console.Error.WriteLine("usage: AnalyzerInspector <repo-root> <version> <nupkg> <packages-folder> <library-nupkg> "
        + "--direct <project.assets.json>... --absent <project.assets.json>...");
    return 2;
}

var repoRoot = Path.GetFullPath(args[0]);
var version = args[1];
var nupkgPath = Path.GetFullPath(args[2]);
var packagesFolder = Path.GetFullPath(args[3]);
var checks = new Checks();

Info("nupkg sha256", Sha256(File.ReadAllBytes(nupkgPath)));
using var package = ZipFile.OpenRead(nupkgPath);
var entries = package.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
Info("nupkg entries", string.Join(", ", entries));
string[] allowed = ["[Content_Types].xml", "_rels/.rels", "LICENSE", ReadmePath, $"{PackageId}.nuspec", AnalyzerPath];
var unexpected = entries
    .Where(entry => !allowed.Contains(entry) && !entry.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal))
    .ToArray();
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
string[] forbidden = ["dependencies", "frameworkAssemblies", "frameworkReferences", "references", "contentFiles", "repository", "projectUrl"];
var present = metadata.Elements().Select(element => element.Name.LocalName).Where(forbidden.Contains).ToArray();
checks.That($"nuspec has no dependencies, framework/assembly references, content files or invented repository/project URL: [{string.Join(", ", present)}]",
    present.Length == 0);

var readme = package.GetEntry(ReadmePath);
if (readme is null)
{
    checks.Fail("nupkg contains root README.md");
}
else
{
    var packaged = ReadAll(readme);
    checks.That("packaged README.md is the authored consumer readme",
        packaged.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(repoRoot, "src", PackageId, ReadmePath))));
    var text = Encoding.UTF8.GetString(packaged);
    var localLinks = Regex.Matches(text, @"\]\((?!https://)[^)]*\)").Select(match => match.Value).ToArray();
    checks.That($"README.md links only to absolute https targets: [{string.Join(", ", localLinks)}]", localLinks.Length == 0);
    string[] checkoutMarkers = ["../", "scripts/", "qualify.sh", "rezultat-specification", "src/", "artifacts/"];
    var checkoutOnly = checkoutMarkers.Where(marker => text.Contains(marker, StringComparison.Ordinal)).ToArray();
    checks.That($"README.md has no checkout-only paths or tooling: [{string.Join(", ", checkoutOnly)}]", checkoutOnly.Length == 0);
    var recipe = $"<PackageReference Include=\"{PackageId}\" Version=\"{version}\" PrivateAssets=\"all\" IncludeAssets=\"analyzers\" />";
    checks.That($"README.md shows the private connection recipe for {version}", text.Contains(recipe, StringComparison.Ordinal));
    checks.That("README.md names RZT0001 and its severity key", text.Contains("dotnet_diagnostic.RZT0001.severity", StringComparison.Ordinal));
}

var dll = ReadAll(package.GetEntry(AnalyzerPath)!);
Info("analyzer dll sha256", Sha256(dll));
using (var peReader = new PEReader(ImmutableArray.Create(dll)))
{
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
    checks.That($"analyzer informational version is {version}", attributes.Contains(("AssemblyInformationalVersionAttribute", version)));

    var types = reader.TypeDefinitions.Select(handle => reader.GetTypeDefinition(handle))
        .Select(type => $"{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}")
        .ToArray();
    Info("analyzer types", string.Join(", ", types));
    checks.That("analyzer assembly defines no carrier types",
        !types.Any(type => type.StartsWith("Rezultat.", StringComparison.Ordinal) && type.Split('.')[^1] is "Result`2" or "Validation`2" or "Maybe`1" or "Presence`1" or "Unit"));
    checks.That("analyzer dll is deterministic", peReader.ReadDebugDirectory().Any(entry => entry.Type == DebugDirectoryEntryType.Reproducible));
    var dllText = Encoding.UTF8.GetString(dll);
    var roots = new[] { repoRoot, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }.Where(root => root.Length > 1);
    checks.That("analyzer dll bytes carry no physical checkout/home root", !roots.Any(root => dllText.Contains(root.TrimEnd('/'), StringComparison.Ordinal)));
}

using var libraryPackage = ZipFile.OpenRead(Path.GetFullPath(args[4]));
var library = Metadata(libraryPackage, "Rezultat.Qualification.AnalyzerLibrary");
var libraryDependencies = library.Descendants().Where(element => element.Name.LocalName == "dependency")
    .Select(element => element.Attribute("id")?.Value)
    .ToArray();
Info("packed library dependencies", string.Join(", ", libraryDependencies));
checks.That("packed library with the private reference depends on Rezultat but not on Rezultat.Analyzers",
    libraryDependencies.Contains("Rezultat") && !libraryDependencies.Contains(PackageId));

foreach (var assetsPath in args[(direct + 1)..absent])
{
    using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
    var root = assets.RootElement;
    var project = Path.GetFileNameWithoutExtension(assetsPath);
    var key = $"{PackageId}/{version}";
    var libraryEntry = root.GetProperty("libraries").EnumerateObject().Where(item => item.Name == key).ToArray();
    checks.That($"{project}: {key} resolved as a package with the analyzer file",
        libraryEntry.Length == 1 && libraryEntry[0].Value.GetProperty("type").GetString() == "package"
        && libraryEntry[0].Value.GetProperty("files").EnumerateArray().Any(file => file.GetString() == AnalyzerPath));

    foreach (var framework in root.GetProperty("project").GetProperty("frameworks").EnumerateObject())
    {
        var reference = framework.Value.GetProperty("dependencies").GetProperty(PackageId);
        checks.That($"{project} [{framework.Name}]: reference keeps only analyzers and suppresses all assets for dependents",
            reference.GetProperty("include").GetString() == "Analyzers" && reference.GetProperty("suppressParent").GetString() == "All");
    }

    foreach (var target in root.GetProperty("targets").EnumerateObject())
    {
        var entry = target.Value.GetProperty(key);
        var sections = entry.EnumerateObject().Select(item => item.Name).Where(name => name != "type").ToArray();
        checks.That($"{project} [{target.Name}]: analyzer package brings no compile/runtime/build/dependency assets: [{string.Join(", ", sections)}]",
            sections.Length == 0);
    }

    var folders = root.GetProperty("packageFolders").EnumerateObject().Select(folder => Path.GetFullPath(folder.Name).TrimEnd('/')).ToArray();
    checks.That($"{project}: isolated package folder only", folders.SequenceEqual([packagesFolder.TrimEnd('/')]));
    var installed = Path.Combine(packagesFolder, PackageId.ToLowerInvariant(), version);
    var installedDll = Path.Combine(installed, AnalyzerPath);
    checks.That($"{project}: installed analyzer is the inspected package dll",
        File.Exists(installedDll) && File.ReadAllBytes(installedDll).AsSpan().SequenceEqual(dll));
    using var restore = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(installed, ".nupkg.metadata")));
    var source = Path.GetFullPath(restore.RootElement.GetProperty("source").GetString() ?? string.Empty).TrimEnd('/');
    checks.That($"{project}: analyzer source is the package feed {source}", source == Path.GetDirectoryName(nupkgPath));
}

foreach (var assetsPath in args[(absent + 1)..])
{
    using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
    var project = Path.GetFileNameWithoutExtension(assetsPath);
    var libraries = assets.RootElement.GetProperty("libraries").EnumerateObject().Select(item => item.Name).ToArray();
    Info($"{project} libraries", string.Join(", ", libraries));
    checks.That($"{project}: Rezultat.Analyzers is absent from the dependency graph",
        !libraries.Any(name => name.StartsWith($"{PackageId}/", StringComparison.OrdinalIgnoreCase)));
}

return checks.Complete();

static XElement Metadata(ZipArchive archive, string id)
{
    var nuspec = XDocument.Load(archive.GetEntry($"{id}.nuspec")!.Open());
    return nuspec.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
}

static (string Type, string? Value) Attribute(MetadataReader reader, CustomAttributeHandle handle)
{
    var attribute = reader.GetCustomAttribute(handle);
    var constructor = (MemberReferenceHandle)attribute.Constructor;
    var type = reader.GetString(reader.GetTypeReference((TypeReferenceHandle)reader.GetMemberReference(constructor).Parent).Name);
    if (type is not ("TargetFrameworkAttribute" or "AssemblyInformationalVersionAttribute"))
    {
        return (type, null);
    }

    var blob = reader.GetBlobReader(attribute.Value);
    blob.ReadUInt16();
    return (type, blob.ReadSerializedString());
}

static byte[] ReadAll(ZipArchiveEntry entry)
{
    using var stream = entry.Open();
    using var buffer = new MemoryStream();
    stream.CopyTo(buffer);
    return buffer.ToArray();
}

static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

static void Info(string name, string value) => Console.WriteLine($"INFO {name}: {value}");
