# Rezultat

Explicit outcome carriers for .NET: `Result<T, TError>` for success or a typed error, `Maybe<T>` for an
optional value, `Presence<T>` for a missing, null or present value, `Validation<T, TIssue>` for accumulated
issues and `Unit` for an outcome without a payload. All of them are value types with synchronous composition.

The package targets `net10.0` and has no dependencies beyond the .NET framework.

## Install

```sh
dotnet add package Rezultat
```

## States, defaults and null

| Type | `default` | `null` | Observing the state and payload |
| --- | --- | --- | --- |
| `Result<T, TError>` | Uninitialized: neither Ok nor Error | `Ok(null)` and `Error(null)` are kept as created branches when `T` or `TError` allows `null`; there is no null guard | `IsInitialized` is safe and is `false` only for `default`. On an uninitialized value `IsSuccess`, `TryGetValue`, `Match` and the composition operations throw `InvalidOperationException` |
| `Maybe<T>` | `None` | `Some(null)` is present and differs from `None`; `Maybe.FromNullable(null)` gives `None` | `default` is an ordinary `None`: `IsSome` and `TryGetValue` return `false`, `Match` calls the None handler. `true` from `IsSome` or `TryGetValue` does not mean the payload is non-null |
| `Presence<T>` (`T : notnull`) | `Missing` | `Null` is an explicit state; `Value(null)` throws `ArgumentNullException` | Exactly one of `IsMissing`, `IsNull`, `IsValue` is `true`. `TryGetValue` returns `true` and a non-null value only for `Value`; use the flags to tell `Missing` from `Null` |
| `Validation<T, TIssue>` | Uninitialized: neither Valid nor Invalid | `Valid(null)` and a `null` issue are kept; `InvalidMany` rejects a default or empty array | As for `Result`: `IsInitialized` is safe; `IsValid`, `TryGetValue`, `Match` and the composition operations throw `InvalidOperationException` on an uninitialized value |
| `Unit` | Valid: `default(Unit)`, `new Unit()` and `Unit.Value` are equal | No payload | Nothing to observe; there is no uninitialized state |

Equality, hashing and `ToString` of an uninitialized `Result` or `Validation` do not throw: two uninitialized
values of the same type are equal, and `ToString` shows `Result.Uninitialized` or `Validation.Uninitialized`.

**`Result` and `Validation` are not safe-to-log containers.** Their `ToString` shows the active payload or every
issue and calls the payload's own `ToString`, with its side effects and exceptions. String interpolation, a log
message argument, an assertion message or a debugger display can run it even without an explicit
`Console.WriteLine`. For diagnostics, select the facts that may be shown instead of passing the carrier:

```csharp
using Rezultat;

Result<Receipt, PaymentError> payment = Result<Receipt, PaymentError>.Error(new PaymentError("card-declined", "4111 1111 1111 1111"));

string state = !payment.IsInitialized ? "Uninitialized" : payment.IsSuccess ? "Ok" : "Error";
string? code = payment.IsInitialized && !payment.TryGetValue(out _, out var error) ? error.Code : null;
Console.WriteLine($"payment {state} {code}");

internal sealed record Receipt(string Id);

internal sealed record PaymentError(string Code, string CardNumber);
```

Which fields may be shown is a decision of your domain. Do not hand the carrier or its payload to structured
logging, an object dump or a serializer next to the selected fields. Overriding the payload's `ToString` covers only
formatting: a logger or serializer can read the properties itself. The package does no automatic redaction.

## Carriers and composition

`using Rezultat;` brings the carriers, `Maybe.FromNullable`, the bridges between Maybe, Result and Validation and
the fallible `Presence` projection.

```csharp
using Rezultat;

Result<int, string> Parse(string text) =>
    int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error($"not a number: {text}");

var total = Parse("40").Zip(Parse("2"), static (left, right) => left + right);
Console.WriteLine(total.Match(static value => $"total {value}", static error => error));

string? configured = null;
Result<string, string> host = Maybe.FromNullable(configured).ToResult("host is not configured");
Console.WriteLine(host);

Maybe<int> port = Parse("8080").ToMaybe();
Result<Maybe<int>, string> checkedPort = Maybe<string>.Some("443").Traverse(Parse);
Console.WriteLine($"{port} {checkedPort}");

Validation<string, string> CheckName(string name) =>
    name.Length > 0 ? Validation<string, string>.Valid(name) : Validation<string, string>.Invalid("name is empty");

Validation<int, string> CheckAge(int age) =>
    age >= 0 ? Validation<int, string>.Valid(age) : Validation<int, string>.Invalid("age is negative");

var person = CheckName("").Zip(CheckAge(-1), static (name, age) => (name, age));
Console.WriteLine(person);
```

