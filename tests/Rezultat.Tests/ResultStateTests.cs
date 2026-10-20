namespace Rezultat.Tests;

public sealed class ResultStateTests
{
    [Fact]
    public void Default_and_parameterless_new_are_uninitialized()
    {
        Assert.False(default(Result<int, string>).IsInitialized);
        Assert.False(new Result<int, string>().IsInitialized);
        Assert.False(default(Result<Unit, Unit>).IsInitialized);
    }

    [Fact]
    public void Created_branches_are_initialized_even_with_null_or_default_payload()
    {
        Assert.True(Result<string?, string?>.Ok(null).IsInitialized);
        Assert.True(Result<string?, string?>.Error(null).IsInitialized);
        Assert.True(Result<int, bool>.Ok(0).IsInitialized);
        Assert.True(Result<int, bool>.Error(false).IsInitialized);
        Assert.True(Result<int?, int?>.Ok(null).IsInitialized);
        Assert.True(Result<Unit, Unit>.Ok(default).IsInitialized);
    }

    [Fact]
    public void IsSuccess_reports_explicitly_selected_branch_for_same_type_payload()
    {
        Assert.True(Result<int, int>.Ok(7).IsSuccess);
        Assert.False(Result<int, int>.Error(7).IsSuccess);
        Assert.True(Result<Unit, Unit>.Ok(Unit.Value).IsSuccess);
        Assert.False(Result<Unit, Unit>.Error(Unit.Value).IsSuccess);
        Assert.True(Result<string?, string?>.Ok(null).IsSuccess);
        Assert.False(Result<string?, string?>.Error(null).IsSuccess);
    }

    [Fact]
    public void Factories_accept_upcast_payload_and_keep_its_identity()
    {
        var error = new ArgumentException("bad");
        Result<object, Exception> result = Result<object, Exception>.Error(error);

        Assert.False(result.TryGetValue(out _, out var observed));
        Assert.Same(error, observed);
    }

    [Fact]
    public void Empty_with_copies_without_selecting_a_branch()
    {
        var uninitialized = default(Result<int, string>) with { };
        var success = Result<int, string>.Ok(3) with { };

        Assert.False(uninitialized.IsInitialized);
        Assert.Equal(Result<int, string>.Ok(3), success);
    }

    [Fact]
    public void IsSuccess_on_uninitialized_throws()
    {
        Assert.Throws<InvalidOperationException>(() => default(Result<int, string>).IsSuccess);
        Assert.Throws<InvalidOperationException>(() => default(Result<Unit, Unit>).IsSuccess);
    }

    [Fact]
    public void TryGetValue_on_uninitialized_throws()
    {
        Assert.Throws<InvalidOperationException>(() => default(Result<string, string>).TryGetValue(out _, out _));
    }

    [Fact]
    public void TryGetValue_success_returns_active_value_and_default_error()
    {
        var payload = new ReferenceProbe(1);

        Assert.True(Result<ReferenceProbe, string>.Ok(payload).TryGetValue(out var value, out var error));
        Assert.Same(payload, value);
        Assert.Null(error);
    }

    [Fact]
    public void TryGetValue_failure_returns_active_error_and_default_value()
    {
        Assert.False(Result<int, string>.Error("e").TryGetValue(out var value, out var error));
        Assert.Equal(0, value);
        Assert.Equal("e", error);
    }

    [Fact]
    public void TryGetValue_bool_selects_branch_independently_of_null_or_default_outputs()
    {
        Assert.True(Result<string?, string?>.Ok(null).TryGetValue(out var okValue, out var okError));
        Assert.Null(okValue);
        Assert.Null(okError);

        Assert.False(Result<Unit, Unit>.Error(Unit.Value).TryGetValue(out var unitValue, out var unitError));
        Assert.Equal(Unit.Value, unitValue);
        Assert.Equal(Unit.Value, unitError);

        Assert.True(Result<int?, int?>.Ok(null).TryGetValue(out var nullableValue, out _));
        Assert.False(nullableValue.HasValue);
    }
}
