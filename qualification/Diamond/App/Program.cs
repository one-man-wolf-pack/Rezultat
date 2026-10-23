using System.Reflection;
using System.Runtime.Loader;
using Rezultat;
using Rezultat.Qualification.DiamondInventory;
using Rezultat.Qualification.DiamondOrders;

if (args.Length != 2 || !Version.TryParse(args[0], out var coreVersion) || !Version.TryParse(args[1], out var inventoryCoreVersion))
{
    Console.Error.WriteLine("usage: DiamondApp <resolved-core-assembly-version> <inventory-compile-core-assembly-version>");
    return 2;
}

var checks = new Checks();

checks.Text("Ok note crosses both libraries", "note: fragile", Orders.DescribeNote(Inventory.FindNote(1)));
checks.Text("Ok(null) note crosses both libraries", "no note", Orders.DescribeNote(Inventory.FindNote(2)));
checks.Text("Error note crosses both libraries", "rejected: unknown sku 9", Orders.DescribeNote(Inventory.FindNote(9)));

var nullNote = Inventory.FindNote(2);
checks.That("Inventory Ok(null) is a successful null payload", nullNote.TryGetValue(out var noteText, out _) && noteText is null);

var valid = Orders.TotalQuantity(Inventory.ParseQuantities(["2", "3"]), 10);
checks.That("Valid quantities total 5", valid.TryGetValue(out var total, out _) && total == 5);

var overLimit = Orders.TotalQuantity(Inventory.ParseQuantities(["2", "3"]), 4);
checks.That("Orders adds its own issue to a Valid input",
    !overLimit.TryGetValue(out _, out var limitIssues) && limitIssues.SequenceEqual(["total 5 is over 4"]));

var parsed = Inventory.ParseQuantities(["2", "x", "-1", "x"]);
var invalid = Orders.TotalQuantity(parsed, 10);
string[] expectedIssues = ["not a number: x", "negative: -1", "not a number: x"];
checks.That("Invalid issues keep order and duplicates through both libraries",
    !invalid.TryGetValue(out _, out var issues) && issues.SequenceEqual(expectedIssues));
checks.That("Orders keeps the issue array produced by Inventory",
    !parsed.TryGetValue(out _, out var parsedIssues) && parsedIssues == issues);

var core = typeof(Result<,>).Assembly;
var noteSource = typeof(Inventory).GetMethod(nameof(Inventory.FindNote))!.ReturnType;
var noteTarget = typeof(Orders).GetMethod(nameof(Orders.DescribeNote))!.GetParameters()[0].ParameterType;
var validationSource = typeof(Inventory).GetMethod(nameof(Inventory.ParseQuantities))!.ReturnType;
var validationTarget = typeof(Orders).GetMethod(nameof(Orders.TotalQuantity))!.GetParameters()[0].ParameterType;
checks.That("Inventory and Orders share the app's Result type",
    noteSource == typeof(Result<string?, string>) && noteTarget == noteSource && noteSource.Assembly == core);
checks.That("Inventory and Orders share the app's Validation type",
    validationSource == typeof(Validation<int[], string>) && validationTarget == validationSource && validationSource.Assembly == core);

var loaded = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name == "Rezultat").ToArray();
Console.WriteLine($"INFO loaded Rezultat: {string.Join(", ", loaded.Select(assembly => $"{assembly.GetName().Version} {assembly.Location}"))}");
checks.That("one Rezultat assembly is loaded", loaded.Length == 1 && loaded[0] == core);
checks.That($"loaded Rezultat is {coreVersion}", core.GetName().Version == coreVersion);
checks.That("loaded Rezultat comes from the app directory",
    Path.GetDirectoryName(core.Location) == Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
checks.That("loaded Rezultat is in the default load context", AssemblyLoadContext.GetLoadContext(core) == AssemblyLoadContext.Default);

Version? Referenced(Assembly assembly) =>
    assembly.GetReferencedAssemblies().SingleOrDefault(reference => reference.Name == "Rezultat")?.Version;
var inventoryReference = Referenced(typeof(Inventory).Assembly);
var ordersReference = Referenced(typeof(Orders).Assembly);
Console.WriteLine($"INFO Inventory compiled against Rezultat {inventoryReference}, Orders against {ordersReference}");
checks.That($"Inventory binary references Rezultat {inventoryCoreVersion}", inventoryReference == inventoryCoreVersion);
checks.That($"Orders binary references Rezultat {coreVersion}", ordersReference == coreVersion);

return checks.Complete();