Validation issues are an `ImmutableArray<TIssue>`; `Zip` keeps the issues of every invalid input in order.

### Observation, dependent checks and bridges

```csharp
using System.Collections.Immutable;
using Rezultat;

Result<int, string> Parse(string text) =>
    int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error($"not a number: {text}");

var parsed = Parse("x")
    .Inspect(static value => Console.WriteLine($"parsed {value}"))
    .InspectError(static error => Console.WriteLine($"rejected: {error}"));

Validation<ushort, string> port = Parse("70000").ToValidation()
    .Ensure(static value => value <= 65535, static value => $"port {value} is out of range")
    .Map(static value => (ushort)value);
Result<ushort, ImmutableArray<string>> checkedPort = port.ToResult();
Console.WriteLine($"{parsed} {port} {checkedPort.IsSuccess}");

Presence<string> field = Presence<string>.Value("8080");
Result<Presence<int>, string> update = field.FilterToNull(static text => text.Length > 0).Traverse(Parse);
Presence<int> positive = Presence<string>.Value("-1").AndThen(static text =>
    int.TryParse(text, out var value) ? Presence<int>.Value(value) : Presence<int>.Missing).FilterToMissing(static value => value > 0);
Console.WriteLine($"{update} {positive} {Presence<string>.Null.Traverse(Parse)}");
```

## Collection operations

Operations over `IEnumerable<T>`, `IDictionary<TKey, TValue>` and `IReadOnlyDictionary<TKey, TValue>` are in a
separate namespace of the same package and need `using Rezultat.Collections;`.

```csharp
using Rezultat;
using Rezultat.Collections;

Result<int, string> Parse(string text) =>
    int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error($"not a number: {text}");

Maybe<int> TryParse(string text) => int.TryParse(text, out var value) ? Maybe<int>.Some(value) : Maybe<int>.None;

Result<int[], string> numbers = new[] { "1", "2", "3" }.Traverse(Parse);
Console.WriteLine(numbers.Map(static values => values.Sum()));

Validation<int[], string> all = new[] { "1", "x", "y" }.Traverse(text =>
    Parse(text).Match(Validation<int, string>.Valid, Validation<int, string>.Invalid));
Console.WriteLine(all);

var ages = new Dictionary<string, int> { ["Ada"] = 36 };
Console.WriteLine($"{ages.GetOrNone("Ada")} {ages.GetOrNone("Eve")}");
Console.WriteLine(new[] { "Ada", "Bob" }.FirstOrNone(static name => name.StartsWith('B')));
Console.WriteLine(string.Join(",", new[] { "1", "x", "3" }.Choose(TryParse)));
```

| Static class | Operations |
| --- | --- |
| `Rezultat.ResultExtensions` | `ToMaybe`, `ToValidation` |
| `Rezultat.ValidationExtensions` | `ToResult` |
| `Rezultat.MaybeExtensions` | `Traverse` over a `Maybe<T>` |
| `Rezultat.PresenceExtensions` | `Traverse` over a `Presence<T>` with a Result selector |
| `Rezultat.Collections.ResultCollectionExtensions` | `Traverse` with a Result selector, `Sequence` |
| `Rezultat.Collections.MaybeCollectionExtensions` | `FirstOrNone`, `LastOrNone`, `SingleOrNone`, `AtMostOne`, `ElementAtOrNone`, `GetOrNone`, `Traverse` with a Maybe selector, `Sequence`, `Choose` |
| `Rezultat.Collections.ValidationCollectionExtensions` | `Traverse` with a Validation selector, `Sequence`, `ValidateAll` |
| `Rezultat.Linq.ResultQueryExtensions` | `Select`, `SelectMany` with a projection |
| `Rezultat.Linq.MaybeQueryExtensions` | `Select`, `SelectMany` with a projection, `Where` |

