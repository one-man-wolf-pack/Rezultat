using AnalyzerLibrary;
using Rezultat;

namespace AnalyzerConsumer;

public sealed class Corpus(OrderService orders)
{
    private Result<int, string> _held;

    public static void RunStep(Action step) => step();

    public static void RunStepAsync(Func<Task> step) => step().GetAwaiter().GetResult();

    public static void Consume(Result<int, string> outcome) => _ = outcome;

    public void Bare()
    {
        /*RZT0001*/
        orders.Save(1);
        /*RZT0001*/
        orders.Validate(2);
        /*RZT0001*/
        orders.Cancel(3);
        /*RZT0001*/
        Result<int, string>.Ok(4);
        /*RZT0001*/
        Validation<int, string>.Invalid("issue");
        /*RZT0001*/
        new Result<int, string>();
        /*RZT0001*/
        Result<Task<int>, string>.Ok(Task.FromResult(5));
        /*RZT0001*/
        Result<string?, string?>.Ok(null);
    }

    public void ExpressionBody() => /*RZT0001*/orders.Save(6);

    public async Task Awaited()
    {
        /*RZT0001*/
        await orders.SaveAsync(7);
        /*RZT0001*/
        await orders.ValidateAsync(8);
        /*RZT0001*/
        await orders.SaveAsync(9).ConfigureAwait(false);
        /*RZT0001*//*CS4014*/
        orders.SaveAsync(10);
    }

    public void Unawaited()
    {
        /*RZT0001*/
        orders.SaveAsync(11);
        /*RZT0001*/
        orders.ValidateAsync(12);
    }

    public void Callbacks()
    {
        RunStep(() => /*RZT0001*/orders.Save(13));
        RunStep(() => { /*RZT0001*/orders.Validate(14); });
        RunStep(async () => /*RZT0001*/await orders.SaveAsync(15));
        RunStepAsync(async () => /*RZT0001*/await orders.ValidateAsync(16));
    }

    public async Task Conditional()
    {
        /*RZT0001*/
        orders.Next?.Save(17);
        /*RZT0001*/
        orders.Next?.ValidateAsync(18);
        if (orders.Next is not null)
        {
            /*RZT0001*/
            await orders.Next.SaveAsync(19);
        }
    }

    public async Task<Result<int, string>> Kept()
    {
        _ = orders.Save(20);
        _ = await orders.SaveAsync(21);
        _ = orders.SaveAsync(22);
        RunStep(() => { _ = orders.Save(23); });
        var local = orders.Save(24);
        _held = orders.Save(25);
        Consume(orders.Save(26));
        object boxed = orders.Validate(27);
        _ = orders.Save(28).IsSuccess;
        _ = new List<Result<int, string>> { orders.Save(29) };
        Maybe<int>.Some(30);
        Presence<int>.Value(31);
        Other.Result<int, string>.Make();
        _ = boxed;
        return local.IsSuccess ? _held : await orders.SaveAsync(32);
    }
}
