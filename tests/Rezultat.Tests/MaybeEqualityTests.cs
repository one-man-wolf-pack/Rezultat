using System.Collections.Immutable;

namespace Rezultat.Tests;

public sealed class MaybeEqualityTests {
    [Fact]
    public void None_default_and_new_are_equal_with_equal_hash() {
        var none = Maybe<string>.None;
        var created = new Maybe<string>();

        none.Equals(default(Maybe<string>)).ShouldBeTrue();
        none.Equals((object)created).ShouldBeTrue();
        (none == created).ShouldBeTrue();
        (none != created).ShouldBeFalse();
        created.GetHashCode().ShouldBe(none.GetHashCode());
    }

    [Fact]
    public void None_differs_from_some_with_null_or_default_payload() {
        Maybe<string?>.Some(null).ShouldNotBe(Maybe<string?>.None);
        Maybe<int>.Some(0).ShouldNotBe(Maybe<int>.None);
        Maybe<int?>.Some(null).ShouldNotBe(Maybe<int?>.None);
        Maybe<Unit>.Some(Unit.Value).ShouldNotBe(Maybe<Unit>.None);
        (Maybe<Maybe<int>>.None != Maybe<Maybe<int>>.Some(Maybe<int>.None)).ShouldBeTrue();
    }

    [Fact]
    public void Some_compares_active_payload_with_default_comparer() {
        Maybe<string>.Some(new string('a', 1)).ShouldBe(Maybe<string>.Some("a"));
        Maybe<string>.Some("A").ShouldNotBe(Maybe<string>.Some("a"));
        Maybe<string?>.Some(null).ShouldBe(Maybe<string?>.Some(null));
        Maybe<int?>.Some(null).ShouldBe(Maybe<int?>.Some(null));
        Maybe<Maybe<int>>.Some(Maybe<int>.Some(1)).ShouldBe(Maybe<Maybe<int>>.Some(Maybe<int>.Some(1)));
        Maybe<Unit>.Some(Unit.Value).ShouldBe(Maybe<Unit>.Some(default));
    }

    [Fact]
    public void Double_payload_follows_default_comparer() {
        (Maybe<double>.Some(double.NaN) == Maybe<double>.Some(double.NaN)).ShouldBeTrue();
        (Maybe<double>.Some(0.0) == Maybe<double>.Some(-0.0)).ShouldBeTrue();
        Maybe<double>.Some(-0.0).GetHashCode().ShouldBe(Maybe<double>.Some(0.0).GetHashCode());
    }

    [Fact]
    public void Collections_keep_their_own_equality() {
        int[] array = [1, 2];
        List<int> list = [1, 2];
        var immutable = ImmutableArray.Create(1, 2);

        Maybe<int[]>.Some(array).ShouldBe(Maybe<int[]>.Some(array));
        Maybe<int[]>.Some([1, 2]).ShouldNotBe(Maybe<int[]>.Some(array));
        Maybe<List<int>>.Some([1, 2]).ShouldNotBe(Maybe<List<int>>.Some(list));
        Maybe<ImmutableArray<int>>.Some(immutable).ShouldBe(Maybe<ImmutableArray<int>>.Some(immutable));
        Maybe<ImmutableArray<int>>.Some(ImmutableArray.Create(1, 2)).ShouldNotBe(Maybe<ImmutableArray<int>>.Some(immutable));
    }

    [Fact]
    public void Object_equality_recognizes_only_the_same_closed_type() {
        var some = Maybe<int>.Some(1);
        var none = Maybe<string>.None;

        some.Equals(null).ShouldBeFalse();
        some.Equals((object)1).ShouldBeFalse();
        some.Equals(Maybe<long>.Some(1)).ShouldBeFalse();
        none.Equals(null).ShouldBeFalse();
        none.Equals((object)Maybe<string?>.None).ShouldBeTrue();
    }

    [Fact]
    public void Lifted_nullable_maybe_distinguishes_wrapped_none_from_null() {
        Maybe<int>? wrappedNone = Maybe<int>.None;
        Maybe<int>? missing = null;

        (wrappedNone == missing).ShouldBeFalse();
        (wrappedNone == Maybe<int>.None).ShouldBeTrue();
    }

    [Fact]
    public void None_does_not_invoke_payload_equality() {
        var probe = new ReferenceProbe(1);

        (Maybe<ReferenceProbe>.Some(probe) == Maybe<ReferenceProbe>.None).ShouldBeFalse();
        Maybe<ReferenceProbe>.None.Equals(Maybe<ReferenceProbe>.Some(probe)).ShouldBeFalse();
        probe.EqualsCalls.ShouldBe(0);
    }

    [Fact]
    public void None_does_not_touch_value_type_slot() {
        ValueProbe.Reset();

        (Maybe<ValueProbe>.None == default).ShouldBeTrue();
        (Maybe<ValueProbe>.None == Maybe<ValueProbe>.Some(default)).ShouldBeFalse();
        _ = Maybe<ValueProbe>.None.GetHashCode();

        ValueProbe.Touches.ShouldBe(0);
    }

    [Fact]
    public void Some_compares_and_hashes_active_payload() {
        ValueProbe.Reset();

        (Maybe<ValueProbe>.Some(new ValueProbe(2)) == Maybe<ValueProbe>.Some(new ValueProbe(2))).ShouldBeTrue();
        var touchesAfterEquality = ValueProbe.Touches;
        _ = Maybe<ValueProbe>.Some(new ValueProbe(2)).GetHashCode();

        touchesAfterEquality.ShouldNotBe(0);
        (ValueProbe.Touches > touchesAfterEquality).ShouldBeTrue();
    }

    [Fact]
    public void Null_payload_hash_is_safe_and_stable() {
        Maybe<string?>.Some(null).GetHashCode().ShouldBe(Maybe<string?>.Some(null).GetHashCode());
        Maybe<int?>.Some(null).GetHashCode().ShouldBe(Maybe<int?>.Some(null).GetHashCode());
    }

    [Fact]
    public void Payload_equality_and_hash_exceptions_propagate() {
        var throwing = new ReferenceProbe(1) { ThrowOnEquals = true, ThrowOnHash = true };

        Should.Throw<ProbeException>(() => Maybe<ReferenceProbe>.Some(throwing) == Maybe<ReferenceProbe>.Some(new ReferenceProbe(1)));
        Should.Throw<ProbeException>(() => Maybe<ReferenceProbe>.Some(throwing).GetHashCode());
    }

    [Fact]
    public void Hash_set_uses_standard_equality() {
        var set = new HashSet<Maybe<string?>>
        {
            Maybe<string?>.None,
            default,
            Maybe<string?>.Some(null),
            Maybe<string?>.Some("a"),
            Maybe<string?>.Some("a"),
        };

        set.Count.ShouldBe(3);
        // Set membership, not enumeration: the hash lookup is under test.
        set.Contains(Maybe<string?>.Some(null)).ShouldBeTrue();
    }
}