### Ready outcomes and multiplicity

`Sequence` combines outcomes that already exist exactly like `Traverse` with an identity selector. `ValidateAll`
collects only the issues and returns `Validation<Unit, TIssue>` without building an array of the valid values.
`AtMostOne` turns a second element into an expected error instead of an exception.

```csharp
using Rezultat;
using Rezultat.Collections;

Validation<int, string> Check(string text) =>
    int.TryParse(text, out var value) ? Validation<int, string>.Valid(value) : Validation<int, string>.Invalid($"not a number: {text}");

Result<int, string>[] ready = [Result<int, string>.Ok(1), Result<int, string>.Ok(2)];
Console.WriteLine(ready.Sequence().Map(static values => values.Sum()));
Console.WriteLine(new[] { Maybe<int>.Some(1), Maybe<int>.None }.Sequence());

Console.WriteLine(new[] { "1", "x", "y" }.ValidateAll(Check));
Console.WriteLine(new[] { Check("1"), Check("2") }.ValidateAll());

var admins = new[] { "Ada", "Bob", "Eve" };
Console.WriteLine(admins.Where(static name => name.StartsWith('A')).AtMostOne(static () => "several admins"));
Console.WriteLine(admins.AtMostOne(static () => "several admins"));
```

## Query syntax

`from`, `let`, `select` for Result and Maybe, and `where` for Maybe, are available after `using Rezultat.Linq;`.
Each later `from` runs only after the previous step succeeded; the first Error or None is the result.

```csharp
using Rezultat;
using Rezultat.Linq;

Result<int, string> Parse(string text) =>
    int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error($"not a number: {text}");

Maybe<int> Find(int id) => id > 0 ? Maybe<int>.Some(id * 10) : Maybe<int>.None;

var sum =
    from width in Parse("3")
    from height in Parse("4")
    let area = width * height
    select $"area {area}";
Console.WriteLine(sum);
Console.WriteLine(from width in Parse("3") from height in Parse("x") select width * height);

Console.WriteLine(from found in Find(2) where found > 10 select found + 1);
Console.WriteLine(from found in Find(1) where found > 10 select found + 1);
```

## Behavior to know

- `Ensure` is a dependent check: it runs only on a valid value and adds one issue. Chained `Ensure` calls stop at
  the first Invalid; independent checks accumulate through `Zip` or the collection `Traverse`.
- `Inspect`, `InspectError` and the Presence filters return the carrier as it was before the callback, even if the
  callback reassigns the variable it was called on; a mutable payload object is still shared, not copied.
  `Inspect` takes an `Action`: an `async` lambda becomes `async void`, so await asynchronous observation explicitly.
- Callbacks are checked for `null` even on the branch that does not invoke them.
- `ToValidation` keeps the error as one issue, even when it is a collection. `ToResult` keeps the issue array itself
  and compares it with `ImmutableArray` equality, so `ToResult` followed by `ToValidation` is not a round trip.
- `Presence.Traverse` and `Presence.Map` reject a `null` result instead of turning it into Null or Missing.

- Result and Maybe `Traverse` stop at the first failure; Validation `Traverse` visits every element and
  accumulates the issues. All three enumerate the source once and return a new array. `Choose` is lazy.
- A `Traverse` selector whose carrier type cannot be inferred, such as `null`, `default` or a throwing
  lambda with explicit type arguments, is ambiguous between the Result and Validation overloads. Use a typed
  selector or call the static class directly.
- `Sequence` behaves exactly like `Traverse` with an identity selector. `ValidateAll` visits every element and
  stops early only on an exception or an uninitialized Validation; one Invalid keeps its issue array.
- `AtMostOne` detects the second element without reading it or the rest of the source and calls the error factory
  only then. Filter first with `Where`; `SingleOrNone` still throws on a second element.
- Query `select` and `where` are `Map` and `Filter`; `SelectMany` checks its delegates even on Error or None and
  rejects an uninitialized Result. There is no Result `where`: use `Ensure` with an explicit error. Validation and
  Presence have no query syntax. Queries run immediately and are not enumerable.
