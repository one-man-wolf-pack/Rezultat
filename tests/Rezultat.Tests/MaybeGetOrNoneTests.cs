using System.Collections.ObjectModel;
using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeGetOrNoneTests {
    [Fact]
    public void Null_source_is_rejected_for_both_interfaces() {
        Should.Throw<ArgumentNullException>(() => ((IReadOnlyDictionary<string, int>)null!).GetOrNone("a")).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => ((IDictionary<string, int>)null!).GetOrNone("a")).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
    }

    [Fact]
    public void Type_with_both_interfaces_binds_to_read_only_overload() {
        var source = new TrackedDictionary<string, string>(
            [new("key", "dictionary")],
            [new("key", "read-only")]);

        source.GetOrNone("key").ShouldBe(Maybe<string>.Some("read-only"));
        (source.DictionaryLookups, source.ReadOnlyLookups).ShouldBe((0, 1));
    }

    [Fact]
    public void Static_interface_type_keeps_its_own_dispatch() {
        var source = new TrackedDictionary<string, string>(
            [new("key", "dictionary")],
            [new("key", "read-only")]);

        ((IDictionary<string, string>)source).GetOrNone("key").ShouldBe(Maybe<string>.Some("dictionary"));
        ((IReadOnlyDictionary<string, string>)source).GetOrNone("key").ShouldBe(Maybe<string>.Some("read-only"));
        (source.DictionaryLookups, source.ReadOnlyLookups).ShouldBe((1, 1));
    }

    [Fact]
    public void Miss_is_none_regardless_of_out_value_with_one_lookup() {
        var source = new TrackedDictionary<string, string> { MissValue = "leaked" };

        source.GetOrNone("absent").IsSome.ShouldBeFalse();
        ((IDictionary<string, string>)source).GetOrNone("absent").IsSome.ShouldBeFalse();
        (source.DictionaryLookups, source.ReadOnlyLookups).ShouldBe((1, 1));
        source.LookedUpKeys.ShouldBe(["absent", "absent"]);
    }

    [Fact]
    public void Found_null_default_and_nested_values_are_some() {
        var references = new TrackedDictionary<int, string?>(new KeyValuePair<int, string?>(1, null));
        var values = new Dictionary<string, int> { ["zero"] = 0 };
        var nullables = new Dictionary<string, int?> { ["empty"] = null };
        var nested = new Dictionary<string, Maybe<int>> { ["none"] = Maybe<int>.None };
        var results = new Dictionary<string, Result<int, string>> { ["default"] = default };

        references.GetOrNone(1).ShouldBe(Maybe<string?>.Some(null));
        values.GetOrNone("zero").ShouldBe(Maybe<int>.Some(0));
        nullables.GetOrNone("empty").ShouldBe(Maybe<int?>.Some(null));
        nested.GetOrNone("none").ShouldBe(Maybe<Maybe<int>>.Some(Maybe<int>.None));
        (results.GetOrNone("default").TryGetValue(out var inner) && !inner.IsInitialized).ShouldBeTrue();
    }

    [Fact]
    public void Found_reference_value_keeps_identity_without_payload_members() {
        var probe = new ReferenceProbe(1);
        var source = new Dictionary<int, ReferenceProbe> { [1] = probe };

        (source.GetOrNone(1).TryGetValue(out var found) && ReferenceEquals(probe, found)).ShouldBeTrue();
        (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void Key_is_passed_unchecked_and_its_policy_belongs_to_the_source() {
        var nullKeys = new TrackedDictionary<string?, int>(new KeyValuePair<string?, int>(null, 7));
        var standard = new Dictionary<string, int>();

        nullKeys.GetOrNone(null).ShouldBe(Maybe<int>.Some(7));
        nullKeys.LookedUpKeys.ShouldBe([null]);
        Should.Throw<ArgumentNullException>(() => standard.GetOrNone(null!)).ShouldBeOfType<ArgumentNullException>();
    }

    [Fact]
    public void Source_comparer_decides_the_match() {
        var source = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Key"] = 1 };

        source.GetOrNone("KEY").ShouldBe(Maybe<int>.Some(1));
        new ReadOnlyDictionary<string, int>(source).GetOrNone("key").ShouldBe(Maybe<int>.Some(1));
    }

    [Fact]
    public void Lookup_exceptions_propagate_unchanged() {
        var canceled = new OperationCanceledException();
        var source = new TrackedDictionary<string, int> { LookupException = canceled };

        Should.Throw<OperationCanceledException>(() => source.GetOrNone("a")).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
        Should.Throw<OperationCanceledException>(() => ((IDictionary<string, int>)source).GetOrNone("a")).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
    }

    [Fact]
    public void Repeated_call_looks_up_again() {
        var source = new Dictionary<string, int> { ["a"] = 1 };

        source.GetOrNone("a").IsSome.ShouldBeTrue();
        source.Remove("a");
        source.GetOrNone("a").IsSome.ShouldBeFalse();
    }
}
