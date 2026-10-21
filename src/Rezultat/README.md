# Rezultat

Explicit outcome carriers for .NET: `Result<T, TError>` for success or a typed error, `Maybe<T>` for an
optional value, `Presence<T>` for a missing, null or present value, `Validation<T, TIssue>` for accumulated
issues and `Unit` for an outcome without a payload. All of them are value types with synchronous composition.

The package targets `net10.0` and has no dependencies beyond the .NET framework.

## Install

```sh
dotnet add package Rezultat
```

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
- `default` of `Result` and `Validation` is uninitialized: `IsInitialized` is false and reading its branch or payload
  throws. `default` of `Maybe` is None.
- All operations are synchronous; there are no overloads for `Task` or `ValueTask` callbacks.
- This package contains no test assertion helpers and no analyzer.
