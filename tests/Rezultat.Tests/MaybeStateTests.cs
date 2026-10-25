namespace Rezultat.Tests;

public sealed class MaybeStateTests {
    [Fact]
    public void None_default_and_new_are_the_same_absence() {
        Maybe<string>[] absent = [Maybe<string>.None, default, new()];

        foreach (var maybe in absent) {
            maybe.IsSome.ShouldBeFalse();
            maybe.TryGetValue(out var value).ShouldBeFalse();
            value.ShouldBeNull();
            maybe.ShouldBe(Maybe<string>.None);
        }
    }

    [Fact]
    public void Unfilled_array_elements_are_none() {
        var slots = new Maybe<int>[3];

        foreach (var slot in slots) {
            slot.IsSome.ShouldBeFalse();
        }
    }

    [Fact]
    public void Inactive_value_type_output_is_default() {
        Maybe<int>.None.TryGetValue(out var number).ShouldBeFalse();
        number.ShouldBe(0);
        default(Maybe<Unit>).TryGetValue(out var unit).ShouldBeFalse();
        unit.ShouldBe(Unit.Value);
    }

    [Fact]
    public void Some_with_default_payload_is_present() {
        Maybe<int>.Some(0).TryGetValue(out var zero).ShouldBeTrue();
        zero.ShouldBe(0);
        Maybe<bool>.Some(false).IsSome.ShouldBeTrue();
        Maybe<string>.Some(string.Empty).IsSome.ShouldBeTrue();
        Maybe<Unit>.Some(Unit.Value).IsSome.ShouldBeTrue();
        Maybe<int>.Some(default).ShouldNotBe(Maybe<int>.None);
    }

    [Fact]
    public void Some_null_reference_is_present_null() {
        var maybe = Maybe<string?>.Some(null);

        maybe.IsSome.ShouldBeTrue();
        maybe.TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBeNull();
    }

    [Fact]
    public void Some_empty_nullable_is_present_without_value() {
        var maybe = Maybe<int?>.Some(null);

        maybe.TryGetValue(out var value).ShouldBeTrue();
        value.HasValue.ShouldBeFalse();
        (Maybe<int?>.Some(0).TryGetValue(out var zero) && zero == 0).ShouldBeTrue();
    }

    [Fact]
    public void Some_accepts_upcast_and_keeps_the_exact_reference() {
        var payload = new List<int>();
        var maybe = Maybe<IEnumerable<int>>.Some(payload);

        maybe.TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
    }

    [Fact]
    public void Some_does_not_copy_mutable_payload() {
        var payload = new List<int> { 1 };
        var maybe = Maybe<List<int>>.Some(payload);

        payload.Add(2);

        maybe.TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBe([1, 2]);
    }

    [Fact]
    public void Nested_none_is_present_payload_of_outer_some() {
        var outer = Maybe<Maybe<int>>.Some(Maybe<int>.None);

        outer.IsSome.ShouldBeTrue();
        outer.TryGetValue(out var inner).ShouldBeTrue();
        inner.IsSome.ShouldBeFalse();
        outer.ShouldNotBe(Maybe<Maybe<int>>.None);
        outer.ShouldNotBe(Maybe<Maybe<int>>.Some(Maybe<int>.Some(1)));
    }

    [Fact]
    public void Uninitialized_result_payload_is_kept_without_validation() {
        var maybe = Maybe<Result<int, string>>.Some(default);

        maybe.TryGetValue(out var result).ShouldBeTrue();
        result.IsInitialized.ShouldBeFalse();
        maybe.ToString().ShouldBe("Maybe.Some(Result.Uninitialized)");
    }
}
