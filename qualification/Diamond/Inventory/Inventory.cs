using Rezultat;
using Rezultat.Collections;

namespace Rezultat.Qualification.DiamondInventory;

public static class Inventory {
    public static Result<string?, string> FindNote(int sku) => sku switch {
        1 => Result<string?, string>.Ok("fragile"),
        2 => Result<string?, string>.Ok(null),
        _ => Result<string?, string>.Error($"unknown sku {sku}"),
    };

    public static Validation<int[], string> ParseQuantities(IEnumerable<string> lines) =>
        lines.Traverse(static line =>
            !int.TryParse(line, out var quantity) ? Validation<int, string>.Invalid($"not a number: {line}")
            : quantity < 0 ? Validation<int, string>.Invalid($"negative: {line}")
            : Validation<int, string>.Valid(quantity));
}