- The compiler may not report nullable warnings inside query expressions as it does for lambdas; handle nullable
  values explicitly. If another imported query provider makes a call ambiguous, call the static class directly.
- For a type implementing both dictionary interfaces, `GetOrNone` prefers the `IReadOnlyDictionary` overload;
  callers compiled with C# 12 or earlier may get an ambiguity error instead.
- All operations are synchronous; there are no overloads for `Task` or `ValueTask` callbacks.
- This package contains no test assertion helpers and no analyzer; for tests see "Assertions in tests" below.

## Moving from local outcome types

### Extract both outcomes with one `TryGetValue`

`Result.TryGetValue` checks the branch once and gives both typed outputs. It replaces `.Value` and `.Error`
getters and pairs of single-output probes, without `Match` or a chain of combinators:

```csharp
using Rezultat;

Result<int, string> Parse(string text) =>
    int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error($"not a number: {text}");

static string MapError(string error) => $"rejected, {error}";
static string Use(int value) => $"next {value + 1}";

string Handle(Result<int, string> result)
{
    if (!result.TryGetValue(out var value, out var error))
    {
        return MapError(error);
    }

    return Use(value);
}

Console.WriteLine(Handle(Parse("41")));
Console.WriteLine(Handle(Parse("x")));
```

Use in each branch only its own output: the other one holds only a `default` value, and nullable analysis treats a
reference-type one as possibly `null`.
`Validation.TryGetValue` works the same way with the valid value or the `ImmutableArray` of issues.

### Uninitialized carriers and domain guards

A `default` `Result` or `Validation` is not an expected failure but a carrier that was never created.
`TryGetValue`, `IsSuccess`, `IsValid`, `Match` and the composition operations throw `InvalidOperationException`
on it. Among the branch checks only `IsInitialized` is safe; use it for code that inspects that state on purpose.
Equality, hashing and `ToString` do not throw either, as described above. Do not turn a forgotten
creation into Error or Invalid with a `catch` or an adapter that returns `false` on `default`; make every fallback
and early return create an explicit outcome.

`Ok(null)` and `Valid(null)` are allowed when the payload type allows `null`. That does not remove the non-null
rules of your domain: keep its guards where they were, in the same order and with the same exceptions, and do not
move them into the carrier or drop them to unify types. When updating the package, compare nullable annotations,
guards and operations with the release notes, not with the methods of the previous local type.

```csharp
using Rezultat;

Result<string?, string> found = Result<string?, string>.Ok(null);
Console.WriteLine(found.IsSuccess);

try
{
    _ = found.Map(static name => new CustomerName(name));
}
catch (ArgumentNullException)
{
    Console.WriteLine("the domain guard still rejects null");
}

internal sealed class CustomerName
{
    public CustomerName(string? value) => Value = value ?? throw new ArgumentNullException(nameof(value));

    public string Value { get; }
}
```

### Independent checks first, the object last

Run the independent checks first and construct the target object only in the selector of the successful
composition. The selector, and the constructor in it, runs only when every check is Valid:

```csharp
using Rezultat;

var constructed = 0;

Validation<string, string> CheckName(string name) =>
    name.Length > 0 ? Validation<string, string>.Valid(name) : Validation<string, string>.Invalid("name is empty");

Validation<int, string> CheckAge(int age) =>
    age >= 0 ? Validation<int, string>.Valid(age) : Validation<int, string>.Invalid("age is negative");

var person = CheckName("").Zip(CheckAge(-1), (name, age) =>
{
    constructed++;
    return new Person(name, age);
});

if (!person.TryGetValue(out _, out var issues))
{
    Console.WriteLine($"{string.Join("; ", issues)}; constructed {constructed}");
}

internal sealed record Person(string Name, int Age);
```

C# evaluates ordinary arguments before the call: an object created in advance or a constructor call written as
an argument has already run, and `Zip` cannot defer it. The issues keep the order of the inputs. Use `AndThen` for
a check that needs a valid value and `ValidateAll` when only the issues matter; a large validator can stay an
ordinary loop with a BCL builder. When an error carries domain facts beyond a list of issues, such as a truncation
flag, keep it as a specific `TError` in a `Result` instead of converting every failure to `Validation`, and do not
construct a partially valid object.

