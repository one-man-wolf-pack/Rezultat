namespace Rezultat.Tests;

public sealed class MaybeFromNullableTests {
    [Fact]
    public void Null_reference_becomes_none() {
        Maybe<string> maybe = Maybe.FromNullable((string?)null);

        maybe.IsSome.ShouldBeFalse();
        maybe.ShouldBe(Maybe<string>.None);
    }

    [Fact]
    public void Non_null_reference_becomes_some_of_the_same_object() {
        var payload = new ReferenceProbe(1);

        Maybe.FromNullable(payload).TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        Maybe.FromNullable(string.Empty).ShouldBe(Maybe<string>.Some(string.Empty));
    }

    [Fact]
    public void Empty_nullable_value_becomes_none() {
        Maybe<int> maybe = Maybe.FromNullable((int?)null);

        maybe.IsSome.ShouldBeFalse();
    }

    [Fact]
    public void Nullable_value_becomes_some_of_the_underlying_value_including_defaults() {
        Maybe.FromNullable((int?)0).ShouldBe(Maybe<int>.Some(0));
        Maybe.FromNullable((bool?)false).ShouldBe(Maybe<bool>.Some(false));
        Maybe.FromNullable((Unit?)default(Unit)).ShouldBe(Maybe<Unit>.Some(default));
        Maybe.FromNullable<Guid>(default(Guid)).ShouldBe(Maybe<Guid>.Some(Guid.Empty));
    }

    [Fact]
    public void Adaptation_differs_from_some_with_nullable_payload() {
        Maybe.FromNullable<string>(null).IsSome.ShouldBeFalse();
        Maybe<string?>.Some(null).IsSome.ShouldBeTrue();
        Maybe.FromNullable<int>(null).IsSome.ShouldBeFalse();
        Maybe<int?>.Some(null).IsSome.ShouldBeTrue();
    }

    [Fact]
    public void Present_nested_maybe_is_not_flattened() {
        var nested = Maybe.FromNullable((Maybe<int>?)Maybe<int>.None);

        nested.TryGetValue(out var inner).ShouldBeTrue();
        inner.IsSome.ShouldBeFalse();
    }
}
