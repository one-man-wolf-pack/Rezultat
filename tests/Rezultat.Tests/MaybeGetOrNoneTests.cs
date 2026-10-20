using System.Collections.ObjectModel;

namespace Rezultat.Tests;

public sealed class MaybeGetOrNoneTests
{
    [Fact]
    public void Null_source_is_rejected_for_both_interfaces()
    {
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((IReadOnlyDictionary<string, int>)null!).GetOrNone("a")).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((IDictionary<string, int>)null!).GetOrNone("a")).ParamName);
    }

    [Fact]
    public void Type_with_both_interfaces_binds_to_read_only_overload()
    {
        var source = new TrackedDictionary<string, string>(
            [new("key", "dictionary")],
            [new("key", "read-only")]);

        Assert.Equal(Maybe<string>.Some("read-only"), source.GetOrNone("key"));
        Assert.Equal((0, 1), (source.DictionaryLookups, source.ReadOnlyLookups));
    }

    [Fact]
    public void Static_interface_type_keeps_its_own_dispatch()
    {
        var source = new TrackedDictionary<string, string>(
            [new("key", "dictionary")],
            [new("key", "read-only")]);

        Assert.Equal(Maybe<string>.Some("dictionary"), ((IDictionary<string, string>)source).GetOrNone("key"));
        Assert.Equal(Maybe<string>.Some("read-only"), ((IReadOnlyDictionary<string, string>)source).GetOrNone("key"));
        Assert.Equal((1, 1), (source.DictionaryLookups, source.ReadOnlyLookups));
    }

    [Fact]
    public void Miss_is_none_regardless_of_out_value_with_one_lookup()
    {
        var source = new TrackedDictionary<string, string> { MissValue = "leaked" };

        Assert.False(source.GetOrNone("absent").IsSome);
        Assert.False(((IDictionary<string, string>)source).GetOrNone("absent").IsSome);
        Assert.Equal((1, 1), (source.DictionaryLookups, source.ReadOnlyLookups));
        Assert.Equal(["absent", "absent"], source.LookedUpKeys);
    }

    [Fact]
    public void Found_null_default_and_nested_values_are_some()
    {
        var references = new TrackedDictionary<int, string?>(new KeyValuePair<int, string?>(1, null));
        var values = new Dictionary<string, int> { ["zero"] = 0 };
        var nullables = new Dictionary<string, int?> { ["empty"] = null };
        var nested = new Dictionary<string, Maybe<int>> { ["none"] = Maybe<int>.None };
        var results = new Dictionary<string, Result<int, string>> { ["default"] = default };

        Assert.Equal(Maybe<string?>.Some(null), references.GetOrNone(1));
        Assert.Equal(Maybe<int>.Some(0), values.GetOrNone("zero"));
        Assert.Equal(Maybe<int?>.Some(null), nullables.GetOrNone("empty"));
        Assert.Equal(Maybe<Maybe<int>>.Some(Maybe<int>.None), nested.GetOrNone("none"));
        Assert.True(results.GetOrNone("default").TryGetValue(out var inner) && !inner.IsInitialized);
    }

    [Fact]
    public void Found_reference_value_keeps_identity_without_payload_members()
    {
        var probe = new ReferenceProbe(1);
        var source = new Dictionary<int, ReferenceProbe> { [1] = probe };

        Assert.True(source.GetOrNone(1).TryGetValue(out var found) && ReferenceEquals(probe, found));
        Assert.Equal((0, 0, 0), (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls));
    }

    [Fact]
    public void Key_is_passed_unchecked_and_its_policy_belongs_to_the_source()
    {
        var nullKeys = new TrackedDictionary<string?, int>(new KeyValuePair<string?, int>(null, 7));
        var standard = new Dictionary<string, int>();

        Assert.Equal(Maybe<int>.Some(7), nullKeys.GetOrNone(null));
        Assert.Equal([null], nullKeys.LookedUpKeys);
        Assert.Throws<ArgumentNullException>(() => standard.GetOrNone(null!));
    }

    [Fact]
    public void Source_comparer_decides_the_match()
    {
        var source = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Key"] = 1 };

        Assert.Equal(Maybe<int>.Some(1), source.GetOrNone("KEY"));
        Assert.Equal(Maybe<int>.Some(1), new ReadOnlyDictionary<string, int>(source).GetOrNone("key"));
    }

    [Fact]
    public void Lookup_exceptions_propagate_unchanged()
    {
        var canceled = new OperationCanceledException();
        var source = new TrackedDictionary<string, int> { LookupException = canceled };

        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => source.GetOrNone("a")));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => ((IDictionary<string, int>)source).GetOrNone("a")));
    }

    [Fact]
    public void Repeated_call_looks_up_again()
    {
        var source = new Dictionary<string, int> { ["a"] = 1 };

        Assert.True(source.GetOrNone("a").IsSome);
        source.Remove("a");
        Assert.False(source.GetOrNone("a").IsSome);
    }
}
