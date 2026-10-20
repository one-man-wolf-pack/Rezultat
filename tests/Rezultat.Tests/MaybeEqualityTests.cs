using System.Collections.Immutable;

namespace Rezultat.Tests;

public sealed class MaybeEqualityTests
{
    [Fact]
    public void None_default_and_new_are_equal_with_equal_hash()
    {
        var none = Maybe<string>.None;
        var created = new Maybe<string>();

        Assert.True(none.Equals(default(Maybe<string>)));
        Assert.True(none.Equals((object)created));
        Assert.True(none == created);
        Assert.False(none != created);
        Assert.Equal(none.GetHashCode(), created.GetHashCode());
    }

    [Fact]
    public void None_differs_from_some_with_null_or_default_payload()
    {
        Assert.NotEqual(Maybe<string?>.None, Maybe<string?>.Some(null));
        Assert.NotEqual(Maybe<int>.None, Maybe<int>.Some(0));
        Assert.NotEqual(Maybe<int?>.None, Maybe<int?>.Some(null));
        Assert.NotEqual(Maybe<Unit>.None, Maybe<Unit>.Some(Unit.Value));
        Assert.True(Maybe<Maybe<int>>.None != Maybe<Maybe<int>>.Some(Maybe<int>.None));
    }

    [Fact]
    public void Some_compares_active_payload_with_default_comparer()
    {
        Assert.Equal(Maybe<string>.Some("a"), Maybe<string>.Some(new string('a', 1)));
        Assert.NotEqual(Maybe<string>.Some("a"), Maybe<string>.Some("A"));
        Assert.Equal(Maybe<string?>.Some(null), Maybe<string?>.Some(null));
        Assert.Equal(Maybe<int?>.Some(null), Maybe<int?>.Some(null));
        Assert.Equal(Maybe<Maybe<int>>.Some(Maybe<int>.Some(1)), Maybe<Maybe<int>>.Some(Maybe<int>.Some(1)));
        Assert.Equal(Maybe<Unit>.Some(default), Maybe<Unit>.Some(Unit.Value));
    }

    [Fact]
    public void Double_payload_follows_default_comparer()
    {
        Assert.True(Maybe<double>.Some(double.NaN) == Maybe<double>.Some(double.NaN));
        Assert.True(Maybe<double>.Some(0.0) == Maybe<double>.Some(-0.0));
        Assert.Equal(Maybe<double>.Some(0.0).GetHashCode(), Maybe<double>.Some(-0.0).GetHashCode());
    }

    [Fact]
    public void Collections_keep_their_own_equality()
    {
        int[] array = [1, 2];
        List<int> list = [1, 2];
        var immutable = ImmutableArray.Create(1, 2);

        Assert.Equal(Maybe<int[]>.Some(array), Maybe<int[]>.Some(array));
        Assert.NotEqual(Maybe<int[]>.Some(array), Maybe<int[]>.Some([1, 2]));
        Assert.NotEqual(Maybe<List<int>>.Some(list), Maybe<List<int>>.Some([1, 2]));
        Assert.Equal(Maybe<ImmutableArray<int>>.Some(immutable), Maybe<ImmutableArray<int>>.Some(immutable));
        Assert.NotEqual(Maybe<ImmutableArray<int>>.Some(immutable), Maybe<ImmutableArray<int>>.Some(ImmutableArray.Create(1, 2)));
    }

    [Fact]
    public void Object_equality_recognizes_only_the_same_closed_type()
    {
        var some = Maybe<int>.Some(1);
        var none = Maybe<string>.None;

        Assert.False(some.Equals(null));
        Assert.False(some.Equals((object)1));
        Assert.False(some.Equals(Maybe<long>.Some(1)));
        Assert.False(none.Equals(null));
        Assert.True(none.Equals((object)Maybe<string?>.None));
    }

    [Fact]
    public void Lifted_nullable_maybe_distinguishes_wrapped_none_from_null()
    {
        Maybe<int>? wrappedNone = Maybe<int>.None;
        Maybe<int>? missing = null;

        Assert.False(wrappedNone == missing);
        Assert.True(wrappedNone == Maybe<int>.None);
    }

    [Fact]
    public void None_does_not_invoke_payload_equality()
    {
        var probe = new ReferenceProbe(1);

        Assert.False(Maybe<ReferenceProbe>.Some(probe) == Maybe<ReferenceProbe>.None);
        Assert.False(Maybe<ReferenceProbe>.None.Equals(Maybe<ReferenceProbe>.Some(probe)));
        Assert.Equal(0, probe.EqualsCalls);
    }

    [Fact]
    public void None_does_not_touch_value_type_slot()
    {
        ValueProbe.Reset();

        Assert.True(Maybe<ValueProbe>.None == default);
        Assert.False(Maybe<ValueProbe>.None == Maybe<ValueProbe>.Some(default));
        _ = Maybe<ValueProbe>.None.GetHashCode();

        Assert.Equal(0, ValueProbe.Touches);
    }

    [Fact]
    public void Some_compares_and_hashes_active_payload()
    {
        ValueProbe.Reset();

        Assert.True(Maybe<ValueProbe>.Some(new ValueProbe(2)) == Maybe<ValueProbe>.Some(new ValueProbe(2)));
        var touchesAfterEquality = ValueProbe.Touches;
        _ = Maybe<ValueProbe>.Some(new ValueProbe(2)).GetHashCode();

        Assert.NotEqual(0, touchesAfterEquality);
        Assert.True(ValueProbe.Touches > touchesAfterEquality);
    }

    [Fact]
    public void Null_payload_hash_is_safe_and_stable()
    {
        Assert.Equal(Maybe<string?>.Some(null).GetHashCode(), Maybe<string?>.Some(null).GetHashCode());
        Assert.Equal(Maybe<int?>.Some(null).GetHashCode(), Maybe<int?>.Some(null).GetHashCode());
    }

    [Fact]
    public void Payload_equality_and_hash_exceptions_propagate()
    {
        var throwing = new ReferenceProbe(1) { ThrowOnEquals = true, ThrowOnHash = true };

        Assert.Throws<ProbeException>(() => Maybe<ReferenceProbe>.Some(throwing) == Maybe<ReferenceProbe>.Some(new ReferenceProbe(1)));
        Assert.Throws<ProbeException>(() => Maybe<ReferenceProbe>.Some(throwing).GetHashCode());
    }

    [Fact]
    public void Hash_set_uses_standard_equality()
    {
        var set = new HashSet<Maybe<string?>>
        {
            Maybe<string?>.None,
            default,
            Maybe<string?>.Some(null),
            Maybe<string?>.Some("a"),
            Maybe<string?>.Some("a"),
        };

        Assert.Equal(3, set.Count);
        Assert.Contains(Maybe<string?>.Some(null), set);
    }
}
