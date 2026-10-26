using System.Collections.Immutable;

namespace Rezultat.Tests;

public sealed class ValidationZipTests {
    public static TheoryData<int> ThreeInputMasks => [.. Enumerable.Range(0, 8)];

    public static TheoryData<int> FourInputMasks => [.. Enumerable.Range(0, 16)];

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Binary_combinations_accumulate_issues_in_operand_order(bool leftInvalid, bool rightInvalid) {
        int calls = 0;
        var left = leftInvalid ? Validation<int, string>.InvalidMany(["l1", "l2"]) : Validation<int, string>.Valid(1);
        var right = rightInvalid ? Validation<string, string>.Invalid("r") : Validation<string, string>.Valid("x");

        var result = left.Zip(right, (a, b) => { calls++; return $"{a}{b}"; });

        string[] expected = [.. leftInvalid ? new[] { "l1", "l2" } : [], .. rightInvalid ? new[] { "r" } : []];
        if (expected.Length == 0) {
            result.ShouldBe(Validation<string, string>.Valid("1x"));
            calls.ShouldBe(1);
        } else {
            result.ShouldBe(Validation<string, string>.InvalidMany([.. expected]));
            calls.ShouldBe(0);
        }
    }

    [Theory]
    [MemberData(nameof(ThreeInputMasks))]
    public void Three_inputs_accumulate_every_invalid_in_operand_order(int invalidMask) {
        int calls = 0;
        var inputs = Enumerable.Range(0, 3).Select(index => Input(index, invalidMask)).ToArray();

        var result = inputs[0].Zip(inputs[1], inputs[2], (a, b, c) => { calls++; return a + b + c; });

        AssertCombined(result, invalidMask, 3, calls, expectedSum: 0 + 1 + 2);
    }

    [Theory]
    [MemberData(nameof(FourInputMasks))]
    public void Four_inputs_accumulate_every_invalid_in_operand_order(int invalidMask) {
        int calls = 0;
        var inputs = Enumerable.Range(0, 4).Select(index => Input(index, invalidMask)).ToArray();

        var result = inputs[0].Zip(inputs[1], inputs[2], inputs[3], (a, b, c, d) => { calls++; return a + b + c + d; });

        AssertCombined(result, invalidMask, 4, calls, expectedSum: 0 + 1 + 2 + 3);
    }

    [Fact]
    public void Selector_receives_values_in_operand_order_with_different_success_types() {
        var result = Validation<int, string>.Valid(1).Zip(
            Validation<string, string>.Valid("b"),
            Validation<bool, string>.Valid(true),
            Validation<char, string>.Valid('d'),
            static (a, b, c, d) => (a, b, c, d));

        result.ShouldBe(Validation<(int, string, bool, char), string>.Valid((1, "b", true, 'd')));
    }

    [Fact]
    public void Issue_contents_keep_inner_order_nulls_and_duplicates() {
        var result = Validation<int, string?>.InvalidMany(["a", null]).Zip(
            Validation<int, string?>.InvalidMany(["a", "a"]),
            static (a, b) => a + b);

        result.ShouldBe(Validation<int, string?>.InvalidMany(["a", null, "a", "a"]));
    }

    [Fact]
    public void Uninitialized_receiver_is_rejected_before_inputs_and_selector() {
        int calls = 0;

        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).Zip<int, int>(default, null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).Zip(Validation<int, string>.Invalid("e"), (a, b) => ++calls)).ShouldBeOfType<InvalidOperationException>();
        calls.ShouldBe(0);
    }

    [Fact]
    public void Uninitialized_operand_is_rejected_even_next_to_invalid_and_before_selector_guard() {
        var invalid = Validation<int, string>.Invalid("e");

        Should.Throw<InvalidOperationException>(() => invalid.Zip<int, int>(default, null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => invalid.Zip<int, int, int>(invalid, default, null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => invalid.Zip<int, int, int, int>(invalid, invalid, default, null!)).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Null_selector_is_rejected_for_every_arity_and_combination() {
        var valid = Validation<int, string>.Valid(1);
        var invalid = Validation<int, string>.Invalid("e");

        Should.Throw<ArgumentNullException>(() => invalid.Zip<int, int>(invalid, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
        Should.Throw<ArgumentNullException>(() => valid.Zip<int, int>(valid, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
        Should.Throw<ArgumentNullException>(() => invalid.Zip<int, int, int>(valid, valid, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
        Should.Throw<ArgumentNullException>(() => valid.Zip<int, int, int, int>(valid, valid, invalid, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
    }

    [Fact]
    public void Selector_exception_propagates_unchanged() {
        var thrown = new OperationCanceledException();
        var valid = Validation<int, string>.Valid(1);

        Should.Throw<OperationCanceledException>(() => valid.Zip<int, int>(valid, (_, _) => throw thrown)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(thrown);
    }

    [Fact]
    public void Selector_result_may_be_null_or_an_uninitialized_payload() {
        var valid = Validation<int, string>.Valid(1);

        valid.Zip(valid, static (_, _) => (string?)null).ShouldBe(Validation<string?, string>.Valid(null));
        var nested = valid.Zip(valid, static (_, _) => default(Validation<int, string>));
        (nested.TryGetValue(out var inner, out _) && !inner.IsInitialized).ShouldBeTrue();
    }

    [Fact]
    public void Async_selector_result_is_an_ordinary_payload() {
        var valid = Validation<int, string>.Valid(1);

        var result = valid.Zip(valid, static (a, b) => Task.FromException<int>(new ProbeException("task")));

        (result.TryGetValue(out var task, out _) && task.IsFaulted).ShouldBeTrue();
    }

    [Fact]
    public void Inputs_are_not_changed() {
        var left = Validation<int, string>.Invalid("l");
        var right = Validation<int, string>.Invalid("r");

        _ = left.Zip(right, static (a, b) => a + b);

        left.ShouldBe(Validation<int, string>.Invalid("l"));
        right.ShouldBe(Validation<int, string>.Invalid("r"));
    }

    private static Validation<int, string> Input(int index, int invalidMask) =>
        (invalidMask & (1 << index)) != 0
            ? Validation<int, string>.InvalidMany([$"i{index}a", $"i{index}b"])
            : Validation<int, string>.Valid(index);

    private static void AssertCombined(Validation<int, string> result, int invalidMask, int arity, int calls, int expectedSum) {
        var expected = Enumerable.Range(0, arity)
            .Where(index => (invalidMask & (1 << index)) != 0)
            .SelectMany(index => new[] { $"i{index}a", $"i{index}b" })
            .ToImmutableArray();
        if (expected.IsEmpty) {
            result.ShouldBe(Validation<int, string>.Valid(expectedSum));
            calls.ShouldBe(1);
        } else {
            result.ShouldBe(Validation<int, string>.InvalidMany(expected));
            calls.ShouldBe(0);
        }
    }
}
