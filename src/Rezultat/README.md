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

`using Rezultat;` brings the carriers, `Maybe.FromNullable` and the bridges between Maybe and Result.

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
| `Rezultat.ResultExtensions` | `ToMaybe` |
| `Rezultat.MaybeExtensions` | `Traverse` over a `Maybe<T>` |
| `Rezultat.Collections.ResultCollectionExtensions` | `Traverse` with a Result selector |
| `Rezultat.Collections.MaybeCollectionExtensions` | `FirstOrNone`, `LastOrNone`, `SingleOrNone`, `ElementAtOrNone`, `GetOrNone`, `Traverse` with a Maybe selector, `Choose` |
| `Rezultat.Collections.ValidationCollectionExtensions` | `Traverse` with a Validation selector |

## Behavior to know

- Result and Maybe `Traverse` stop at the first failure; Validation `Traverse` visits every element and
  accumulates the issues. All three enumerate the source once and return a new array. `Choose` is lazy.
- A `Traverse` selector whose carrier type cannot be inferred, such as `null`, `default` or a throwing
  lambda with explicit type arguments, is ambiguous between the Result and Validation overloads. Use a typed
  selector or call the static class directly.
- For a type implementing both dictionary interfaces, `GetOrNone` prefers the `IReadOnlyDictionary` overload;
  callers compiled with C# 12 or earlier may get an ambiguity error instead.
- `default` of `Result` and `Validation` is uninitialized: `IsInitialized` is false and reading its branch or payload
  throws. `default` of `Maybe` is None.
- All operations are synchronous; there are no overloads for `Task` or `ValueTask` callbacks.
- This package contains no test assertion helpers and no analyzer.
