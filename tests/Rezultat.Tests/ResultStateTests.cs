namespace Rezultat.Tests;

public sealed class ResultStateTests {
    [Fact]
    public void Default_and_parameterless_new_are_uninitialized() {
        default(Result<int, string>).IsInitialized.ShouldBeFalse();
        new Result<int, string>().IsInitialized.ShouldBeFalse();
        default(Result<Unit, Unit>).IsInitialized.ShouldBeFalse();
    }

    [Fact]
    public void Created_branches_are_initialized_even_with_null_or_default_payload() {
        Result<string?, string?>.Ok(null).IsInitialized.ShouldBeTrue();
        Result<string?, string?>.Error(null).IsInitialized.ShouldBeTrue();
        Result<int, bool>.Ok(0).IsInitialized.ShouldBeTrue();
        Result<int, bool>.Error(false).IsInitialized.ShouldBeTrue();
        Result<int?, int?>.Ok(null).IsInitialized.ShouldBeTrue();
        Result<Unit, Unit>.Ok(default).IsInitialized.ShouldBeTrue();
    }

    [Fact]
    public void IsSuccess_reports_explicitly_selected_branch_for_same_type_payload() {
        Result<int, int>.Ok(7).IsSuccess.ShouldBeTrue();
        Result<int, int>.Error(7).IsSuccess.ShouldBeFalse();
        Result<Unit, Unit>.Ok(Unit.Value).IsSuccess.ShouldBeTrue();
        Result<Unit, Unit>.Error(Unit.Value).IsSuccess.ShouldBeFalse();
        Result<string?, string?>.Ok(null).IsSuccess.ShouldBeTrue();
        Result<string?, string?>.Error(null).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Factories_accept_upcast_payload_and_keep_its_identity() {
        var error = new ArgumentException("bad");
        Result<object, Exception> result = Result<object, Exception>.Error(error);

        result.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
    }

    [Fact]
    public void Empty_with_copies_without_selecting_a_branch() {
        var uninitialized = default(Result<int, string>) with { };
        var success = Result<int, string>.Ok(3) with { };

        uninitialized.IsInitialized.ShouldBeFalse();
        success.ShouldBe(Result<int, string>.Ok(3));
    }

    [Fact]
    public void IsSuccess_on_uninitialized_throws() {
        Should.Throw<InvalidOperationException>(() => default(Result<int, string>).IsSuccess).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => default(Result<Unit, Unit>).IsSuccess).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void TryGetValue_on_uninitialized_throws() {
        Should.Throw<InvalidOperationException>(() => default(Result<string, string>).TryGetValue(out _, out _)).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void TryGetValue_success_returns_active_value_and_default_error() {
        var payload = new ReferenceProbe(1);

        Result<ReferenceProbe, string>.Ok(payload).TryGetValue(out var value, out var error).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        error.ShouldBeNull();
    }

    [Fact]
    public void TryGetValue_failure_returns_active_error_and_default_value() {
        Result<int, string>.Error("e").TryGetValue(out var value, out var error).ShouldBeFalse();
        value.ShouldBe(0);
        error.ShouldBe("e");
    }

    [Fact]
    public void TryGetValue_bool_selects_branch_independently_of_null_or_default_outputs() {
        Result<string?, string?>.Ok(null).TryGetValue(out var okValue, out var okError).ShouldBeTrue();
        okValue.ShouldBeNull();
        okError.ShouldBeNull();

        Result<Unit, Unit>.Error(Unit.Value).TryGetValue(out var unitValue, out var unitError).ShouldBeFalse();
        unitValue.ShouldBe(Unit.Value);
        unitError.ShouldBe(Unit.Value);

        Result<int?, int?>.Ok(null).TryGetValue(out var nullableValue, out _).ShouldBeTrue();
        nullableValue.HasValue.ShouldBeFalse();
    }
}
