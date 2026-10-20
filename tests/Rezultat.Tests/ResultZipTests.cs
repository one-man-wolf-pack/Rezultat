namespace Rezultat.Tests;

public sealed class ResultZipTests
{
    [Fact]
    public void Zip_of_two_successes_invokes_selector_once_with_values_in_operand_order()
    {
        var calls = new List<(int Left, string Right)>();

        var result = Result<int, string>.Ok(1).Zip(Result<string, string>.Ok("b"), (left, right) =>
        {
            calls.Add((left, right));
            return $"{left}{right}";
        });

        Assert.Equal(Result<string, string>.Ok("1b"), result);
        Assert.Equal([(1, "b")], calls);
    }

    [Theory]
    [InlineData(false, true, "left")]
    [InlineData(true, false, "right")]
    [InlineData(false, false, "left")]
    public void Zip_with_failure_keeps_first_error_by_operand_position_and_skips_selector(
        bool leftSuccess, bool rightSuccess, string expected)
    {
        var errors = new Dictionary<string, ReferenceProbe> { ["left"] = new(1), ["right"] = new(2) };
        var left = leftSuccess ? Result<int, ReferenceProbe>.Ok(1) : Result<int, ReferenceProbe>.Error(errors["left"]);
        var right = rightSuccess ? Result<int, ReferenceProbe>.Ok(2) : Result<int, ReferenceProbe>.Error(errors["right"]);
        var calls = 0;

        var result = left.Zip(right, (a, b) => ++calls + a + b);

        Assert.False(result.TryGetValue(out _, out var observed));
        Assert.Same(errors[expected], observed);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Zip_keeps_null_error_of_first_failed_operand()
    {
        var result = Result<int, string?>.Error(null).Zip(Result<int, string?>.Error("right"), static (a, b) => a + b);

        Assert.Equal(Result<int, string?>.Error(null), result);
    }

    [Fact]
    public void Zip_passes_null_and_default_payloads_and_accepts_null_result()
    {
        (string? Left, int Right) received = ("unset", -1);

        var result = Result<string?, Unit>.Ok(null).Zip<int, string?>(Result<int, Unit>.Ok(0), (left, right) =>
        {
            received = (left, right);
            return null;
        });

        Assert.Equal((null, 0), received);
        Assert.Equal(Result<string?, Unit>.Ok(null), result);
    }

    [Fact]
    public void Zip_keeps_same_type_failure_branch()
    {
        Assert.Equal(
            Result<int, int>.Error(2),
            Result<int, int>.Ok(1).Zip(Result<int, int>.Error(2), static (a, b) => a + b));
    }

    [Fact]
    public void Zip_selector_result_is_ordinary_payload_without_nested_checks()
    {
        var result = Result<int, string>.Ok(1).Zip(Result<int, string>.Ok(2), static (_, _) => default(Result<int, string>));

        Assert.True(result.TryGetValue(out var inner, out _));
        Assert.False(inner.IsInitialized);
    }

    [Fact]
    public void Selector_exceptions_propagate_unchanged()
    {
        var thrown = new ProbeException("selector");

        var observed = Assert.Throws<ProbeException>(
            () => Result<int, string>.Ok(1).Zip<int, int>(Result<int, string>.Ok(2), (_, _) => throw thrown));

        Assert.Same(thrown, observed);
    }
}
