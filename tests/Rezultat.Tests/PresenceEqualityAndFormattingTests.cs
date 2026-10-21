namespace Rezultat.Tests;

public sealed class PresenceEqualityAndFormattingTests
{
    [Fact]
    public void Same_states_are_equal_and_different_states_are_not()
    {
        Assert.Equal(Presence<string>.Missing, default);
        Assert.Equal(Presence<string>.Null, Presence<string>.Null);
        Assert.Equal(Presence<string>.Value("a"), Presence<string>.Value("a"));
        Assert.NotEqual(Presence<string>.Missing, Presence<string>.Null);
        Assert.NotEqual(Presence<string>.Null, Presence<string>.Value("a"));
        Assert.NotEqual(Presence<string>.Value("a"), Presence<string>.Value("b"));
        Assert.True(Presence<int>.Value(1) == Presence<int>.Value(1));
        Assert.True(Presence<int>.Missing != Presence<int>.Null);
    }

    [Fact]
    public void Object_equality_requires_the_same_closed_type()
    {
        object boxed = Presence<int>.Value(1);

        Assert.True(boxed.Equals(Presence<int>.Value(1)));
        Assert.False(boxed.Equals(Presence<long>.Value(1)));
        Assert.False(boxed.Equals(1));
        Assert.False(Presence<int>.Missing.Equals(null));
        Assert.False(((object)Presence<int>.Missing).Equals(Maybe<int>.None));
    }

    [Fact]
    public void Missing_and_null_do_not_touch_inactive_payload_on_equality_and_hash()
    {
        ValueProbe.Reset();
        Presence<ValueProbe>[] inactive = [Presence<ValueProbe>.Missing, Presence<ValueProbe>.Null];

        foreach (var left in inactive)
        {
            foreach (var right in inactive)
            {
                _ = left.Equals(right);
                _ = left == right;
            }

            _ = left.GetHashCode();
            _ = left.Equals(Presence<ValueProbe>.Value(new ValueProbe(0)));
        }

        Assert.Equal(0, ValueProbe.Touches);
    }

    [Fact]
    public void Value_equality_and_hash_use_the_default_comparer_of_the_payload()
    {
        var left = new ReferenceProbe(1);
        var right = new ReferenceProbe(1);

        Assert.Equal(Presence<ReferenceProbe>.Value(left), Presence<ReferenceProbe>.Value(right));
        Assert.Equal(Presence<ReferenceProbe>.Value(left).GetHashCode(), Presence<ReferenceProbe>.Value(right).GetHashCode());
        Assert.True(left.EqualsCalls > 0 && left.HashCalls > 0);
        Assert.Equal(Presence<double>.Value(double.NaN), Presence<double>.Value(double.NaN));
    }

    [Fact]
    public void Arrays_keep_reference_equality()
    {
        int[] payload = [1];

        Assert.NotEqual(Presence<int[]>.Value([1]), Presence<int[]>.Value([1]));
        Assert.Equal(Presence<int[]>.Value(payload), Presence<int[]>.Value(payload));
    }

    [Fact]
    public void Comparer_exceptions_propagate()
    {
        var probe = new ReferenceProbe(1) { ThrowOnEquals = true, ThrowOnHash = true };

        Assert.Throws<ProbeException>(() => Presence<ReferenceProbe>.Value(probe).Equals(Presence<ReferenceProbe>.Value(new ReferenceProbe(1))));
        Assert.Throws<ProbeException>(() => Presence<ReferenceProbe>.Value(probe).GetHashCode());
    }

    [Fact]
    public void Hash_distinguishes_missing_and_null_and_is_usable_as_dictionary_key()
    {
        var lookup = new Dictionary<Presence<string>, string>
        {
            [Presence<string>.Missing] = "missing",
            [Presence<string>.Null] = "null",
            [Presence<string>.Value("x")] = "value",
        };

        Assert.Equal("missing", lookup[default]);
        Assert.Equal("null", lookup[Presence<string>.Null]);
        Assert.Equal("value", lookup[Presence<string>.Value("x")]);
        Assert.NotEqual(Presence<string>.Missing.GetHashCode(), Presence<string>.Null.GetHashCode());
    }

    [Fact]
    public void Text_names_the_state_and_formats_only_the_active_payload()
    {
        Assert.Equal("Presence.Missing", Presence<int>.Missing.ToString());
        Assert.Equal("Presence.Null", Presence<int>.Null.ToString());
        Assert.Equal("Presence.Value(0)", Presence<int>.Value(0).ToString());
        Assert.Equal("Presence.Value(a b)", Presence<string>.Value("a b").ToString());
        Assert.Equal("Presence.Value()", Presence<string>.Value(string.Empty).ToString());
        Assert.Equal("Presence.Value(())", Presence<Unit>.Value(Unit.Value).ToString());
        Assert.Equal("Presence.Value(Maybe.None)", Presence<Maybe<int>>.Value(Maybe<int>.None).ToString());
    }

    [Fact]
    public void Value_text_calls_ToString_once_and_normalizes_null_text()
    {
        var probe = new ReferenceProbe(1) { ReturnNullText = true };
        ValueProbe.Reset();

        Assert.Equal("Presence.Value()", Presence<ReferenceProbe>.Value(probe).ToString());
        Assert.Equal(1, probe.ToStringCalls);
        _ = Presence<ValueProbe>.Null.ToString() + Presence<ValueProbe>.Missing.ToString();
        Assert.Equal(0, ValueProbe.Touches);
    }

    [Fact]
    public void Formatter_exception_propagates()
    {
        var probe = new ReferenceProbe(1) { ThrowOnToString = true };

        Assert.Throws<ProbeException>(() => Presence<ReferenceProbe>.Value(probe).ToString());
    }
}
