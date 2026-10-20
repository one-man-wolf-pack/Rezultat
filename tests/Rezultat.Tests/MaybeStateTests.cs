namespace Rezultat.Tests;

public sealed class MaybeStateTests
{
    [Fact]
    public void None_default_and_new_are_the_same_absence()
    {
        Maybe<string>[] absent = [Maybe<string>.None, default, new()];

        Assert.All(absent, maybe =>
        {
            Assert.False(maybe.IsSome);
            Assert.False(maybe.TryGetValue(out var value));
            Assert.Null(value);
            Assert.Equal(Maybe<string>.None, maybe);
        });
    }

    [Fact]
    public void Unfilled_array_elements_are_none()
    {
        var slots = new Maybe<int>[3];

        Assert.All(slots, slot => Assert.False(slot.IsSome));
    }

    [Fact]
    public void Inactive_value_type_output_is_default()
    {
        Assert.False(Maybe<int>.None.TryGetValue(out var number));
        Assert.Equal(0, number);
        Assert.False(default(Maybe<Unit>).TryGetValue(out var unit));
        Assert.Equal(Unit.Value, unit);
    }

    [Fact]
    public void Some_with_default_payload_is_present()
    {
        Assert.True(Maybe<int>.Some(0).TryGetValue(out var zero));
        Assert.Equal(0, zero);
        Assert.True(Maybe<bool>.Some(false).IsSome);
        Assert.True(Maybe<string>.Some(string.Empty).IsSome);
        Assert.True(Maybe<Unit>.Some(Unit.Value).IsSome);
        Assert.NotEqual(Maybe<int>.None, Maybe<int>.Some(default));
    }

    [Fact]
    public void Some_null_reference_is_present_null()
    {
        var maybe = Maybe<string?>.Some(null);

        Assert.True(maybe.IsSome);
        Assert.True(maybe.TryGetValue(out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Some_empty_nullable_is_present_without_value()
    {
        var maybe = Maybe<int?>.Some(null);

        Assert.True(maybe.TryGetValue(out var value));
        Assert.False(value.HasValue);
        Assert.True(Maybe<int?>.Some(0).TryGetValue(out var zero) && zero == 0);
    }

    [Fact]
    public void Some_accepts_upcast_and_keeps_the_exact_reference()
    {
        var payload = new List<int>();
        var maybe = Maybe<IEnumerable<int>>.Some(payload);

        Assert.True(maybe.TryGetValue(out var value));
        Assert.Same(payload, value);
    }

    [Fact]
    public void Some_does_not_copy_mutable_payload()
    {
        var payload = new List<int> { 1 };
        var maybe = Maybe<List<int>>.Some(payload);

        payload.Add(2);

        Assert.True(maybe.TryGetValue(out var value));
        Assert.Equal([1, 2], value);
    }

    [Fact]
    public void Nested_none_is_present_payload_of_outer_some()
    {
        var outer = Maybe<Maybe<int>>.Some(Maybe<int>.None);

        Assert.True(outer.IsSome);
        Assert.True(outer.TryGetValue(out var inner));
        Assert.False(inner.IsSome);
        Assert.NotEqual(Maybe<Maybe<int>>.None, outer);
        Assert.NotEqual(Maybe<Maybe<int>>.Some(Maybe<int>.Some(1)), outer);
    }

    [Fact]
    public void Uninitialized_result_payload_is_kept_without_validation()
    {
        var maybe = Maybe<Result<int, string>>.Some(default);

        Assert.True(maybe.TryGetValue(out var result));
        Assert.False(result.IsInitialized);
        Assert.Equal("Maybe.Some(Result.Uninitialized)", maybe.ToString());
    }
}
