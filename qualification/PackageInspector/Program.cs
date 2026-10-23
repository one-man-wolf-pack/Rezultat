using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Length < 7 || args[0] is not (Inspection.CoreId or Inspection.AssertionsId))
{
    Console.Error.WriteLine("usage: PackageInspector <Rezultat|Rezultat.Testing.Xunit> <repo-root> <version> <nupkg> <snupkg> <packages-folder> <project.assets.json>...");
    return 2;
}

var repoRoot = Path.GetFullPath(args[1]);
var inspection = new Inspection(args[0], args[2]);
var nupkgDll = inspection.InspectPackage(args[3], repoRoot);
inspection.InspectSymbols(args[4], nupkgDll, repoRoot);
foreach (var assets in args[6..])
{
    inspection.InspectConsumer(assets, Path.GetFullPath(args[5]), Path.GetDirectoryName(Path.GetFullPath(args[3]))!, nupkgDll);
}

return inspection.Complete();

internal sealed class Inspection(string packageId, string packageVersion)
{
    public const string CoreId = "Rezultat";
    public const string AssertionsId = "Rezultat.Testing.Xunit";
    private const string AssertId = "xunit.v3.assert";
    private const string LibPath = "lib/net10.0/";
    private const string LogicalRoot = "/_/";
    private const string ReadmePath = "README.md";

    private static readonly Guid _sha256Algorithm = new("8829d00f-11b8-4213-878b-770e8597ac16");
    private static readonly Guid _embeddedSourceKind = new("0e8a571b-6926-466e-b4ad-8ab04611f5fe");
    private static readonly Guid _sourceLinkKind = new("cc110556-a091-4d38-9fec-25ab9a351a6a");

    private int _failures;
    private string? _assertMinimum;

    public byte[] InspectPackage(string nupkgPath, string repoRoot)
    {
        Report("nupkg sha256", Sha256(File.ReadAllBytes(nupkgPath)));
        using var package = ZipFile.OpenRead(nupkgPath);
        var entries = package.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
        Report("nupkg entries", string.Join(", ", entries));

        string[] allowed =
        [
            "[Content_Types].xml", "_rels/.rels", "LICENSE", ReadmePath, $"{packageId}.nuspec",
            $"{LibPath}{packageId}.dll", $"{LibPath}{packageId}.xml",
        ];
        var unexpected = entries
            .Where(entry => !allowed.Contains(entry) && !entry.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal))
            .ToArray();
        Check("nupkg has only lib/net10.0 assets, docs, license, readme and metadata", unexpected.Length == 0, string.Join(", ", unexpected));
        Check("nupkg has no build/analyzer/content/ref assets",
            !entries.Any(entry => entry.Split('/')[0] is "build" or "buildTransitive" or "buildMultiTargeting" or "analyzers" or "content" or "contentFiles" or "ref" or "tools"));

        var nuspec = XDocument.Load(package.GetEntry($"{packageId}.nuspec")!.Open());
        var metadata = nuspec.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
        string? Value(string name) => metadata.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value;
        Check("nuspec id", Value("id") == packageId, Value("id"));
        Check("nuspec version", Value("version") == packageVersion, Value("version"));
        var license = metadata.Elements().Single(element => element.Name.LocalName == "license");
        Check("nuspec license expression MIT", license.Attribute("type")?.Value == "expression" && license.Value == "MIT", license.ToString());

        Report("nuspec description", Value("description") ?? "<none>");
        Report("nuspec tags", Value("tags") ?? "<none>");
        Check("nuspec has description and tags", !string.IsNullOrWhiteSpace(Value("description")) && !string.IsNullOrWhiteSpace(Value("tags")));
        InspectReadme(package, Value("readme"), repoRoot);

