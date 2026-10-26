namespace Rezultat.Qualification.DiamondOrders;

public static class Orders {
    public static string DescribeNote(Result<string?, string> note) {
        if (!note.TryGetValue(out string? text, out string? error)) {
            return $"rejected: {error}";
        }

        return text is null ? "no note" : $"note: {text}";
    }

    public static Validation<int, string> TotalQuantity(Validation<int[], string> quantities, int limit) =>
        quantities
            .Map(static values => values.Sum())
            .Ensure(total => total <= limit, total => $"total {total} is over {limit}");
}
