using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Length < 6)
{
    Console.Error.WriteLine("usage: PackageInspector <repo-root> <version> <nupkg> <snupkg> <packages-folder> <project.assets.json>...");
    return 2;
}

var repoRoot = Path.GetFullPath(args[0]);
var inspection = new Inspection(args[1]);
var nupkgDll = inspection.InspectPackage(args[2], repoRoot);
inspection.InspectSymbols(args[3], nupkgDll, repoRoot);
foreach (var assets in args[5..])
{
    inspection.InspectConsumer(assets, Path.GetFullPath(args[4]), Path.GetDirectoryName(Path.GetFullPath(args[2]))!, nupkgDll);
}

return inspection.Complete();

internal sealed class Inspection(string packageVersion)
{
    private const string PackageId = "Rezultat";
    private const string LibPath = "lib/net10.0/";
    private const string LogicalRoot = "/_/";
    private const string ReadmePath = "README.md";

    private static readonly Guid _sha256Algorithm = new("8829d00f-11b8-4213-878b-770e8597ac16");
    private static readonly Guid _embeddedSourceKind = new("0e8a571b-6926-466e-b4ad-8ab04611f5fe");
    private static readonly Guid _sourceLinkKind = new("cc110556-a091-4d38-9fec-25ab9a351a6a");

    private int _failures;

    public byte[] InspectPackage(string nupkgPath, string repoRoot)
    {
        using var package = ZipFile.OpenRead(nupkgPath);
        var entries = package.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
        Report("nupkg entries", string.Join(", ", entries));

        string[] allowed =
        [
            "[Content_Types].xml", "_rels/.rels", "LICENSE", ReadmePath, $"{PackageId}.nuspec",
            $"{LibPath}{PackageId}.dll", $"{LibPath}{PackageId}.xml",
        ];
        var unexpected = entries
            .Where(entry => !allowed.Contains(entry) && !entry.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal))
            .ToArray();
        Check("nupkg has only lib/net10.0 assets, docs, license, readme and metadata", unexpected.Length == 0, string.Join(", ", unexpected));
        Check("nupkg has no build/analyzer/content/ref assets",
            !entries.Any(entry => entry.Split('/')[0] is "build" or "buildTransitive" or "buildMultiTargeting" or "analyzers" or "content" or "contentFiles" or "ref" or "tools"));

        var nuspec = XDocument.Load(package.GetEntry($"{PackageId}.nuspec")!.Open());
        var metadata = nuspec.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
        string? Value(string name) => metadata.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value;
        Check("nuspec id", Value("id") == PackageId, Value("id"));
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
        Check("nuspec has one net10.0 dependency group without package dependencies",
            groups.Length == 1
            && groups[0].Attribute("targetFramework")?.Value == "net10.0"
            && !groups[0].Elements().Any());

        var repository = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == "repository");
        Report("nuspec repository", repository?.ToString(SaveOptions.DisableFormatting) ?? "<none>");
        Check("nuspec carries no invented repository url/commit",
            repository is null || (repository.Attribute("url") is null && repository.Attribute("commit") is null));

        var dll = ReadAll(package.GetEntry($"{LibPath}{PackageId}.dll")!);
        var informationalVersion = ReadInformationalVersion(dll);
        Report("dll informational version", informationalVersion ?? "<none>");
        Check("dll informational version is the package version without unconfirmed source revision",
            informationalVersion == packageVersion, informationalVersion);

        var references = ReadAssemblyReferences(dll);
        Report("dll assembly references", string.Join(", ", references.Select(reference => $"{reference.Name} {reference.Version}")));
        Check("dll references System.Collections.Immutable from the net10.0 framework",
            references.Any(reference => reference.Name == "System.Collections.Immutable" && reference.Version.Major == 10));
        Check("dll references only net10.0 framework assemblies",
            references.All(reference => reference.Version.Major == 10 && (reference.Name.StartsWith("System.", StringComparison.Ordinal) || reference.Name == "netstandard")),
            string.Join(", ", references.Select(reference => reference.Name)));

        return dll;
    }

    public void InspectSymbols(string snupkgPath, byte[] dll, string repoRoot)
    {
        using var symbols = ZipFile.OpenRead(snupkgPath);
        var entries = symbols.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
        Report("snupkg entries", string.Join(", ", entries));
        var pdbEntry = symbols.GetEntry($"{LibPath}{PackageId}.pdb");
        Check("snupkg contains lib/net10.0/Rezultat.pdb", pdbEntry is not null);
        if (pdbEntry is null)
        {
            return;
        }

        var pdb = ReadAll(pdbEntry);
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

        var authoredSources = Directory.EnumerateFiles(Path.Combine(repoRoot, "src", PackageId), "*.cs", SearchOption.AllDirectories)
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
        var key = $"{PackageId}/{packageVersion}";

        var rezultatLibraries = root.GetProperty("libraries").EnumerateObject()
            .Where(library => library.Name.StartsWith($"{PackageId}/", StringComparison.OrdinalIgnoreCase))
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
            Check($"{project} [{target.Name}] compile asset", Keys("compile").SequenceEqual([$"{LibPath}{PackageId}.dll"]), string.Join(", ", Keys("compile")));
            Check($"{project} [{target.Name}] runtime asset", Keys("runtime").SequenceEqual([$"{LibPath}{PackageId}.dll"]), string.Join(", ", Keys("runtime")));
            Check($"{project} [{target.Name}] no package build assets or dependencies",
                Keys("build").Length == 0 && Keys("buildMultiTargeting").Length == 0 && Keys("dependencies").Length == 0);
        }

        var folders = root.GetProperty("packageFolders").EnumerateObject().Select(folder => Path.GetFullPath(folder.Name)).ToArray();
        Check($"{project}: isolated package folder only",
            folders.Length == 1 && folders[0].TrimEnd('/') == packagesFolder.TrimEnd('/'), string.Join(", ", folders));

        var installed = Path.Combine(packagesFolder, PackageId.ToLowerInvariant(), packageVersion);
        var installedDll = Path.Combine(installed, LibPath, $"{PackageId}.dll");
        Check($"{project}: installed dll is the inspected package dll",
            File.Exists(installedDll) && File.ReadAllBytes(installedDll).AsSpan().SequenceEqual(nupkgDll));

        using var restoreMetadata = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(installed, ".nupkg.metadata")));
        var source = restoreMetadata.RootElement.GetProperty("source").GetString() ?? string.Empty;
        Check($"{project}: package source is the local feed", Path.GetFullPath(source).TrimEnd('/') == feedFolder.TrimEnd('/'), source);
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
        var authored = File.ReadAllBytes(Path.Combine(repoRoot, "src", PackageId, ReadmePath));
        Check("packaged README.md is the authored consumer readme", packaged.AsSpan().SequenceEqual(authored));

        var text = System.Text.Encoding.UTF8.GetString(packaged);
        var localLinks = Regex.Matches(text, @"\]\((?!https://)[^)]*\)").Select(match => match.Value).ToArray();
        Check("README.md links only to absolute https targets", localLinks.Length == 0, string.Join(", ", localLinks));
        string[] checkoutMarkers = ["../", "scripts/", "qualify.sh", "rezultat-specification", "src/", "artifacts/"];
        var checkoutOnly = checkoutMarkers.Where(marker => text.Contains(marker, StringComparison.Ordinal)).ToArray();
        Check("README.md has no checkout-only paths or tooling", checkoutOnly.Length == 0, string.Join(", ", checkoutOnly));
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
