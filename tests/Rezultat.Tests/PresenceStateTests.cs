namespace Rezultat.Tests;

public sealed class PresenceStateTests
{
    [Fact]
    public void Missing_default_and_new_are_the_same_state()
    {
        Presence<string>[] missing = [Presence<string>.Missing, default, new()];

        Assert.All(missing, presence =>
        {
            Assert.Equal((true, false, false), (presence.IsMissing, presence.IsNull, presence.IsValue));
            Assert.False(presence.TryGetValue(out var value));
            Assert.Null(value);
            Assert.Equal(Presence<string>.Missing, presence);
        });
    }

    [Fact]
    public void Unfilled_array_elements_are_missing()
    {
        var slots = new Presence<int>[3];

        Assert.All(slots, slot => Assert.True(slot.IsMissing));
    }

    [Fact]
    public void Null_is_a_separate_state_with_one_active_flag()
    {
        var presence = Presence<string>.Null;

        Assert.Equal((false, true, false), (presence.IsMissing, presence.IsNull, presence.IsValue));
        Assert.False(presence.TryGetValue(out var value));
        Assert.Null(value);
        Assert.NotEqual(Presence<string>.Missing, presence);
    }

    [Fact]
    public void Inactive_value_type_output_is_default_and_does_not_tell_missing_from_null()
    {
        Assert.False(Presence<int>.Missing.TryGetValue(out var missing));
        Assert.False(Presence<int>.Null.TryGetValue(out var explicitNull));
        Assert.Equal((0, 0), (missing, explicitNull));
        Assert.False(Presence<Unit>.Null.TryGetValue(out var unit));
        Assert.Equal(Unit.Value, unit);
    }

    [Fact]
    public void Value_keeps_default_like_payloads_as_value()
    {
        Assert.True(Presence<int>.Value(0).TryGetValue(out var zero));
        Assert.Equal(0, zero);
        Assert.True(Presence<bool>.Value(false).IsValue);
        Assert.True(Presence<string>.Value(string.Empty).TryGetValue(out var empty));
        Assert.Equal(string.Empty, empty);
        Assert.True(Presence<Unit>.Value(Unit.Value).IsValue);
        Assert.NotEqual(Presence<int>.Missing, Presence<int>.Value(default));
        Assert.Equal((false, false, true), (Presence<int>.Value(1).IsMissing, Presence<int>.Value(1).IsNull, Presence<int>.Value(1).IsValue));
    }

    [Fact]
    public void Value_null_reference_is_rejected_with_value_param_name()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => Presence<string>.Value(null!));

        Assert.Equal("value", exception.ParamName);
    }

#pragma warning disable CS8714 // Bypassed notnull constraint: the runtime guard must hold without the warning.
    [Fact]
    public void Value_empty_nullable_is_rejected_when_the_constraint_warning_is_bypassed()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => Presence<int?>.Value(null));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Value_non_empty_nullable_keeps_the_declared_nullable_type()
    {
        var presence = Presence<int?>.Value(5);

        Assert.True(presence.TryGetValue(out var value));
        Assert.True(value.HasValue);
        Assert.Equal(5, value.Value);
        Assert.Equal("Presence.Value(5)", presence.ToString());
    }
#pragma warning restore CS8714

    [Fact]
    public void Value_guard_does_not_invoke_payload_code()
    {
        var probe = new ReferenceProbe(1);

        _ = Presence<ReferenceProbe>.Value(probe);

        Assert.Equal((0, 0, 0), (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls));
    }

    [Fact]
    public void Value_keeps_the_exact_reference_without_copying()
    {
        var payload = new List<int> { 1 };
        var presence = Presence<IEnumerable<int>>.Value(payload);

        payload.Add(2);

        Assert.True(presence.TryGetValue(out var value));
        Assert.Same(payload, value);
        Assert.Equal([1, 2], value);
    }

    [Fact]
    public void Nested_carriers_are_not_flattened()
    {
        var missing = Presence<Presence<int>>.Value(Presence<int>.Missing);
        var explicitNull = Presence<Presence<int>>.Value(Presence<int>.Null);
        var none = Presence<Maybe<int>>.Value(Maybe<int>.None);

        Assert.True(missing.IsValue && missing.TryGetValue(out var inner) && inner.IsMissing);
        Assert.True(explicitNull.IsValue && explicitNull.TryGetValue(out var innerNull) && innerNull.IsNull);
        Assert.NotEqual(missing, explicitNull);
        Assert.True(none.TryGetValue(out var maybe) && !maybe.IsSome);
        Assert.Equal("Presence.Value(Presence.Missing)", missing.ToString());
    }

    [Fact]
    public void Flags_do_not_touch_value_payload()
    {
        ValueProbe.Reset();
        var presence = Presence<ValueProbe>.Value(new ValueProbe(1));

        _ = (presence.IsMissing, presence.IsNull, presence.IsValue, Presence<ValueProbe>.Null.IsNull);

        Assert.Equal(0, ValueProbe.Touches);
    }
}
