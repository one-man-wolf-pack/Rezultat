namespace Rezultat.Tests;

public sealed class MaybeFromNullableTests
{
    [Fact]
    public void Null_reference_becomes_none()
    {
        Maybe<string> maybe = Maybe.FromNullable((string?)null);

        Assert.False(maybe.IsSome);
        Assert.Equal(Maybe<string>.None, maybe);
    }

    [Fact]
    public void Non_null_reference_becomes_some_of_the_same_object()
    {
        var payload = new ReferenceProbe(1);

        Assert.True(Maybe.FromNullable(payload).TryGetValue(out var value));
        Assert.Same(payload, value);
        Assert.Equal(Maybe<string>.Some(string.Empty), Maybe.FromNullable(string.Empty));
    }

    [Fact]
    public void Empty_nullable_value_becomes_none()
    {
        Maybe<int> maybe = Maybe.FromNullable((int?)null);

        Assert.False(maybe.IsSome);
    }

    [Fact]
    public void Nullable_value_becomes_some_of_the_underlying_value_including_defaults()
    {
        Assert.Equal(Maybe<int>.Some(0), Maybe.FromNullable((int?)0));
        Assert.Equal(Maybe<bool>.Some(false), Maybe.FromNullable((bool?)false));
        Assert.Equal(Maybe<Unit>.Some(default), Maybe.FromNullable((Unit?)default(Unit)));
        Assert.Equal(Maybe<Guid>.Some(Guid.Empty), Maybe.FromNullable<Guid>(default(Guid)));
    }

    [Fact]
    public void Adaptation_differs_from_some_with_nullable_payload()
    {
        Assert.False(Maybe.FromNullable<string>(null).IsSome);
        Assert.True(Maybe<string?>.Some(null).IsSome);
        Assert.False(Maybe.FromNullable<int>(null).IsSome);
        Assert.True(Maybe<int?>.Some(null).IsSome);
    }

    [Fact]
    public void Present_nested_maybe_is_not_flattened()
    {
        var nested = Maybe.FromNullable((Maybe<int>?)Maybe<int>.None);

        Assert.True(nested.TryGetValue(out var inner));
        Assert.False(inner.IsSome);
    }
}
