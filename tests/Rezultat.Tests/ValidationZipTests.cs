using System.Collections.Immutable;

namespace Rezultat.Tests;

public sealed class ValidationZipTests
{
    public static TheoryData<int> ThreeInputMasks => [.. Enumerable.Range(0, 8)];

    public static TheoryData<int> FourInputMasks => [.. Enumerable.Range(0, 16)];

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Binary_combinations_accumulate_issues_in_operand_order(bool leftInvalid, bool rightInvalid)
    {
        var calls = 0;
        var left = leftInvalid ? Validation<int, string>.InvalidMany(["l1", "l2"]) : Validation<int, string>.Valid(1);
        var right = rightInvalid ? Validation<string, string>.Invalid("r") : Validation<string, string>.Valid("x");

        var result = left.Zip(right, (a, b) => { calls++; return $"{a}{b}"; });

        string[] expected = [.. (leftInvalid ? new[] { "l1", "l2" } : []), .. (rightInvalid ? new[] { "r" } : [])];
        if (expected.Length == 0)
        {
            Assert.Equal(Validation<string, string>.Valid("1x"), result);
            Assert.Equal(1, calls);
        }
        else
        {
            Assert.Equal(Validation<string, string>.InvalidMany([.. expected]), result);
            Assert.Equal(0, calls);
        }
    }

    [Theory]
    [MemberData(nameof(ThreeInputMasks))]
    public void Three_inputs_accumulate_every_invalid_in_operand_order(int invalidMask)
    {
        var calls = 0;
        var inputs = Enumerable.Range(0, 3).Select(index => Input(index, invalidMask)).ToArray();

        var result = inputs[0].Zip(inputs[1], inputs[2], (a, b, c) => { calls++; return a + b + c; });

        AssertCombined(result, invalidMask, 3, calls, expectedSum: 0 + 1 + 2);
    }

    [Theory]
    [MemberData(nameof(FourInputMasks))]
    public void Four_inputs_accumulate_every_invalid_in_operand_order(int invalidMask)
    {
        var calls = 0;
        var inputs = Enumerable.Range(0, 4).Select(index => Input(index, invalidMask)).ToArray();

        var result = inputs[0].Zip(inputs[1], inputs[2], inputs[3], (a, b, c, d) => { calls++; return a + b + c + d; });

        AssertCombined(result, invalidMask, 4, calls, expectedSum: 0 + 1 + 2 + 3);
    }

    [Fact]
    public void Selector_receives_values_in_operand_order_with_different_success_types()
    {
        var result = Validation<int, string>.Valid(1).Zip(
            Validation<string, string>.Valid("b"),
            Validation<bool, string>.Valid(true),
            Validation<char, string>.Valid('d'),
            static (a, b, c, d) => (a, b, c, d));

        Assert.Equal(Validation<(int, string, bool, char), string>.Valid((1, "b", true, 'd')), result);
    }

    [Fact]
    public void Issue_contents_keep_inner_order_nulls_and_duplicates()
    {
        var result = Validation<int, string?>.InvalidMany(["a", null]).Zip(
            Validation<int, string?>.InvalidMany(["a", "a"]),
            static (a, b) => a + b);

        Assert.Equal(Validation<int, string?>.InvalidMany(["a", null, "a", "a"]), result);
    }

    [Fact]
    public void Uninitialized_receiver_is_rejected_before_inputs_and_selector()
    {
        var calls = 0;

        Assert.Throws<InvalidOperationException>(() => default(Validation<int, string>).Zip<int, int>(default, null!));
        Assert.Throws<InvalidOperationException>(() => default(Validation<int, string>).Zip(Validation<int, string>.Invalid("e"), (a, b) => ++calls));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Uninitialized_operand_is_rejected_even_next_to_invalid_and_before_selector_guard()
    {
        var invalid = Validation<int, string>.Invalid("e");

        Assert.Throws<InvalidOperationException>(() => invalid.Zip<int, int>(default, null!));
        Assert.Throws<InvalidOperationException>(() => invalid.Zip<int, int, int>(invalid, default, null!));
        Assert.Throws<InvalidOperationException>(() => invalid.Zip<int, int, int, int>(invalid, invalid, default, null!));
    }

    [Fact]
    public void Null_selector_is_rejected_for_every_arity_and_combination()
    {
        var valid = Validation<int, string>.Valid(1);
        var invalid = Validation<int, string>.Invalid("e");

        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => invalid.Zip<int, int>(invalid, null!)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => valid.Zip<int, int>(valid, null!)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => invalid.Zip<int, int, int>(valid, valid, null!)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => valid.Zip<int, int, int, int>(valid, valid, invalid, null!)).ParamName);
    }

    [Fact]
    public void Selector_exception_propagates_unchanged()
    {
        var thrown = new OperationCanceledException();
        var valid = Validation<int, string>.Valid(1);

        Assert.Same(thrown, Assert.Throws<OperationCanceledException>(() => valid.Zip<int, int>(valid, (_, _) => throw thrown)));
    }

    [Fact]
    public void Selector_result_may_be_null_or_an_uninitialized_payload()
    {
        var valid = Validation<int, string>.Valid(1);

        Assert.Equal(Validation<string?, string>.Valid(null), valid.Zip(valid, static (_, _) => (string?)null));
        var nested = valid.Zip(valid, static (_, _) => default(Validation<int, string>));
        Assert.True(nested.TryGetValue(out var inner, out _) && !inner.IsInitialized);
    }

    [Fact]
    public void Async_selector_result_is_an_ordinary_payload()
    {
        var valid = Validation<int, string>.Valid(1);

        var result = valid.Zip(valid, static (a, b) => Task.FromException<int>(new ProbeException("task")));

        Assert.True(result.TryGetValue(out var task, out _) && task.IsFaulted);
    }

    [Fact]
    public void Inputs_are_not_changed()
    {
        var left = Validation<int, string>.Invalid("l");
        var right = Validation<int, string>.Invalid("r");

        _ = left.Zip(right, static (a, b) => a + b);

        Assert.Equal(Validation<int, string>.Invalid("l"), left);
        Assert.Equal(Validation<int, string>.Invalid("r"), right);
    }

    private static Validation<int, string> Input(int index, int invalidMask) =>
        (invalidMask & (1 << index)) != 0
            ? Validation<int, string>.InvalidMany([$"i{index}a", $"i{index}b"])
            : Validation<int, string>.Valid(index);

    private static void AssertCombined(Validation<int, string> result, int invalidMask, int arity, int calls, int expectedSum)
    {
        var expected = Enumerable.Range(0, arity)
            .Where(index => (invalidMask & (1 << index)) != 0)
            .SelectMany(index => new[] { $"i{index}a", $"i{index}b" })
            .ToImmutableArray();
        if (expected.IsEmpty)
        {
            Assert.Equal(Validation<int, string>.Valid(expectedSum), result);
            Assert.Equal(1, calls);
        }
        else
        {
            Assert.Equal(Validation<int, string>.InvalidMany(expected), result);
            Assert.Equal(0, calls);
        }
    }
}
