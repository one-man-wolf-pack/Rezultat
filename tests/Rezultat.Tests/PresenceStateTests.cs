namespace Rezultat.Tests;

public sealed class PresenceStateTests {
    [Fact]
    public void Missing_default_and_new_are_the_same_state() {
        Presence<string>[] missing = [Presence<string>.Missing, default, new()];

        foreach (var presence in missing) {
            (presence.IsMissing, presence.IsNull, presence.IsValue).ShouldBe((true, false, false));
            presence.TryGetValue(out string? value).ShouldBeFalse();
            value.ShouldBeNull();
            presence.ShouldBe(Presence<string>.Missing);
        }
    }

    [Fact]
    public void Unfilled_array_elements_are_missing() {
        var slots = new Presence<int>[3];

        foreach (var slot in slots) {
            slot.IsMissing.ShouldBeTrue();
        }
    }

    [Fact]
    public void Null_is_a_separate_state_with_one_active_flag() {
        var presence = Presence<string>.Null;

        (presence.IsMissing, presence.IsNull, presence.IsValue).ShouldBe((false, true, false));
        presence.TryGetValue(out string? value).ShouldBeFalse();
        value.ShouldBeNull();
        presence.ShouldNotBe(Presence<string>.Missing);
    }

    [Fact]
    public void Inactive_value_type_output_is_default_and_does_not_tell_missing_from_null() {
        Presence<int>.Missing.TryGetValue(out int missing).ShouldBeFalse();
        Presence<int>.Null.TryGetValue(out int explicitNull).ShouldBeFalse();
        (missing, explicitNull).ShouldBe((0, 0));
        Presence<Unit>.Null.TryGetValue(out var unit).ShouldBeFalse();
        unit.ShouldBe(Unit.Value);
    }

    [Fact]
    public void Value_keeps_default_like_payloads_as_value() {
        Presence<int>.Value(0).TryGetValue(out int zero).ShouldBeTrue();
        zero.ShouldBe(0);
        Presence<bool>.Value(false).IsValue.ShouldBeTrue();
        Presence<string>.Value(string.Empty).TryGetValue(out string? empty).ShouldBeTrue();
        empty.ShouldBe(string.Empty);
        Presence<Unit>.Value(Unit.Value).IsValue.ShouldBeTrue();
        Presence<int>.Value(default).ShouldNotBe(Presence<int>.Missing);
        (Presence<int>.Value(1).IsMissing, Presence<int>.Value(1).IsNull, Presence<int>.Value(1).IsValue).ShouldBe((false, false, true));
    }

    [Fact]
    public void Value_null_reference_is_rejected_with_value_param_name() {
        var exception = Should.Throw<ArgumentNullException>(() => Presence<string>.Value(null!)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("value");
    }

#pragma warning disable CS8714 // Bypassed notnull constraint: the runtime guard must hold without the warning.
    [Fact]
    public void Value_empty_nullable_is_rejected_when_the_constraint_warning_is_bypassed() {
        var exception = Should.Throw<ArgumentNullException>(() => Presence<int?>.Value(null)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Value_non_empty_nullable_keeps_the_declared_nullable_type() {
        var presence = Presence<int?>.Value(5);

        presence.TryGetValue(out int? value).ShouldBeTrue();
        value.HasValue.ShouldBeTrue();
        value.Value.ShouldBe(5);
        presence.ToString().ShouldBe("Presence.Value(5)");
    }
#pragma warning restore CS8714

    [Fact]
    public void Value_guard_does_not_invoke_payload_code() {
        var probe = new ReferenceProbe(1);

        _ = Presence<ReferenceProbe>.Value(probe);

        (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void Value_keeps_the_exact_reference_without_copying() {
        var payload = new List<int> { 1 };
        var presence = Presence<IEnumerable<int>>.Value(payload);

        payload.Add(2);

        presence.TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        value.ShouldBe([1, 2]);
    }

    [Fact]
    public void Nested_carriers_are_not_flattened() {
        var missing = Presence<Presence<int>>.Value(Presence<int>.Missing);
        var explicitNull = Presence<Presence<int>>.Value(Presence<int>.Null);
        var none = Presence<Maybe<int>>.Value(Maybe<int>.None);

        (missing.IsValue && missing.TryGetValue(out var inner) && inner.IsMissing).ShouldBeTrue();
        (explicitNull.IsValue && explicitNull.TryGetValue(out var innerNull) && innerNull.IsNull).ShouldBeTrue();
        explicitNull.ShouldNotBe(missing);
        (none.TryGetValue(out var maybe) && !maybe.IsSome).ShouldBeTrue();
        missing.ToString().ShouldBe("Presence.Value(Presence.Missing)");
    }

    [Fact]
    public void Flags_do_not_touch_value_payload() {
        ValueProbe.Reset();
        var presence = Presence<ValueProbe>.Value(new ValueProbe(1));

        _ = (presence.IsMissing, presence.IsNull, presence.IsValue, Presence<ValueProbe>.Null.IsNull);

        ValueProbe.Touches.ShouldBe(0);
    }
}
