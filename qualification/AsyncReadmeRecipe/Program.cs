using Rezultat;
using Rezultat.Qualification;

var checks = new Checks();
var order = new Order(7, 12.5m);
var receipt = new Receipt(7, "r-1");

{
    var orders = new PendingOrders();
    var payments = new PendingPayments();
    using var cancellation = new CancellationTokenSource();
    var paying = new Checkout(orders, payments).PayAsync(7, cancellation.Token);
    checks.That("success: waits for the pending load", !paying.IsCompleted && payments.Calls == 0);
    orders.Complete(Result<Order, CheckoutError>.Ok(order));
    checks.That("success: charge starts after the load with the caller's token",
        !paying.IsCompleted && payments.Calls == 1 && payments.Charged == order && payments.Token == cancellation.Token);
    payments.Complete(Result<Receipt, CheckoutError>.Ok(receipt));
    var paid = await paying;
    checks.That("success: returns the receipt", paid.TryGetValue(out var value, out _) && value == receipt);
    checks.That("success: the load received the caller's token", orders.Token == cancellation.Token);
}

{
    var orders = new PendingOrders();
    var payments = new PendingPayments();
    var missing = new CheckoutError.NotFound(7);
    var paying = new Checkout(orders, payments).PayAsync(7, CancellationToken.None);
    orders.Complete(Result<Order, CheckoutError>.Error(missing));
    var paid = await paying;
    checks.That("error: returns the load error", !paid.TryGetValue(out _, out var error) && ReferenceEquals(error, missing));
    checks.That("error: the charge does not start", payments.Calls == 0);
}

{
    var orders = new PendingOrders();
    var payments = new PendingPayments();
    using var cancellation = new CancellationTokenSource();
    var paying = new Checkout(orders, payments).PayAsync(7, cancellation.Token);
    await cancellation.CancelAsync();
    checks.That("cancelled load: the task is canceled, not an Error", paying.IsCanceled);
    checks.That("cancelled load: the charge does not start", payments.Calls == 0);
    await CheckCanceled("cancelled load", paying, cancellation.Token);
}

{
    var orders = new PendingOrders();
    var payments = new PendingPayments();
    using var cancellation = new CancellationTokenSource();
    var paying = new Checkout(orders, payments).PayAsync(7, cancellation.Token);
    orders.Complete(Result<Order, CheckoutError>.Ok(order));
    checks.That("cancelled charge: the charge is pending", !paying.IsCompleted && payments.Calls == 1);
    await cancellation.CancelAsync();
    checks.That("cancelled charge: the task is canceled, not an Error", paying.IsCanceled);
    await CheckCanceled("cancelled charge", paying, cancellation.Token);
}

{
    var orders = new PendingOrders();
    var payments = new PendingPayments();
    var fault = new InvalidOperationException("payment gateway defect");
    var paying = new Checkout(orders, payments).PayAsync(7, CancellationToken.None);
    orders.Complete(Result<Order, CheckoutError>.Ok(order));
    payments.Fail(fault);
    checks.That("fault: the task is faulted with the same exception",
        paying.IsFaulted && paying.Exception?.InnerExceptions is [var only] && ReferenceEquals(only, fault));
    try {
        _ = await paying;
        checks.Fail("fault: await returned");
    } catch (InvalidOperationException exception) {
        checks.That("fault: await rethrows the original exception", ReferenceEquals(exception, fault));
    }
}

return checks.Complete();

async Task CheckCanceled(string name, Task<Result<Receipt, CheckoutError>> paying, CancellationToken token) {
    try {
        _ = await paying;
        checks.Fail($"{name}: await returned");
    } catch (OperationCanceledException exception) {
        checks.That($"{name}: await throws the caller's cancellation", exception.CancellationToken == token);
    }
}

internal sealed class PendingOrders : IOrders {
    private readonly TaskCompletionSource<Result<Order, CheckoutError>> _completion = new();

    public CancellationToken Token { get; private set; }

    public Task<Result<Order, CheckoutError>> LoadAsync(int orderId, CancellationToken cancellationToken) {
        Token = cancellationToken;
        cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
        return _completion.Task;
    }

    public void Complete(Result<Order, CheckoutError> outcome) => _completion.SetResult(outcome);
}

internal sealed class PendingPayments : IPayments {
    private readonly TaskCompletionSource<Result<Receipt, CheckoutError>> _completion = new();

    public int Calls { get; private set; }

    public Order? Charged { get; private set; }

    public CancellationToken Token { get; private set; }

    public Task<Result<Receipt, CheckoutError>> ChargeAsync(Order order, CancellationToken cancellationToken) {
        Calls++;
        Charged = order;
        Token = cancellationToken;
        cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
        return _completion.Task;
    }

    public void Complete(Result<Receipt, CheckoutError> outcome) => _completion.SetResult(outcome);

    public void Fail(Exception exception) => _completion.SetException(exception);
}
