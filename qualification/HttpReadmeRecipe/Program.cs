using Microsoft.AspNetCore.Http.HttpResults;
using Rezultat;
using Rezultat.Qualification;

var checks = new Checks();
var order = new Order(7, "cancelled");

checks.That("cancelled order is 200 with the order",
    OrderResponses.Cancel(Result<Order, OrderError>.Ok(order)) is Ok<Order> { StatusCode: 200 } ok && ok.Value == order);
checks.That("missing order is 404",
    OrderResponses.Cancel(Result<Order, OrderError>.Error(new OrderError.NotFound(7))) is NotFound { StatusCode: 404 });
checks.That("shipped order is 409 with its message",
    OrderResponses.Cancel(Result<Order, OrderError>.Error(new OrderError.AlreadyShipped(7)))
        is Conflict<string> { StatusCode: 409, Value: "order 7 has already shipped" });

checks.That("created order is 201 with its location",
    OrderResponses.Create(Validation<Order, string>.Valid(order)) is Created<Order> { StatusCode: 201, Location: "/orders/7" } created
    && created.Value == order);
var invalid = OrderResponses.Create(Validation<Order, string>.InvalidMany(["quantity is negative", "address is empty", "quantity is negative"]));
checks.That("invalid order is 400 with every issue in order",
    invalid is ValidationProblem { StatusCode: 400 } problem
    && problem.ProblemDetails.Errors.Keys.SequenceEqual(["order"])
    && problem.ProblemDetails.Errors["order"].SequenceEqual(["quantity is negative", "address is empty", "quantity is negative"]));

return checks.Complete();
