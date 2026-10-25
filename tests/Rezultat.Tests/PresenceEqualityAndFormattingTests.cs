namespace Rezultat.Tests;

public sealed class PresenceEqualityAndFormattingTests {
    [Fact]
    public void Same_states_are_equal_and_different_states_are_not() {
        default(Presence<string>).ShouldBe(Presence<string>.Missing);
        Presence<string>.Null.ShouldBe(Presence<string>.Null);
        Presence<string>.Value("a").ShouldBe(Presence<string>.Value("a"));
        Presence<string>.Null.ShouldNotBe(Presence<string>.Missing);
        Presence<string>.Value("a").ShouldNotBe(Presence<string>.Null);
        Presence<string>.Value("b").ShouldNotBe(Presence<string>.Value("a"));
        (Presence<int>.Value(1) == Presence<int>.Value(1)).ShouldBeTrue();
        (Presence<int>.Missing != Presence<int>.Null).ShouldBeTrue();
    }

    [Fact]
    public void Object_equality_requires_the_same_closed_type() {
        object boxed = Presence<int>.Value(1);

        boxed.Equals(Presence<int>.Value(1)).ShouldBeTrue();
        boxed.Equals(Presence<long>.Value(1)).ShouldBeFalse();
        boxed.Equals(1).ShouldBeFalse();
        Presence<int>.Missing.Equals(null).ShouldBeFalse();
        ((object)Presence<int>.Missing).Equals(Maybe<int>.None).ShouldBeFalse();
    }

    [Fact]
    public void Missing_and_null_do_not_touch_inactive_payload_on_equality_and_hash() {
        ValueProbe.Reset();
        Presence<ValueProbe>[] inactive = [Presence<ValueProbe>.Missing, Presence<ValueProbe>.Null];

        foreach (var left in inactive) {
            foreach (var right in inactive) {
                _ = left.Equals(right);
                _ = left == right;
            }

            _ = left.GetHashCode();
            _ = left.Equals(Presence<ValueProbe>.Value(new ValueProbe(0)));
        }

        ValueProbe.Touches.ShouldBe(0);
    }

    [Fact]
    public void Value_equality_and_hash_use_the_default_comparer_of_the_payload() {
        var left = new ReferenceProbe(1);
        var right = new ReferenceProbe(1);

        Presence<ReferenceProbe>.Value(right).ShouldBe(Presence<ReferenceProbe>.Value(left));
        Presence<ReferenceProbe>.Value(right).GetHashCode().ShouldBe(Presence<ReferenceProbe>.Value(left).GetHashCode());
        (left.EqualsCalls > 0 && left.HashCalls > 0).ShouldBeTrue();
        Presence<double>.Value(double.NaN).ShouldBe(Presence<double>.Value(double.NaN));
    }

    [Fact]
    public void Arrays_keep_reference_equality() {
        int[] payload = [1];

        Presence<int[]>.Value([1]).ShouldNotBe(Presence<int[]>.Value([1]));
        Presence<int[]>.Value(payload).ShouldBe(Presence<int[]>.Value(payload));
    }

    [Fact]
    public void Comparer_exceptions_propagate() {
        var probe = new ReferenceProbe(1) { ThrowOnEquals = true, ThrowOnHash = true };

        Should.Throw<ProbeException>(() => Presence<ReferenceProbe>.Value(probe).Equals(Presence<ReferenceProbe>.Value(new ReferenceProbe(1))));
        Should.Throw<ProbeException>(() => Presence<ReferenceProbe>.Value(probe).GetHashCode());
    }

    [Fact]
    public void Hash_distinguishes_missing_and_null_and_is_usable_as_dictionary_key() {
        var lookup = new Dictionary<Presence<string>, string> {
            [Presence<string>.Missing] = "missing",
            [Presence<string>.Null] = "null",
            [Presence<string>.Value("x")] = "value",
        };

        lookup[default].ShouldBe("missing");
        lookup[Presence<string>.Null].ShouldBe("null");
        lookup[Presence<string>.Value("x")].ShouldBe("value");
        Presence<string>.Null.GetHashCode().ShouldNotBe(Presence<string>.Missing.GetHashCode());
    }

    [Fact]
    public void Text_names_the_state_and_formats_only_the_active_payload() {
        Presence<int>.Missing.ToString().ShouldBe("Presence.Missing");
        Presence<int>.Null.ToString().ShouldBe("Presence.Null");
        Presence<int>.Value(0).ToString().ShouldBe("Presence.Value(0)");
        Presence<string>.Value("a b").ToString().ShouldBe("Presence.Value(a b)");
        Presence<string>.Value(string.Empty).ToString().ShouldBe("Presence.Value()");
        Presence<Unit>.Value(Unit.Value).ToString().ShouldBe("Presence.Value(())");
        Presence<Maybe<int>>.Value(Maybe<int>.None).ToString().ShouldBe("Presence.Value(Maybe.None)");
    }

    [Fact]
    public void Value_text_calls_ToString_once_and_normalizes_null_text() {
        var probe = new ReferenceProbe(1) { ReturnNullText = true };
        ValueProbe.Reset();

        Presence<ReferenceProbe>.Value(probe).ToString().ShouldBe("Presence.Value()");
        probe.ToStringCalls.ShouldBe(1);
        _ = Presence<ValueProbe>.Null.ToString() + Presence<ValueProbe>.Missing.ToString();
        ValueProbe.Touches.ShouldBe(0);
    }

    [Fact]
    public void Formatter_exception_propagates() {
        var probe = new ReferenceProbe(1) { ThrowOnToString = true };

        Should.Throw<ProbeException>(() => Presence<ReferenceProbe>.Value(probe).ToString());
    }
}