        var dependencies = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == "dependencies");
        var groups = dependencies?.Elements().ToArray() ?? [];
        Report("nuspec dependencies", dependencies?.ToString(SaveOptions.DisableFormatting) ?? "<none>");
        Check("nuspec has one net10.0 dependency group",
            groups.Length == 1 && groups[0].Attribute("targetFramework")?.Value == "net10.0");
        var packageDependencies = groups.SelectMany(group => group.Elements()).ToArray();
        if (packageId == CoreId)
        {
            Check("nuspec has no package dependencies", packageDependencies.Length == 0);
        }
        else
        {
            InspectAssertionDependencies(packageDependencies);
        }

        var repository = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == "repository");
        Report("nuspec repository", repository?.ToString(SaveOptions.DisableFormatting) ?? "<none>");
        Check("nuspec carries no invented repository url/commit",
            repository is null || (repository.Attribute("url") is null && repository.Attribute("commit") is null));

        var dll = ReadAll(package.GetEntry($"{LibPath}{packageId}.dll")!);
        Report("dll sha256", Sha256(dll));
        var informationalVersion = ReadInformationalVersion(dll);
        Report("dll informational version", informationalVersion ?? "<none>");
        Check("dll informational version is the package version without unconfirmed source revision",
            informationalVersion == packageVersion, informationalVersion);

        var references = ReadAssemblyReferences(dll);
        Report("dll assembly references", string.Join(", ", references.Select(reference => $"{reference.Name} {reference.Version}")));
        Check("dll references System.Collections.Immutable from the net10.0 framework",
            references.Any(reference => reference.Name == "System.Collections.Immutable" && reference.Version.Major == 10));
        static bool Framework((string Name, Version Version) reference) =>
            reference.Version.Major == 10 && (reference.Name.StartsWith("System.", StringComparison.Ordinal) || reference.Name == "netstandard");
        if (packageId == CoreId)
        {
            Check("dll references only net10.0 framework assemblies", references.All(Framework),
                string.Join(", ", references.Select(reference => reference.Name)));
        }
        else
        {
            Check("dll references only net10.0 framework assemblies, Rezultat and xunit.v3.assert",
                references.All(reference => Framework(reference) || reference.Name is CoreId or AssertId)
                && references.Any(reference => reference.Name == CoreId) && references.Any(reference => reference.Name == AssertId),
                string.Join(", ", references.Select(reference => reference.Name)));
        }

        return dll;
    }

    public void InspectSymbols(string snupkgPath, byte[] dll, string repoRoot)
    {
        Report("snupkg sha256", Sha256(File.ReadAllBytes(snupkgPath)));
        using var symbols = ZipFile.OpenRead(snupkgPath);
        var entries = symbols.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
        Report("snupkg entries", string.Join(", ", entries));
        var pdbEntry = symbols.GetEntry($"{LibPath}{packageId}.pdb");
        Check("snupkg contains lib/net10.0/Rezultat.pdb", pdbEntry is not null);
        if (pdbEntry is null)
        {
            return;
        }

        var pdb = ReadAll(pdbEntry);
        Report("pdb sha256", Sha256(pdb));
        using var peReader = new PEReader(ImmutableArray.Create(dll));
        var debugDirectory = peReader.ReadDebugDirectory();
        Report("dll debug directory", string.Join(", ", debugDirectory.Select(entry => entry.Type)));
        Check("dll has no embedded portable pdb", debugDirectory.All(entry => entry.Type != DebugDirectoryEntryType.EmbeddedPortablePdb));
        Check("dll is deterministic", debugDirectory.Any(entry => entry.Type == DebugDirectoryEntryType.Reproducible));

        var codeViewEntry = debugDirectory.Single(entry => entry.Type == DebugDirectoryEntryType.CodeView);
        var codeView = peReader.ReadCodeViewDebugDirectoryData(codeViewEntry);
        Report("dll codeview", $"{codeView.Guid} age {codeView.Age} path {codeView.Path}");
        Check("dll codeview path is logical", IsLogical(codeView.Path), codeView.Path);
        var physicalRoots = PhysicalRoots(repoRoot);
        var dllText = System.Text.Encoding.UTF8.GetString(dll);
        var leakedRoot = physicalRoots.FirstOrDefault(physical => dllText.Contains(physical, StringComparison.Ordinal));
        Check("dll bytes carry no physical checkout/home root", leakedRoot is null, leakedRoot);

        using var pdbProvider = MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(pdb));
        var pdbReader = pdbProvider.GetMetadataReader();
        var header = pdbReader.DebugMetadataHeader!;
        var pdbGuid = new Guid(header.Id.AsSpan(0, 16));
        var pdbStamp = BitConverter.ToUInt32(header.Id.AsSpan(16, 4));
        Check("pdb id matches dll codeview guid and stamp", pdbGuid == codeView.Guid && pdbStamp == codeViewEntry.Stamp,
            $"pdb {pdbGuid}/{pdbStamp:x8}, dll {codeView.Guid}/{codeViewEntry.Stamp:x8}");

        var checksumEntry = debugDirectory.Single(entry => entry.Type == DebugDirectoryEntryType.PdbChecksum);
        var checksum = peReader.ReadPdbChecksumDebugDirectoryData(checksumEntry);
        var zeroedId = pdb.ToArray();
        Array.Clear(zeroedId, header.IdStartOffset, header.Id.Length);
        Check("pdb checksum matches dll PdbChecksum entry",
            checksum.AlgorithmName == "SHA256" && SHA256.HashData(zeroedId).AsSpan().SequenceEqual(checksum.Checksum.AsSpan()),
            checksum.AlgorithmName);

        var embedded = 0;
        var sourceLink = 0;
        foreach (var handle in pdbReader.CustomDebugInformation)
        {
            var kind = pdbReader.GetGuid(pdbReader.GetCustomDebugInformation(handle).Kind);
            embedded += kind == _embeddedSourceKind ? 1 : 0;
            sourceLink += kind == _sourceLinkKind ? 1 : 0;
        }

        Check("pdb has no embedded sources", embedded == 0, $"{embedded} embedded");
        Report("pdb source link mapping", sourceLink == 0 ? "absent (not generated in this bootstrap)" : "present");

        var authoredSources = Directory.EnumerateFiles(Path.Combine(repoRoot, "src", packageId), "*.cs", SearchOption.AllDirectories)
            .Select(path => LogicalRoot + Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var authoredDocuments = pdbReader.Documents
            .Select(handle => pdbReader.GetString(pdbReader.GetDocument(handle).Name))
            .Where(name => name.StartsWith($"{LogicalRoot}src/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Check("pdb documents cover every authored source", authoredDocuments.SequenceEqual(authoredSources),
            $"pdb [{string.Join(", ", authoredDocuments)}], sources [{string.Join(", ", authoredSources)}]");

        foreach (var handle in pdbReader.Documents)
        {
            var document = pdbReader.GetDocument(handle);
            var name = pdbReader.GetString(document.Name);
            var algorithm = pdbReader.GetGuid(document.HashAlgorithm);
            var hash = pdbReader.GetBlobBytes(document.Hash);
            if (!IsLogical(name) || physicalRoots.Any(physical => name.Contains(physical, StringComparison.Ordinal)))
            {
                Check($"pdb document {name} has a logical path", false);
                continue;
            }

            var path = Path.Combine(repoRoot, name[LogicalRoot.Length..]);
            var exists = File.Exists(path);
            var matches = exists && algorithm == _sha256Algorithm && SHA256.HashData(File.ReadAllBytes(path)).AsSpan().SequenceEqual(hash);
            var origin = name.StartsWith($"{LogicalRoot}src/", StringComparison.Ordinal) ? "authored" : "generated";
            Check($"pdb document {name} ({origin}) checksum matches build input", matches, exists ? "hash mismatch" : $"missing {path}");
        }
    }

    public void InspectConsumer(string assetsPath, string packagesFolder, string feedFolder, byte[] nupkgDll)
    {
        using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
        var root = assets.RootElement;
        var project = Path.GetFileName(Path.GetDirectoryName(assetsPath));
        var key = $"{packageId}/{packageVersion}";

        var rezultatLibraries = root.GetProperty("libraries").EnumerateObject()
            .Where(library => library.Name.StartsWith($"{packageId}/", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Check($"{project}: only {key} resolved, as a package",
            rezultatLibraries.Length == 1 && rezultatLibraries[0].Name == key
            && rezultatLibraries[0].Value.GetProperty("type").GetString() == "package",
            string.Join(", ", rezultatLibraries.Select(library => library.Name)));

        foreach (var target in root.GetProperty("targets").EnumerateObject())
        {
            Report($"{project} [{target.Name}] libraries", string.Join(", ", target.Value.EnumerateObject().Select(library => library.Name)));
            var entry = target.Value.GetProperty(key);
            string[] Keys(string section) => entry.TryGetProperty(section, out var value) ? value.EnumerateObject().Select(item => item.Name).ToArray() : [];
            Check($"{project} [{target.Name}] compile asset", Keys("compile").SequenceEqual([$"{LibPath}{packageId}.dll"]), string.Join(", ", Keys("compile")));
            Check($"{project} [{target.Name}] runtime asset", Keys("runtime").SequenceEqual([$"{LibPath}{packageId}.dll"]), string.Join(", ", Keys("runtime")));
            string[] expectedDependencies = packageId == CoreId ? [] : [CoreId, AssertId];
            Check($"{project} [{target.Name}] no package build assets; dependencies [{string.Join(", ", expectedDependencies)}]",
                Keys("build").Length == 0 && Keys("buildMultiTargeting").Length == 0
                && Keys("dependencies").Order(StringComparer.Ordinal).SequenceEqual(expectedDependencies),
                string.Join(", ", Keys("dependencies")));
            if (packageId == AssertionsId)
            {
                InspectAssertionGraph(root, target, project);
            }
        }

        var folders = root.GetProperty("packageFolders").EnumerateObject().Select(folder => Path.GetFullPath(folder.Name)).ToArray();
        Check($"{project}: isolated package folder only",
            folders.Length == 1 && folders[0].TrimEnd('/') == packagesFolder.TrimEnd('/'), string.Join(", ", folders));

        var installed = Path.Combine(packagesFolder, packageId.ToLowerInvariant(), packageVersion);
        var installedDll = Path.Combine(installed, LibPath, $"{packageId}.dll");
        Check($"{project}: installed dll is the inspected package dll",
            File.Exists(installedDll) && File.ReadAllBytes(installedDll).AsSpan().SequenceEqual(nupkgDll));

        using var restoreMetadata = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(installed, ".nupkg.metadata")));
        var source = restoreMetadata.RootElement.GetProperty("source").GetString() ?? string.Empty;
        Check($"{project}: package source is the local feed", Path.GetFullPath(source).TrimEnd('/') == feedFolder.TrimEnd('/'), source);
    }

    private void InspectAssertionDependencies(XElement[] dependencies)
    {
        string? Attribute(XElement element, string name) => element.Attribute(name)?.Value;
        string[] Flags(XElement element) => (Attribute(element, "exclude") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
        Check("nuspec depends exactly on Rezultat and xunit.v3.assert",
            dependencies.Select(dependency => Attribute(dependency, "id")).Order(StringComparer.Ordinal).SequenceEqual([CoreId, AssertId]));

        var core = dependencies.SingleOrDefault(dependency => Attribute(dependency, "id") == CoreId);
        Check("nuspec Rezultat dependency is the shared-line minimum without exact pin or upper bound",
            core is not null && Attribute(core, "version") == packageVersion, core?.ToString());
        Check("nuspec Rezultat dependency keeps compile and runtime assets",
            core is not null && !Flags(core).Intersect(["Compile", "Runtime"]).Any(), core?.ToString());

        var assert = dependencies.SingleOrDefault(dependency => Attribute(dependency, "id") == AssertId);
        var minimum = assert is null ? null : Attribute(assert, "version");
        Report("nuspec xunit.v3.assert minimum", minimum ?? "<none>");
        Check("nuspec xunit.v3.assert dependency is a plain minimum without exact pin or upper bound",
            minimum is not null && Version.TryParse(minimum, out _), minimum);
        var excluded = assert is null ? [] : Flags(assert);
        Check("nuspec xunit.v3.assert keeps runtime and excludes compile, build, buildTransitive and analyzers",
            assert is not null && !excluded.Contains("Runtime") && !excluded.Contains("All")
            && new[] { "Compile", "Build", "BuildTransitive", "Analyzers" }.All(excluded.Contains),
            assert?.ToString());
        _assertMinimum = minimum;
    }

    private void InspectAssertionGraph(JsonElement root, JsonProperty target, string? project)
    {
        var libraries = target.Value.EnumerateObject().ToDictionary(library => library.Name.Split('/')[0], library => library, StringComparer.OrdinalIgnoreCase);
        string[] Keys(JsonProperty library, string section) =>
            library.Value.TryGetProperty(section, out var value) ? value.EnumerateObject().Select(item => item.Name).ToArray() : [];
        var label = $"{project} [{target.Name}]";

        Check($"{label}: Rezultat {packageVersion} resolved as a package",
            libraries.TryGetValue(CoreId, out var core) && core.Name == $"{CoreId}/{packageVersion}"
            && core.Value.GetProperty("type").GetString() == "package");
        if (!libraries.TryGetValue(AssertId, out var assert))
        {
            Check($"{label}: xunit.v3.assert resolved", false);
            return;
        }

        var assertVersion = assert.Name.Split('/')[1];
        var ownsAssert = libraries.Keys.Any(name => name.StartsWith("xunit.v3.core", StringComparison.OrdinalIgnoreCase));
        Report($"{label} xunit.v3.assert", $"{assertVersion} compile [{string.Join(", ", Keys(assert, "compile"))}] runtime [{string.Join(", ", Keys(assert, "runtime"))}]");
        Check($"{label}: xunit.v3.assert runtime assembly flows to the test consumer",
            Keys(assert, "runtime").Any(asset => asset.EndsWith("/xunit.v3.assert.dll", StringComparison.Ordinal)));
        Check($"{label}: xunit.v3.assert brings no build or analyzer assets",
            Keys(assert, "build").Length == 0 && Keys(assert, "buildMultiTargeting").Length == 0 && Keys(assert, "analyzers").Length == 0);
        if (ownsAssert)
        {
            Check($"{label}: the consumer's own xUnit v3 dependency keeps xunit.v3.assert compile assets",
                Keys(assert, "compile").Any(asset => asset.EndsWith("/xunit.v3.assert.dll", StringComparison.Ordinal)));
            return;
        }

        Check($"{label}: no xunit.v3.assert compile asset through the helper",
            Keys(assert, "compile").All(asset => asset.EndsWith("/_._", StringComparison.Ordinal)), string.Join(", ", Keys(assert, "compile")));
        Check($"{label}: xunit.v3.assert resolved at the declared minimum {_assertMinimum}", assertVersion == _assertMinimum, assertVersion);
        Check($"{label}: helper brings no other xUnit v3 package",
            libraries.Keys.All(name => !name.StartsWith("xunit.v3.", StringComparison.OrdinalIgnoreCase) || name.Equals(AssertId, StringComparison.OrdinalIgnoreCase)),
            string.Join(", ", libraries.Keys));
        var direct = root.GetProperty("project").GetProperty("frameworks").EnumerateObject()
            .SelectMany(framework => framework.Value.TryGetProperty("dependencies", out var value) ? value.EnumerateObject().Select(item => item.Name) : [])
            .ToArray();
        if (direct.SequenceEqual([AssertionsId], StringComparer.OrdinalIgnoreCase))
        {
            Check($"{label}: the helper graph is exactly Rezultat, Rezultat.Testing.Xunit and xunit.v3.assert",
                libraries.Keys.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual([CoreId, AssertionsId, AssertId], StringComparer.OrdinalIgnoreCase),
                string.Join(", ", libraries.Keys));
        }
        else if (libraries.ContainsKey("xunit.assert"))
        {
            Check($"{label}: the consumer's own xUnit v2 assertion library keeps compile assets",
                Keys(libraries["xunit.assert"], "compile").Any(asset => asset.EndsWith("/xunit.assert.dll", StringComparison.Ordinal)));
        }
    }

    public int Complete()
    {
        Console.WriteLine(_failures == 0 ? "package inspection passed" : $"package inspection failed: {_failures}");
        return _failures == 0 ? 0 : 1;
    }

    private void InspectReadme(ZipArchive package, string? readme, string repoRoot)
    {
        Check("nuspec readme names the root README.md", readme == ReadmePath, readme);
        var entry = package.GetEntry(ReadmePath);
        Check("nupkg contains root README.md", entry is not null);
        if (entry is null)
        {
            return;
        }

        var packaged = ReadAll(entry);
        var authored = File.ReadAllBytes(Path.Combine(repoRoot, "src", packageId, ReadmePath));
        Check("packaged README.md is the authored consumer readme", packaged.AsSpan().SequenceEqual(authored));

        var text = System.Text.Encoding.UTF8.GetString(packaged);
        var localLinks = Regex.Matches(text, @"\]\((?!https://|#)[^)]*\)").Select(match => match.Value).ToArray();
        Check("README.md links only to absolute https targets or its own sections", localLinks.Length == 0, string.Join(", ", localLinks));
        var sections = GitHubHeadingAnchors(text);
        var anchors = Regex.Matches(text, @"\]\(#([^)]*)\)").Select(match => match.Groups[1].Value).ToArray();
        var unknown = anchors.Where(anchor => !sections.Contains(anchor)).ToArray();
        Report("README.md section links", anchors.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Check("README.md section links name its own headings", unknown.Length == 0, string.Join(", ", unknown));
        string[] checkoutMarkers = ["../", "scripts/", "qualify.sh", "rezultat-specification", "src/", "artifacts/"];
        var checkoutOnly = checkoutMarkers.Where(marker => text.Contains(marker, StringComparison.Ordinal)).ToArray();
        Check("README.md has no checkout-only paths or tooling", checkoutOnly.Length == 0, string.Join(", ", checkoutOnly));
    }

    private static HashSet<string> GitHubHeadingAnchors(string markdown)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var fenced = false;
        foreach (var line in markdown.Split('\n'))
        {
            fenced ^= line.StartsWith("```", StringComparison.Ordinal);
            var heading = fenced ? null : Regex.Match(line, @"^#{1,6} (.+)$");
            if (heading is { Success: true })
            {
                var title = heading.Groups[1].Value.Trim().ToLowerInvariant();
                anchors.Add(Regex.Replace(title, @"[^\p{L}\p{N} _-]", string.Empty).Replace(' ', '-'));
            }
        }

        return anchors;
    }

    private void Check(string name, bool condition, string? detail = null)
    {
        Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}{(condition || detail is null ? string.Empty : $": {detail}")}");
        _failures += condition ? 0 : 1;
    }

    private static bool IsLogical(string path) => path.StartsWith(LogicalRoot, StringComparison.Ordinal);

    private static string[] PhysicalRoots(string repoRoot) =>
        new[] { repoRoot, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }
            .Where(root => root.Length > 1)
            .Select(root => root.TrimEnd('/'))
            .ToArray();

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void Report(string name, string value) => Console.WriteLine($"INFO {name}: {value}");

    private static (string Name, Version Version)[] ReadAssemblyReferences(byte[] dll)
    {
        using var peReader = new PEReader(ImmutableArray.Create(dll));
        var metadata = peReader.GetMetadataReader();
        return metadata.AssemblyReferences
            .Select(handle => metadata.GetAssemblyReference(handle))
            .Select(reference => (metadata.GetString(reference.Name), reference.Version))
            .Order()
            .ToArray();
    }

    private static string? ReadInformationalVersion(byte[] dll)
    {
        using var peReader = new PEReader(ImmutableArray.Create(dll));
        var metadata = peReader.GetMetadataReader();
        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference)
            {
                continue;
            }

            var parent = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
            if (parent.Kind != HandleKind.TypeReference
                || metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)parent).Name) != "AssemblyInformationalVersionAttribute")
            {
                continue;
            }

            var value = metadata.GetBlobReader(attribute.Value);
            value.ReadUInt16();
            return value.ReadSerializedString();
        }

        return null;
    }

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
