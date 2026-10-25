using System.Collections.Immutable;

namespace Rezultat.Tests;

public sealed class ValidationStateTests {
    [Fact]
    public void Default_and_new_are_uninitialized_and_differ_from_created_outcomes() {
        Validation<string?, string?>[] uninitialized = [default, new()];

        foreach (var validation in uninitialized) {
            validation.IsInitialized.ShouldBeFalse();
            validation.ShouldNotBe(Validation<string?, string?>.Valid(null));
            validation.ShouldNotBe(Validation<string?, string?>.Invalid(null));
            validation.ShouldBe(default);
        }
    }

    [Fact]
    public void Valid_keeps_null_and_default_payloads_as_valid() {
        var reference = Validation<string?, int>.Valid(null);
        var number = Validation<int, int>.Valid(0);
        var nullable = Validation<int?, int>.Valid(null);

        (reference.IsInitialized && reference.IsValid).ShouldBeTrue();
        (reference.TryGetValue(out var referenceValue, out _) && referenceValue is null).ShouldBeTrue();
        (number.TryGetValue(out var numberValue, out _) && numberValue == 0).ShouldBeTrue();
        (nullable.TryGetValue(out var nullableValue, out _) && !nullableValue.HasValue).ShouldBeTrue();
        Validation<bool, int>.Valid(false).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Single_invalid_keeps_null_or_default_issue_as_one_element() {
        var reference = Validation<int, string?>.Invalid(null);
        var number = Validation<int, int>.Invalid(0);

        reference.IsInitialized.ShouldBeTrue();
        reference.IsValid.ShouldBeFalse();
        reference.TryGetValue(out _, out var referenceIssues).ShouldBeFalse();
        referenceIssues.ShouldBe([null]);
        number.TryGetValue(out _, out var numberIssues).ShouldBeFalse();
        numberIssues.ShouldBe([0]);
    }

    [Fact]
    public void Collection_issue_stays_one_opaque_element() {
        int[] issue = [1, 2];

        Validation<int, int[]>.Invalid(issue).TryGetValue(out _, out var issues).ShouldBeFalse();
        issues.ShouldHaveSingleItem().ShouldBeSameAs(issue);
    }

    [Fact]
    public void InvalidMany_keeps_order_duplicates_and_null_elements() {
        ImmutableArray<string?> input = ["b", null, "a", "b"];

        Validation<int, string?>.InvalidMany(input).TryGetValue(out _, out var issues).ShouldBeFalse();
        issues.ShouldBe(["b", null, "a", "b"]);
    }

    [Fact]
    public void InvalidMany_rejects_default_and_empty_sequences() {
        Should.Throw<ArgumentException>(() => Validation<int, string>.InvalidMany(default)).ShouldBeOfType<ArgumentException>().ParamName.ShouldBe("issues");
        Should.Throw<ArgumentException>(() => Validation<int, string>.InvalidMany([])).ShouldBeOfType<ArgumentException>().ParamName.ShouldBe("issues");
        Should.Throw<ArgumentException>(() => Validation<int, string>.InvalidMany(ImmutableArray<string>.Empty)).ShouldBeOfType<ArgumentException>().ParamName.ShouldBe("issues");
    }

    [Fact]
    public void InvalidMany_snapshot_from_builder_is_stable_after_builder_changes() {
        var builder = ImmutableArray.CreateBuilder<string>();
        builder.Add("a");

        var validation = Validation<int, string>.InvalidMany(builder.ToImmutable());
        builder.Add("b");
        builder[0] = "z";

        validation.TryGetValue(out _, out var issues).ShouldBeFalse();
        issues.ShouldBe(["a"]);
    }

    [Fact]
    public void TryGetValue_on_valid_outputs_initialized_empty_issues() {
        var payload = new ReferenceProbe(1);

        Validation<ReferenceProbe, string>.Valid(payload).TryGetValue(out var value, out var issues).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        issues.IsDefault.ShouldBeFalse();
        issues.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryGetValue_on_invalid_outputs_default_value_and_stored_issues() {
        Validation<string, int>.Invalid(3).TryGetValue(out var value, out var issues).ShouldBeFalse();
        value.ShouldBeNull();
        issues.ShouldBe([3]);
        Validation<int, int>.Invalid(3).TryGetValue(out var number, out _).ShouldBeFalse();
        number.ShouldBe(0);
    }

    [Fact]
    public void Uninitialized_observation_throws_but_storage_and_IsInitialized_are_safe() {
        var slots = new Validation<int, string>[2];
        var copy = slots[0];

        copy.IsInitialized.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => copy.IsValid).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => copy.TryGetValue(out _, out _)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => copy.Match(static _ => 0, static _ => 1)).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Match_checks_initialization_before_handlers_then_handlers_in_order() {
        var calls = 0;
        var valid = Validation<int, string>.Valid(1);

        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).Match<int>(null!, null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<ArgumentNullException>(() => valid.Match<int>(null!, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("onValid");
        Should.Throw<ArgumentNullException>(() => valid.Match(_ => ++calls, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("onInvalid");
        Should.Throw<ArgumentNullException>(() => Validation<int, string>.Invalid("e").Match(null!, _ => ++calls)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("onValid");
        calls.ShouldBe(0);
    }

    [Fact]
    public void Match_invokes_only_the_selected_handler_once_with_exact_contents() {
        var input = ImmutableArray.Create("a", "b");
        var calls = new List<string>();

        var valid = Validation<int, string>.Valid(4).Match(value => { calls.Add("valid"); return value; }, issues => { calls.Add("invalid"); return -1; });
        var invalid = Validation<int, string>.InvalidMany(input).Match(
            _ => { calls.Add("valid"); return default(ImmutableArray<string>); },
            issues => { calls.Add("invalid"); return issues; });

        valid.ShouldBe(4);
        invalid.ShouldBe(input);
        calls.ShouldBe(["valid", "invalid"]);
        Validation<int, string>.Valid(1).Match<string?>(static _ => null, static _ => "x").ShouldBeNull();
    }

    [Fact]
    public void Match_handler_exception_propagates_unchanged() {
        var thrown = new OperationCanceledException();

        Should.Throw<OperationCanceledException>(() => Validation<int, string>.Invalid("e").Match<int>(static _ => 0, _ => throw thrown)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(thrown);
    }

    [Fact]
    public void Nested_uninitialized_payload_and_issue_are_ordinary_contents() {
        var valid = Validation<Validation<int, int>, int>.Valid(default);
        var invalid = Validation<int, Validation<int, int>>.Invalid(default);

        (valid.TryGetValue(out var inner, out _) && !inner.IsInitialized).ShouldBeTrue();
        invalid.TryGetValue(out _, out var issues).ShouldBeFalse();
        issues.ShouldHaveSingleItem().IsInitialized.ShouldBeFalse();
        valid.ToString().ShouldBe("Validation.Valid(Validation.Uninitialized)");
    }
}
