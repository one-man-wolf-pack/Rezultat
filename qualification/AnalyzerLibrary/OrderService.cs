using Rezultat;

namespace AnalyzerLibrary;

public sealed class OrderService
{
    public Result<int, string> Save(int order) => order > 0 ? Result<int, string>.Ok(order) : Result<int, string>.Error("empty");

    public Validation<int, string> Validate(int order) =>
        order > 0 ? Validation<int, string>.Valid(order) : Validation<int, string>.Invalid("empty");

    public Result<Unit, string> Cancel(int order) => Result<Unit, string>.Ok(Unit.Value);

    public Task<Result<int, string>> SaveAsync(int order) => Task.FromResult(Save(order));

    public ValueTask<Validation<int, string>> ValidateAsync(int order) => new(Validate(order));

    public OrderService? Next { get; init; }

    public void Archive(int order) => /*RZT0001*/Save(order);
}