### Assertions in tests

With xUnit, the optional `Rezultat.Testing.Xunit` package gives `ShouldBeOk`, `ShouldBeError`, `ShouldBeValid`
and `ShouldBeInvalid`. From its version 0.8.2 each has an `includePayload: false` overload whose failure message
names only the expected and actual branches; version 0.8.0 does not have it. The flag limits only that message:
later assertions and runner output can still show the data.

With Shouldly, a test project can keep a small local recipe of the same four extractions instead. It needs only
this package and Shouldly, not `Rezultat.Testing.Xunit`:

```csharp shouldly
using System.Collections.Immutable;
using Rezultat;
using Shouldly;

internal static class RezultatShouldlyExtensions
{
    public static T ShouldBeOk<T, TError>(this Result<T, TError> result) =>
        result.TryGetValue(out var value, out _) ? value : throw new ShouldAssertException("Expected Result.Ok; actual Result.Error.");

    public static TError ShouldBeError<T, TError>(this Result<T, TError> result) =>
        result.TryGetValue(out _, out var error) ? throw new ShouldAssertException("Expected Result.Error; actual Result.Ok.") : error;

    public static T ShouldBeValid<T, TIssue>(this Validation<T, TIssue> validation) =>
        validation.TryGetValue(out var value, out _) ? value : throw new ShouldAssertException("Expected Validation.Valid; actual Validation.Invalid.");

    public static ImmutableArray<TIssue> ShouldBeInvalid<T, TIssue>(this Validation<T, TIssue> validation) =>
        validation.TryGetValue(out _, out var issues) ? throw new ShouldAssertException("Expected Validation.Invalid; actual Validation.Valid.") : issues;
}
```

Its checks, written here with xUnit attributes; use the test attribute of your framework:

```csharp shouldly
using System.Collections.Immutable;
using Rezultat;
using Shouldly;
using Xunit;

public sealed class RezultatShouldlyRecipeTests
{
    [Fact]
    public void ExtractsTheActiveBranch()
    {
        Result<int, string>.Ok(42).ShouldBeOk().ShouldBe(42);
        Result<int, string>.Error("refused").ShouldBeError().ShouldBe("refused");
        Validation<string?, string>.Valid(null).ShouldBeValid().ShouldBeNull();

        var issues = ImmutableArray.Create("bad name", "bad name");
        Validation<int, string>.InvalidMany(issues).ShouldBeInvalid().ShouldBe(issues);
    }

    [Fact]
    public void WrongBranchNamesOnlyTheBranches()
    {
        var failure = Should.Throw<ShouldAssertException>(() => Result<int, string>.Error("secret").ShouldBeOk());
        failure.Message.ShouldNotContain("secret");
    }

    [Fact]
    public void UninitializedKeepsTheCarrierGuard() =>
        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).ShouldBeValid());
}
```

- A wrong branch throws `ShouldAssertException` directly with constant branch names; the payload and the issues
  are not formatted. Do not replace the `throw` with a fail callback that could return.
- Success returns the exact payload, still nullable when its type is, or the stored issue array.
- An uninitialized carrier keeps the native `InvalidOperationException` of `TryGetValue`. This differs from the
  xUnit package, which reports `Uninitialized` as an assertion failure; check `IsInitialized` explicitly when a test
  studies `default`.
- If you add a `customMessage` parameter, its content is your responsibility: interpolating the payload into it
  shows the data again. Later Shouldly assertions such as `ShouldBe` format their own values.

## Release notes

- 0.8.2: documentation only. Adds the formatting warning next to the states table, the guide for moving from
  local outcome types and the Shouldly recipe. Compared with 0.8.1, operations, signatures, nullable annotations,
  guards and exceptions, namespaces and overload binding are unchanged; no action is needed besides updating the
  version.
- 0.8.1: documentation only. Adds the states table and these notes. Compared with 0.8.0, operations,
  signatures, nullable annotations, guards and exceptions, namespaces and overload binding are unchanged; no
  action is needed besides updating the version.
- 0.8.0: the first issued `Rezultat` package. Its API, as described in this README, is the baseline; it is not a
  change relative to similar types in code outside this package.
