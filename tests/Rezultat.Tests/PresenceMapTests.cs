using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Rezultat.Tests;

public sealed class PresenceMapTests
{
    [Fact]
    public void Value_invokes_selector_once_with_the_exact_payload()
    {
        var payload = new ReferenceProbe(1);
        var seen = new List<ReferenceProbe>();

        var mapped = Presence<ReferenceProbe>.Value(payload).Map(value => { seen.Add(value); return value.Id + 1; });

        Assert.Equal(Presence<int>.Value(2), mapped);
        Assert.Same(payload, Assert.Single(seen));
    }

    [Fact]
    public void Missing_and_null_are_kept_without_invoking_the_selector()
    {
        var calls = 0;

        Assert.True(Presence<int>.Missing.Map(value => ++calls).IsMissing);
        Assert.True(default(Presence<int>).Map(value => ++calls).IsMissing);
        Assert.True(Presence<int>.Null.Map(value => ++calls).IsNull);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("value")]
    public void Null_selector_is_rejected_in_every_state(string state)
    {
        var presence = state switch { "value" => Presence<int>.Value(1), "null" => Presence<int>.Null, _ => Presence<int>.Missing };

        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => presence.Map<int>(null!)).ParamName);
    }

    [Fact]
    public void Default_like_selector_results_are_values()
    {
        Assert.Equal(Presence<int>.Value(0), Presence<int>.Value(5).Map(static _ => 0));
        Assert.Equal(Presence<bool>.Value(false), Presence<int>.Value(5).Map(static _ => false));
        Assert.Equal(Presence<string>.Value(string.Empty), Presence<int>.Value(5).Map(static _ => string.Empty));
    }

    [Fact]
    public void Null_selector_result_is_an_invalid_operation_not_a_null_state_or_factory_argument_error()
    {
        Assert.Throws<InvalidOperationException>(() => Presence<int>.Value(1).Map<string>(static _ => null!));
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => Presence<string>.Value(null!)).ParamName);
    }

#pragma warning disable CS8714 // Bypassed notnull constraint: the runtime guard must hold without the warning.
    [Fact]
    public void Empty_nullable_selector_result_is_rejected_and_non_empty_one_is_kept()
    {
        Assert.Throws<InvalidOperationException>(() => Presence<int>.Value(1).Map<int?>(static _ => null));

        var kept = Presence<int>.Value(1).Map<int?>(static value => value + 1);
        Assert.True(kept.TryGetValue(out var value) && value.HasValue && value.Value == 2);
    }
#pragma warning restore CS8714

    [Fact]
    public void Selector_exceptions_propagate_unchanged_including_invalid_operation()
    {
        var invalid = new InvalidOperationException("selector");
        var canceled = new OperationCanceledException();

        Assert.Same(invalid, Assert.Throws<InvalidOperationException>(() => Presence<int>.Value(1).Map<int>(_ => throw invalid)));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => Presence<int>.Value(1).Map<int>(_ => throw canceled)));
    }

    [Fact]
    public void Nested_carriers_and_tasks_are_ordinary_payloads()
    {
        var nested = Presence<int>.Value(1).Map(static _ => Presence<int>.Missing);
        var task = Presence<int>.Value(1).Map(static _ => Task.FromException<int>(new ProbeException("task")));

        Assert.True(nested.IsValue && nested.TryGetValue(out var inner) && inner.IsMissing);
        Assert.True(task.IsValue && task.TryGetValue(out var pending) && pending.IsFaulted);
    }

    [Fact]
    public void Receiver_is_not_changed()
    {
        var presence = Presence<int>.Value(1);

        _ = presence.Map(static value => value + 1);

        Assert.Equal(Presence<int>.Value(1), presence);
    }
}

public static class PresenceValues
{
    public static Arbitrary<Presence<int>> SmallInts() =>
        Gen.OneOf(
                Gen.Constant(Presence<int>.Missing),
                Gen.Constant(Presence<int>.Null),
                Gen.Choose(-3, 3).Select(Presence<int>.Value))
            .ToArbitrary();

    public static Arbitrary<Func<int, Presence<int>>> Steps() =>
        (from state in ArbMap.Default.GeneratorFor<Func<int, int>>()
         from value in ArbMap.Default.GeneratorFor<Func<int, int>>()
         select (Func<int, Presence<int>>)(input => (state(input) & 3) switch
         {
             0 => Presence<int>.Missing,
             1 => Presence<int>.Null,
             2 => default,
             _ => Presence<int>.Value(value(input)),
         }))
            .ToArbitrary();
}

[Properties(Arbitrary = [typeof(PresenceValues)])]
public sealed class PresencePropertyTests
{
    [Property]
    public bool Equality_forms_agree_and_are_symmetric(Presence<int> left, Presence<int> right) =>
        left.Equals(right) == (left == right)
        && (left != right) == !(left == right)
        && left.Equals((object)right) == left.Equals(right)
        && left.Equals(right) == right.Equals(left)
        && (!left.Equals(right) || left.GetHashCode() == right.GetHashCode());

    [Property]
    public bool Exactly_one_flag_is_set(Presence<int> presence) =>
        (presence.IsMissing ? 1 : 0) + (presence.IsNull ? 1 : 0) + (presence.IsValue ? 1 : 0) == 1
        && presence.TryGetValue(out _) == presence.IsValue;

    [Property]
    public bool Map_preserves_identity(Presence<int> presence) => presence.Map(static value => value) == presence;

    [Property]
    public bool Map_preserves_composition(Presence<int> presence, Func<int, int> f, Func<int, int> g) =>
        presence.Map(f).Map(g) == presence.Map(value => g(f(value)));

    [Property]
    public bool AndThen_has_left_identity(int value, Func<int, Presence<int>> next) =>
        Presence<int>.Value(value).AndThen(next) == next(value);

    [Property]
    public bool AndThen_has_right_identity(Presence<int> presence) =>
        presence.AndThen(Presence<int>.Value) == presence;

    [Property]
    public bool AndThen_is_associative(Presence<int> presence, Func<int, Presence<int>> f, Func<int, Presence<int>> g) =>
        presence.AndThen(f).AndThen(g) == presence.AndThen(value => f(value).AndThen(g));

    [Property]
    public bool Map_equals_AndThen_of_value(Presence<int> presence, Func<int, int> f) =>
        presence.Map(f) == presence.AndThen(value => Presence<int>.Value(f(value)));

    [Property]
    public bool Filters_equal_AndThen_with_the_named_state(Presence<int> presence, Func<int, bool> p) =>
        presence.FilterToMissing(p) == presence.AndThen(value => p(value) ? Presence<int>.Value(value) : Presence<int>.Missing)
        && presence.FilterToNull(p) == presence.AndThen(value => p(value) ? Presence<int>.Value(value) : Presence<int>.Null);

    [Property]
    public bool Traverse_of_ok_equals_ok_of_Map(Presence<int> presence, Func<int, int> f) =>
        presence.Traverse(value => Result<int, string>.Ok(f(value))) == Result<Presence<int>, string>.Ok(presence.Map(f));
}
