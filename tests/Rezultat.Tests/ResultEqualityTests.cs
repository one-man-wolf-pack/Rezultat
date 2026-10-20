namespace Rezultat.Tests;

public sealed class ResultEqualityTests
{
    [Fact]
    public void Two_uninitialized_results_are_equal_with_equal_hash()
    {
        var left = default(Result<string, string>);
        var right = new Result<string, string>();

        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object)right));
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    public static TheoryData<string, Result<string?, string?>> InitializedNullAndDefaultOutcomes => new()
    {
        { "Ok(null)", Result<string?, string?>.Ok(null) },
        { "Error(null)", Result<string?, string?>.Error(null) },
        { "Ok(empty)", Result<string?, string?>.Ok(string.Empty) },
    };

    [Theory]
    [MemberData(nameof(InitializedNullAndDefaultOutcomes))]
    public void Uninitialized_differs_from_every_initialized_outcome(string name, Result<string?, string?> initialized)
    {
        var uninitialized = default(Result<string?, string?>);

        Assert.False(uninitialized.Equals(initialized), name);
        Assert.False(initialized.Equals(uninitialized), name);
        Assert.True(uninitialized != initialized, name);
    }

    [Fact]
    public void Uninitialized_differs_from_value_type_default_payload_outcomes()
    {
        Assert.NotEqual(default, Result<int, bool>.Ok(0));
        Assert.NotEqual(default, Result<int, bool>.Error(false));
        Assert.NotEqual(default, Result<Unit, Unit>.Ok(Unit.Value));
        Assert.NotEqual(default, Result<Unit, Unit>.Error(Unit.Value));
    }

    [Fact]
    public void Different_branches_with_identical_payload_are_not_equal()
    {
        Assert.NotEqual(Result<int, int>.Ok(1), Result<int, int>.Error(1));
        Assert.NotEqual(Result<string?, string?>.Ok(null), Result<string?, string?>.Error(null));
        Assert.NotEqual(Result<Unit, Unit>.Ok(Unit.Value), Result<Unit, Unit>.Error(Unit.Value));
    }

    [Fact]
    public void Same_branch_compares_active_payload_with_default_comparer()
    {
        Assert.Equal(Result<string, int>.Ok("a"), Result<string, int>.Ok(new string('a', 1)));
        Assert.NotEqual(Result<string, int>.Ok("a"), Result<string, int>.Ok("A"));
        Assert.Equal(Result<int, string?>.Error(null), Result<int, string?>.Error(null));
        Assert.Equal(Result<Unit, string>.Ok(default), Result<Unit, string>.Ok(Unit.Value));
    }

    [Fact]
    public void NaN_payloads_are_equal_through_default_comparer()
    {
        Assert.True(Result<double, int>.Ok(double.NaN) == Result<double, int>.Ok(double.NaN));
    }

    [Fact]
    public void Arrays_keep_reference_equality()
    {
        int[] array = [1, 2];

        Assert.Equal(Result<int[], int>.Ok(array), Result<int[], int>.Ok(array));
        Assert.NotEqual(Result<int[], int>.Ok(array), Result<int[], int>.Ok([1, 2]));
    }

    [Fact]
    public void Object_equality_rejects_null_raw_payload_and_other_closed_types()
    {
        var result = Result<int, string>.Ok(1);

        Assert.False(result.Equals(null));
        Assert.False(result.Equals((object)1));
        Assert.False(result.Equals(Result<long, string>.Ok(1)));
        Assert.False(result.Equals(Result<string, int>.Error(1)));
    }

    [Fact]
    public void Different_branches_do_not_invoke_payload_equality()
    {
        var value = new ReferenceProbe(1);
        var error = new ReferenceProbe(1);

        Assert.False(Result<ReferenceProbe, ReferenceProbe>.Ok(value) == Result<ReferenceProbe, ReferenceProbe>.Error(error));
        Assert.Equal(0, value.EqualsCalls);
        Assert.Equal(0, error.EqualsCalls);
    }

    [Fact]
    public void Comparison_with_uninitialized_does_not_invoke_payload_equality()
    {
        var probe = new ReferenceProbe(1);

        Assert.False(Result<ReferenceProbe, int>.Ok(probe).Equals(default));
        Assert.Equal(0, probe.EqualsCalls);
    }

    [Fact]
    public void Inactive_value_type_slot_is_not_compared_or_hashed()
    {
        ValueProbe.Reset();
        var left = Result<ValueProbe, int>.Error(1);
        var right = Result<ValueProbe, int>.Error(1);

        Assert.True(left == right);
        _ = left.GetHashCode();
        Assert.Equal(0, ValueProbe.Touches);
    }

    [Fact]
    public void Uninitialized_equality_and_hash_do_not_touch_value_type_slots()
    {
        ValueProbe.Reset();
        var left = default(Result<ValueProbe, ValueProbe>);
        var right = default(Result<ValueProbe, ValueProbe>);

        Assert.True(left == right);
        Assert.False(left == Result<ValueProbe, ValueProbe>.Ok(default));
        _ = left.GetHashCode();
        Assert.Equal(0, ValueProbe.Touches);
    }

    [Fact]
    public void Active_value_type_payload_is_compared()
    {
        ValueProbe.Reset();

        Assert.True(Result<ValueProbe, int>.Ok(new ValueProbe(2)) == Result<ValueProbe, int>.Ok(new ValueProbe(2)));
        Assert.NotEqual(0, ValueProbe.Touches);
    }

    [Fact]
    public void Payload_equality_exception_propagates_unchanged()
    {
        var left = Result<int, ReferenceProbe>.Error(new ReferenceProbe(1) { ThrowOnEquals = true });
        var right = Result<int, ReferenceProbe>.Error(new ReferenceProbe(1));

        Assert.Throws<ProbeException>(() => left.Equals(right));
    }

    [Fact]
    public void Equal_results_have_equal_hashes_including_null_payload()
    {
        Assert.Equal(Result<string?, int>.Ok(null).GetHashCode(), Result<string?, int>.Ok(null).GetHashCode());
        Assert.Equal(Result<int, string>.Error("x").GetHashCode(), Result<int, string>.Error(new string('x', 1)).GetHashCode());
        Assert.Equal(Result<Unit, Unit>.Error(default).GetHashCode(), Result<Unit, Unit>.Error(Unit.Value).GetHashCode());
    }

    [Fact]
    public void Hash_uses_active_payload_and_not_inactive_or_uninitialized()
    {
        var probe = new ReferenceProbe(4);

        _ = Result<ReferenceProbe, int>.Ok(probe).GetHashCode();
        _ = default(Result<ReferenceProbe, ReferenceProbe>).GetHashCode();

        Assert.Equal(1, probe.HashCalls);
    }

    [Fact]
    public void Payload_hash_exception_propagates_unchanged()
    {
        var result = Result<ReferenceProbe, int>.Ok(new ReferenceProbe(1) { ThrowOnHash = true });

        Assert.Throws<ProbeException>(() => result.GetHashCode());
    }

    [Fact]
    public void Results_work_as_dictionary_keys_with_branch_distinction()
    {
        var map = new Dictionary<Result<int, int>, string>
        {
            [Result<int, int>.Ok(1)] = "ok",
            [Result<int, int>.Error(1)] = "error",
            [default] = "uninitialized",
        };

        Assert.Equal("ok", map[Result<int, int>.Ok(1)]);
        Assert.Equal("error", map[Result<int, int>.Error(1)]);
        Assert.Equal("uninitialized", map[default]);
    }

    [Fact]
    public void External_comparer_can_be_built_from_public_observation()
    {
        var comparer = new CaseInsensitiveErrorComparer();
        var set = new HashSet<Result<int, string>>(comparer)
        {
            Result<int, string>.Error("Missing"),
            Result<int, string>.Error("MISSING"),
            default,
            Result<int, string>.Ok(1),
        };

        Assert.Equal(3, set.Count);
        Assert.NotEqual(Result<int, string>.Error("Missing"), Result<int, string>.Error("MISSING"));
    }

    private sealed class CaseInsensitiveErrorComparer : IEqualityComparer<Result<int, string>>
    {
        public bool Equals(Result<int, string> x, Result<int, string> y)
        {
            if (!x.IsInitialized || !y.IsInitialized)
            {
                return x.IsInitialized == y.IsInitialized;
            }

            var xSuccess = x.TryGetValue(out var xValue, out var xError);
            var ySuccess = y.TryGetValue(out var yValue, out var yError);
            if (xSuccess != ySuccess)
            {
                return false;
            }

            return xSuccess ? xValue == yValue : StringComparer.OrdinalIgnoreCase.Equals(xError, yError);
        }

        public int GetHashCode(Result<int, string> obj)
        {
            if (!obj.IsInitialized)
            {
                return 0;
            }

            return obj.TryGetValue(out var value, out var error)
                ? HashCode.Combine(1, value)
                : HashCode.Combine(2, StringComparer.OrdinalIgnoreCase.GetHashCode(error));
        }
    }
}
