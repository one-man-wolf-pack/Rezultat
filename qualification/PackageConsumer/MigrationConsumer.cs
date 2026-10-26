using System.Collections.Immutable;
using Rezultat;
using Rezultat.Qualification;

namespace PackageConsumer.Migration;

internal static class MigrationConsumer {
    public static void Run(Checks checks) {
        checks.Text("two-out success uses the value", "next 42", Handle(Parse("41")));
        checks.Text("two-out failure uses the error", "rejected, not a number: x", Handle(Parse("x")));
        checks.Throws<InvalidOperationException>("two-out rejects an uninitialized carrier", () => Handle(default));

        var found = Result<string?, string>.Ok(null);
        checks.That("Ok(null) is a created success", found.IsInitialized && found.IsSuccess);
        checks.Throws<ArgumentNullException>("domain guard still rejects null after Ok(null)", () => found.Map(static name => new CustomerName(name)));
        checks.Throws<ArgumentNullException>("domain guard still rejects null after Valid(null)",
            () => Validation<string?, string>.Valid(null).Map(static name => new CustomerName(name)));

        int constructed = 0;
        var invalid = CheckName("").Zip(CheckAge(-1), (name, age) => {
            constructed++;
            return new Person(name, age);
        });
        checks.That("invalid inputs construct nothing and keep issue order",
            constructed == 0 && !invalid.TryGetValue(out _, out var issues)
            && issues.SequenceEqual(["name is empty", "age is negative"]));
        var partly = CheckName("Ada").Zip(CheckAge(-1), (name, age) => {
            constructed++;
            return new Person(name, age);
        });
        checks.That("one invalid input constructs nothing", constructed == 0 && !partly.TryGetValue(out _, out var single) && single.Length == 1);
        var valid = CheckName("Ada").Zip(CheckAge(36), (name, age) => {
            constructed++;
            return new Person(name, age);
        });
        checks.That("valid inputs construct once", constructed == 1 && valid.TryGetValue(out var person, out _) && person == new Person("Ada", 36));

        var repeated = ImmutableArray.Create("dup", "dup");
        checks.That("ordered repeated issues are kept by Zip",
            !Validation<int, string>.InvalidMany(repeated).Zip(CheckAge(-1), static (left, right) => left + right).TryGetValue(out _, out var combined)
            && combined.SequenceEqual(["dup", "dup", "age is negative"]));

        var payment = Result<int, PaymentError>.Error(new PaymentError("card-declined", "4111 1111 1111 1111"));
        string state = !payment.IsInitialized ? "Uninitialized" : payment.IsSuccess ? "Ok" : "Error";
        string? code = payment.IsInitialized && !payment.TryGetValue(out _, out var error) ? error.Code : null;
        checks.That("selected facts omit the card number", $"{state} {code}" == "Error card-declined" && !$"{state} {code}".Contains("4111", StringComparison.Ordinal));
        checks.That("the carrier text itself shows the card number", payment.ToString().Contains("4111", StringComparison.Ordinal));
    }

    private static string Handle(Result<int, string> result) {
        if (!result.TryGetValue(out int value, out string? error)) {
            return $"rejected, {error}";
        }

        return $"next {value + 1}";
    }

    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, out int value) ? Result<int, string>.Ok(value) : Result<int, string>.Error($"not a number: {text}");

    private static Validation<string, string> CheckName(string name) =>
        name.Length > 0 ? Validation<string, string>.Valid(name) : Validation<string, string>.Invalid("name is empty");

    private static Validation<int, string> CheckAge(int age) =>
        age >= 0 ? Validation<int, string>.Valid(age) : Validation<int, string>.Invalid("age is negative");

    private sealed record Person(string Name, int Age);

    private sealed record PaymentError(string Code, string CardNumber);

    private sealed class CustomerName {
        public CustomerName(string? value) => Value = value ?? throw new ArgumentNullException(nameof(value));

        public string Value { get; }
    }
}
