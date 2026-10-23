using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

if (args.Length != 11)
{
    Console.Error.WriteLine("usage: DiamondInspector <case> <packages-folder> <library-feed> <app-bin> <assets-dir> "
        + "<core-version> <core-feed> <core-sha256> <inventory-core-version> <inventory-core-feed> <inventory-core-sha256>");
    return 2;
}

var inspection = new Inspection(args[0], Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
var candidate = new Core(args[5], Path.GetFullPath(args[6]), args[7]);
var inventoryCore = new Core(args[8], Path.GetFullPath(args[9]), args[10]);
var assets = Path.GetFullPath(args[4]);

var inventoryDll = inspection.Library(Inspection.InventoryId, inventoryCore, Path.Combine(assets, "DiamondInventory.json"));
var ordersDll = inspection.Library(Inspection.OrdersId, candidate, Path.Combine(assets, "DiamondOrders.json"));
inspection.App(Path.Combine(assets, "DiamondApp.json"), Path.GetFullPath(args[3]), candidate, inventoryCore, inventoryDll, ordersDll);
return inspection.Complete();

internal sealed record Core(string Version, string Feed, string Sha256);

internal sealed class Inspection(string label, string packagesFolder, string libraryFeed)
{
    public const string InventoryId = "Rezultat.Qualification.DiamondInventory";
    public const string OrdersId = "Rezultat.Qualification.DiamondOrders";
    private const string CoreId = "Rezultat";
    private const string LibraryVersion = "1.0.0";
    private const string Framework = "net10.0";

    private int _failures;

    public byte[] Library(string id, Core core, string assetsPath)
    {
        var name = id.Split('.')[^1];
        using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
        var root = assets.RootElement;
        Check($"{name}: restore input is the minimum range of Rezultat {core.Version}",
            DirectDependency(root, CoreId) == $"[{core.Version}, )", DirectDependency(root, CoreId));
        CheckResolvedCore(name, root, core);
        CheckNoRestoreLogs(name, root);

        var nupkgPath = Path.Combine(libraryFeed, $"{id}.{LibraryVersion}.nupkg");
        Report($"{name} nupkg sha256", Sha256(File.ReadAllBytes(nupkgPath)));
        using var package = ZipFile.OpenRead(nupkgPath);
        var nuspec = XDocument.Load(package.GetEntry($"{id}.nuspec")!.Open());
        var groups = nuspec.Descendants().Where(element => element.Name.LocalName == "group").ToArray();
        var dependencies = groups.SelectMany(group => group.Elements()).ToArray();
        Report($"{name} nuspec dependencies", string.Join(", ", dependencies.Select(dependency => dependency.ToString(SaveOptions.DisableFormatting))));
        Check($"{name}: nuspec depends only on Rezultat with minimum {core.Version}",
            groups.Length == 1 && groups[0].Attribute("targetFramework")?.Value == Framework
            && dependencies.Length == 1 && dependencies[0].Attribute("id")?.Value == CoreId && dependencies[0].Attribute("version")?.Value == core.Version);

        var dll = ReadAll(package.GetEntry($"lib/{Framework}/{id}.dll")!);
        var reference = CoreReference(dll);
        Report($"{name} dll", $"sha256 {Sha256(dll)}, mvid {Mvid(dll)}, references Rezultat {reference}");
        Check($"{name}: dll was compiled against Rezultat {core.Version}", reference == AssemblyVersion(core.Version), reference?.ToString());
        return dll;
    }

    public void App(string assetsPath, string appBin, Core candidate, Core inventoryCore, byte[] inventoryDll, byte[] ordersDll)
    {
        using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
        var root = assets.RootElement;
        Check($"App: exact Rezultat {candidate.Version} reference", DirectDependency(root, CoreId) == $"[{candidate.Version}, {candidate.Version}]", DirectDependency(root, CoreId));
        foreach (var id in new[] { InventoryId, OrdersId })
        {
            Check($"App: exact {id} {LibraryVersion} reference", DirectDependency(root, id) == $"[{LibraryVersion}, {LibraryVersion}]", DirectDependency(root, id));
        }

        var libraries = root.GetProperty("libraries").EnumerateObject()
            .Select(library => $"{library.Name} ({library.Value.GetProperty("type").GetString()})")
            .Order(StringComparer.Ordinal)
            .ToArray();
        Report("App libraries", string.Join(", ", libraries));
        string[] expected =
        [
            $"{CoreId}/{candidate.Version} (package)",
            $"{InventoryId}/{LibraryVersion} (package)",
            $"{OrdersId}/{LibraryVersion} (package)",
        ];
        Check("App: graph is exactly one Rezultat and both fixture libraries, all packages",
            libraries.SequenceEqual(expected.Order(StringComparer.Ordinal)), string.Join(", ", libraries));
        CheckResolvedCore("App", root, candidate);
        CheckNoRestoreLogs("App", root);

        var target = root.GetProperty("targets").GetProperty(Framework);
        string? Declared(string id) =>
            target.GetProperty($"{id}/{LibraryVersion}").TryGetProperty("dependencies", out var value)
            && value.TryGetProperty(CoreId, out var version) ? version.GetString() : null;
        Report("App declared core dependencies", $"Inventory {Declared(InventoryId)}, Orders {Declared(OrdersId)}");
        Check($"App: Inventory keeps its declared minimum {inventoryCore.Version}", Declared(InventoryId) == inventoryCore.Version, Declared(InventoryId));
        Check($"App: Orders keeps its declared minimum {candidate.Version}", Declared(OrdersId) == candidate.Version, Declared(OrdersId));

        foreach (var (id, dll) in new[] { (InventoryId, inventoryDll), (OrdersId, ordersDll) })
        {
            var installed = Path.Combine(packagesFolder, id.ToLowerInvariant(), LibraryVersion);
            Check($"App: installed {id} dll is the packed binary", SameBytes(Path.Combine(installed, "lib", Framework, $"{id}.dll"), dll));
            Check($"App: {id} restored from the fixture feed", RestoreSource(installed) == libraryFeed, RestoreSource(installed));
            Check($"App: output {id}.dll is the packed binary", SameBytes(Path.Combine(appBin, $"{id}.dll"), dll));
        }

        using var corePackage = ZipFile.OpenRead(Path.Combine(candidate.Feed, $"{CoreId}.{candidate.Version}.nupkg"));
        var coreDll = ReadAll(corePackage.GetEntry($"lib/{Framework}/{CoreId}.dll")!);
        Check($"App: output Rezultat.dll is the {candidate.Version} package dll", SameBytes(Path.Combine(appBin, $"{CoreId}.dll"), coreDll));
    }

    public int Complete()
    {
        Console.WriteLine(_failures == 0 ? $"diamond {label} inspection passed" : $"diamond {label} inspection failed: {_failures}");
        return _failures == 0 ? 0 : 1;
    }

    private void CheckResolvedCore(string name, JsonElement root, Core core)
    {
        var resolved = root.GetProperty("libraries").EnumerateObject()
            .Where(library => library.Name.Split('/')[0].Equals(CoreId, StringComparison.OrdinalIgnoreCase))
            .Select(library => library.Name)
            .ToArray();
        Check($"{name}: resolves only Rezultat {core.Version}", resolved.SequenceEqual([$"{CoreId}/{core.Version}"]), string.Join(", ", resolved));
        var compile = root.GetProperty("targets").GetProperty(Framework).TryGetProperty($"{CoreId}/{core.Version}", out var entry)
            && entry.TryGetProperty("compile", out var value) ? value.EnumerateObject().Select(item => item.Name).ToArray() : [];
        Check($"{name}: Rezultat compile asset is lib/{Framework}/Rezultat.dll", compile.SequenceEqual([$"lib/{Framework}/{CoreId}.dll"]), string.Join(", ", compile));

        var folders = root.GetProperty("packageFolders").EnumerateObject().Select(folder => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder.Name))).ToArray();
        Check($"{name}: isolated case package folder only", folders.SequenceEqual([Path.TrimEndingDirectorySeparator(packagesFolder)]), string.Join(", ", folders));

        var installed = Path.Combine(packagesFolder, CoreId.ToLowerInvariant(), core.Version);
        var archive = Path.Combine(installed, $"{CoreId.ToLowerInvariant()}.{core.Version}.nupkg");
        var hash = File.Exists(archive) ? Sha256(File.ReadAllBytes(archive)) : "<missing>";
        Report($"{name} installed Rezultat {core.Version}", $"sha256 {hash}, source {RestoreSource(installed)}");
        Check($"{name}: installed Rezultat {core.Version} is the chosen archive", hash == core.Sha256, hash);
        Check($"{name}: Rezultat {core.Version} restored from its chosen feed", RestoreSource(installed) == core.Feed, RestoreSource(installed));
    }

    private void CheckNoRestoreLogs(string name, JsonElement root)
    {
        var logs = root.TryGetProperty("logs", out var value) ? value.EnumerateArray().Select(log => log.GetRawText()).ToArray() : [];
        Check($"{name}: restore reported no warnings (NU1605/NU1608 included)", logs.Length == 0, string.Join(", ", logs));
    }

    private static string? DirectDependency(JsonElement root, string id) =>
        root.GetProperty("project").GetProperty("frameworks").GetProperty(Framework).GetProperty("dependencies")
            .TryGetProperty(id, out var dependency) ? dependency.GetProperty("version").GetString() : null;

    private static string? RestoreSource(string installed)
    {
        var metadata = Path.Combine(installed, ".nupkg.metadata");
        if (!File.Exists(metadata))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(metadata));
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(document.RootElement.GetProperty("source").GetString()!));
    }

    private static Version AssemblyVersion(string version)
    {
        var parsed = Version.Parse(version);
        return new Version(parsed.Major, parsed.Minor, parsed.Build, 0);
    }

    private static Version? CoreReference(byte[] dll)
    {
        using var peReader = new PEReader(ImmutableArray.Create(dll));
        var metadata = peReader.GetMetadataReader();
        return metadata.AssemblyReferences
            .Select(handle => metadata.GetAssemblyReference(handle))
            .Where(reference => metadata.GetString(reference.Name) == CoreId)
            .Select(reference => reference.Version)
            .SingleOrDefault();
    }

    private static Guid Mvid(byte[] dll)
    {
        using var peReader = new PEReader(ImmutableArray.Create(dll));
        var metadata = peReader.GetMetadataReader();
        return metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    }

    private static bool SameBytes(string path, byte[] expected) => File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(expected);

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void Report(string name, string value) => Console.WriteLine($"INFO {name}: {value}");

    private void Check(string name, bool condition, string? detail = null)
    {
        Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}{(condition || detail is null ? string.Empty : $": {detail}")}");
        _failures += condition ? 0 : 1;
    }
}
