using System.Collections.Immutable;

namespace Rezultat.Tests;

public sealed class ValidationStateTests
{
    [Fact]
    public void Default_and_new_are_uninitialized_and_differ_from_created_outcomes()
    {
        Validation<string?, string?>[] uninitialized = [default, new()];

        Assert.All(uninitialized, validation =>
        {
            Assert.False(validation.IsInitialized);
            Assert.NotEqual(Validation<string?, string?>.Valid(null), validation);
            Assert.NotEqual(Validation<string?, string?>.Invalid(null), validation);
            Assert.Equal(default, validation);
        });
    }

    [Fact]
    public void Valid_keeps_null_and_default_payloads_as_valid()
    {
        var reference = Validation<string?, int>.Valid(null);
        var number = Validation<int, int>.Valid(0);
        var nullable = Validation<int?, int>.Valid(null);

        Assert.True(reference.IsInitialized && reference.IsValid);
        Assert.True(reference.TryGetValue(out var referenceValue, out _) && referenceValue is null);
        Assert.True(number.TryGetValue(out var numberValue, out _) && numberValue == 0);
        Assert.True(nullable.TryGetValue(out var nullableValue, out _) && !nullableValue.HasValue);
        Assert.True(Validation<bool, int>.Valid(false).IsValid);
    }

    [Fact]
    public void Single_invalid_keeps_null_or_default_issue_as_one_element()
    {
        var reference = Validation<int, string?>.Invalid(null);
        var number = Validation<int, int>.Invalid(0);

        Assert.True(reference.IsInitialized);
        Assert.False(reference.IsValid);
        Assert.False(reference.TryGetValue(out _, out var referenceIssues));
        Assert.Equal([null], referenceIssues);
        Assert.False(number.TryGetValue(out _, out var numberIssues));
        Assert.Equal([0], numberIssues);
    }

    [Fact]
    public void Collection_issue_stays_one_opaque_element()
    {
        int[] issue = [1, 2];

        Assert.False(Validation<int, int[]>.Invalid(issue).TryGetValue(out _, out var issues));
        Assert.Same(issue, Assert.Single(issues));
    }

    [Fact]
    public void InvalidMany_keeps_order_duplicates_and_null_elements()
    {
        ImmutableArray<string?> input = ["b", null, "a", "b"];

        Assert.False(Validation<int, string?>.InvalidMany(input).TryGetValue(out _, out var issues));
        Assert.Equal(["b", null, "a", "b"], issues);
    }

    [Fact]
    public void InvalidMany_rejects_default_and_empty_sequences()
    {
        Assert.Equal("issues", Assert.Throws<ArgumentException>(() => Validation<int, string>.InvalidMany(default)).ParamName);
        Assert.Equal("issues", Assert.Throws<ArgumentException>(() => Validation<int, string>.InvalidMany([])).ParamName);
        Assert.Equal("issues", Assert.Throws<ArgumentException>(() => Validation<int, string>.InvalidMany(ImmutableArray<string>.Empty)).ParamName);
    }

    [Fact]
    public void InvalidMany_snapshot_from_builder_is_stable_after_builder_changes()
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        builder.Add("a");

        var validation = Validation<int, string>.InvalidMany(builder.ToImmutable());
        builder.Add("b");
        builder[0] = "z";

        Assert.False(validation.TryGetValue(out _, out var issues));
        Assert.Equal(["a"], issues);
    }

    [Fact]
    public void TryGetValue_on_valid_outputs_initialized_empty_issues()
    {
        var payload = new ReferenceProbe(1);

        Assert.True(Validation<ReferenceProbe, string>.Valid(payload).TryGetValue(out var value, out var issues));
        Assert.Same(payload, value);
        Assert.False(issues.IsDefault);
        Assert.True(issues.IsEmpty);
    }

    [Fact]
    public void TryGetValue_on_invalid_outputs_default_value_and_stored_issues()
    {
        Assert.False(Validation<string, int>.Invalid(3).TryGetValue(out var value, out var issues));
        Assert.Null(value);
        Assert.Equal([3], issues);
        Assert.False(Validation<int, int>.Invalid(3).TryGetValue(out var number, out _));
        Assert.Equal(0, number);
    }

    [Fact]
    public void Uninitialized_observation_throws_but_storage_and_IsInitialized_are_safe()
    {
        var slots = new Validation<int, string>[2];
        var copy = slots[0];

        Assert.False(copy.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => copy.IsValid);
        Assert.Throws<InvalidOperationException>(() => copy.TryGetValue(out _, out _));
        Assert.Throws<InvalidOperationException>(() => copy.Match(static _ => 0, static _ => 1));
    }

    [Fact]
    public void Match_checks_initialization_before_handlers_then_handlers_in_order()
    {
        var calls = 0;
        var valid = Validation<int, string>.Valid(1);

        Assert.Throws<InvalidOperationException>(() => default(Validation<int, string>).Match<int>(null!, null!));
        Assert.Equal("onValid", Assert.Throws<ArgumentNullException>(() => valid.Match<int>(null!, null!)).ParamName);
        Assert.Equal("onInvalid", Assert.Throws<ArgumentNullException>(() => valid.Match(_ => ++calls, null!)).ParamName);
        Assert.Equal("onValid", Assert.Throws<ArgumentNullException>(() => Validation<int, string>.Invalid("e").Match(null!, _ => ++calls)).ParamName);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Match_invokes_only_the_selected_handler_once_with_exact_contents()
    {
        var input = ImmutableArray.Create("a", "b");
        var calls = new List<string>();

        var valid = Validation<int, string>.Valid(4).Match(value => { calls.Add("valid"); return value; }, issues => { calls.Add("invalid"); return -1; });
        var invalid = Validation<int, string>.InvalidMany(input).Match(
            _ => { calls.Add("valid"); return default(ImmutableArray<string>); },
            issues => { calls.Add("invalid"); return issues; });

        Assert.Equal(4, valid);
        Assert.Equal(input, invalid);
        Assert.Equal(["valid", "invalid"], calls);
        Assert.Null(Validation<int, string>.Valid(1).Match<string?>(static _ => null, static _ => "x"));
    }

    [Fact]
    public void Match_handler_exception_propagates_unchanged()
    {
        var thrown = new OperationCanceledException();

        Assert.Same(thrown, Assert.Throws<OperationCanceledException>(
            () => Validation<int, string>.Invalid("e").Match<int>(static _ => 0, _ => throw thrown)));
    }

    [Fact]
    public void Nested_uninitialized_payload_and_issue_are_ordinary_contents()
    {
        var valid = Validation<Validation<int, int>, int>.Valid(default);
        var invalid = Validation<int, Validation<int, int>>.Invalid(default);

        Assert.True(valid.TryGetValue(out var inner, out _) && !inner.IsInitialized);
        Assert.False(invalid.TryGetValue(out _, out var issues));
        Assert.False(Assert.Single(issues).IsInitialized);
        Assert.Equal("Validation.Valid(Validation.Uninitialized)", valid.ToString());
    }
}
