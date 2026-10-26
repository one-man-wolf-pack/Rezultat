namespace Rezultat.Tests;

public sealed class ResultEqualityTests {
    [Fact]
    public void Two_uninitialized_results_are_equal_with_equal_hash() {
        var left = default(Result<string, string>);
        var right = new Result<string, string>();

        left.Equals(right).ShouldBeTrue();
        left.Equals((object)right).ShouldBeTrue();
        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
        right.GetHashCode().ShouldBe(left.GetHashCode());
    }

    public static TheoryData<string, Result<string?, string?>> InitializedNullAndDefaultOutcomes => new()
    {
        { "Ok(null)", Result<string?, string?>.Ok(null) },
        { "Error(null)", Result<string?, string?>.Error(null) },
        { "Ok(empty)", Result<string?, string?>.Ok(string.Empty) },
    };

    [Theory]
    [MemberData(nameof(InitializedNullAndDefaultOutcomes))]
    public void Uninitialized_differs_from_every_initialized_outcome(string name, Result<string?, string?> initialized) {
        var uninitialized = default(Result<string?, string?>);

        uninitialized.Equals(initialized).ShouldBeFalse(name);
        initialized.Equals(uninitialized).ShouldBeFalse(name);
        (uninitialized != initialized).ShouldBeTrue(name);
    }

    [Fact]
    public void Uninitialized_differs_from_value_type_default_payload_outcomes() {
        Result<int, bool>.Ok(0).ShouldNotBe(default);
        Result<int, bool>.Error(false).ShouldNotBe(default);
        Result<Unit, Unit>.Ok(Unit.Value).ShouldNotBe(default);
        Result<Unit, Unit>.Error(Unit.Value).ShouldNotBe(default);
    }

    [Fact]
    public void Different_branches_with_identical_payload_are_not_equal() {
        Result<int, int>.Error(1).ShouldNotBe(Result<int, int>.Ok(1));
        Result<string?, string?>.Error(null).ShouldNotBe(Result<string?, string?>.Ok(null));
        Result<Unit, Unit>.Error(Unit.Value).ShouldNotBe(Result<Unit, Unit>.Ok(Unit.Value));
    }

    [Fact]
    public void Same_branch_compares_active_payload_with_default_comparer() {
        Result<string, int>.Ok(new string('a', 1)).ShouldBe(Result<string, int>.Ok("a"));
        Result<string, int>.Ok("A").ShouldNotBe(Result<string, int>.Ok("a"));
        Result<int, string?>.Error(null).ShouldBe(Result<int, string?>.Error(null));
        Result<Unit, string>.Ok(Unit.Value).ShouldBe(Result<Unit, string>.Ok(default));
    }

    [Fact]
    public void NaN_payloads_are_equal_through_default_comparer() {
        (Result<double, int>.Ok(double.NaN) == Result<double, int>.Ok(double.NaN)).ShouldBeTrue();
    }

    [Fact]
    public void Arrays_keep_reference_equality() {
        int[] array = [1, 2];

        Result<int[], int>.Ok(array).ShouldBe(Result<int[], int>.Ok(array));
        Result<int[], int>.Ok([1, 2]).ShouldNotBe(Result<int[], int>.Ok(array));
    }

    [Fact]
    public void Object_equality_rejects_null_raw_payload_and_other_closed_types() {
        var result = Result<int, string>.Ok(1);

        result.Equals(null).ShouldBeFalse();
        result.Equals((object)1).ShouldBeFalse();
        result.Equals(Result<long, string>.Ok(1)).ShouldBeFalse();
        result.Equals(Result<string, int>.Error(1)).ShouldBeFalse();
    }

    [Fact]
    public void Different_branches_do_not_invoke_payload_equality() {
        var value = new ReferenceProbe(1);
        var error = new ReferenceProbe(1);

        (Result<ReferenceProbe, ReferenceProbe>.Ok(value) == Result<ReferenceProbe, ReferenceProbe>.Error(error)).ShouldBeFalse();
        value.EqualsCalls.ShouldBe(0);
        error.EqualsCalls.ShouldBe(0);
    }

    [Fact]
    public void Comparison_with_uninitialized_does_not_invoke_payload_equality() {
        var probe = new ReferenceProbe(1);

        Result<ReferenceProbe, int>.Ok(probe).Equals(default).ShouldBeFalse();
        probe.EqualsCalls.ShouldBe(0);
    }

    [Fact]
    public void Inactive_value_type_slot_is_not_compared_or_hashed() {
        ValueProbe.Reset();
        var left = Result<ValueProbe, int>.Error(1);
        var right = Result<ValueProbe, int>.Error(1);

        (left == right).ShouldBeTrue();
        _ = left.GetHashCode();
        ValueProbe.Touches.ShouldBe(0);
    }

    [Fact]
    public void Uninitialized_equality_and_hash_do_not_touch_value_type_slots() {
        ValueProbe.Reset();
        var left = default(Result<ValueProbe, ValueProbe>);
        var right = default(Result<ValueProbe, ValueProbe>);

        (left == right).ShouldBeTrue();
        (left == Result<ValueProbe, ValueProbe>.Ok(default)).ShouldBeFalse();
        _ = left.GetHashCode();
        ValueProbe.Touches.ShouldBe(0);
    }

    [Fact]
    public void Active_value_type_payload_is_compared() {
        ValueProbe.Reset();

        (Result<ValueProbe, int>.Ok(new ValueProbe(2)) == Result<ValueProbe, int>.Ok(new ValueProbe(2))).ShouldBeTrue();
        ValueProbe.Touches.ShouldNotBe(0);
    }

    [Fact]
    public void Payload_equality_exception_propagates_unchanged() {
        var left = Result<int, ReferenceProbe>.Error(new ReferenceProbe(1) { ThrowOnEquals = true });
        var right = Result<int, ReferenceProbe>.Error(new ReferenceProbe(1));

        Should.Throw<ProbeException>(() => left.Equals(right));
    }

    [Fact]
    public void Equal_results_have_equal_hashes_including_null_payload() {
        Result<string?, int>.Ok(null).GetHashCode().ShouldBe(Result<string?, int>.Ok(null).GetHashCode());
        Result<int, string>.Error(new string('x', 1)).GetHashCode().ShouldBe(Result<int, string>.Error("x").GetHashCode());
        Result<Unit, Unit>.Error(Unit.Value).GetHashCode().ShouldBe(Result<Unit, Unit>.Error(default).GetHashCode());
    }

    [Fact]
    public void Hash_uses_active_payload_and_not_inactive_or_uninitialized() {
        var probe = new ReferenceProbe(4);

        _ = Result<ReferenceProbe, int>.Ok(probe).GetHashCode();
        _ = default(Result<ReferenceProbe, ReferenceProbe>).GetHashCode();

        probe.HashCalls.ShouldBe(1);
    }

    [Fact]
    public void Payload_hash_exception_propagates_unchanged() {
        var result = Result<ReferenceProbe, int>.Ok(new ReferenceProbe(1) { ThrowOnHash = true });

        Should.Throw<ProbeException>(() => result.GetHashCode());
    }

    [Fact]
    public void Results_work_as_dictionary_keys_with_branch_distinction() {
        var map = new Dictionary<Result<int, int>, string> {
            [Result<int, int>.Ok(1)] = "ok",
            [Result<int, int>.Error(1)] = "error",
            [default] = "uninitialized",
        };

        map[Result<int, int>.Ok(1)].ShouldBe("ok");
        map[Result<int, int>.Error(1)].ShouldBe("error");
        map[default].ShouldBe("uninitialized");
    }

    [Fact]
    public void External_comparer_can_be_built_from_public_observation() {
        var comparer = new CaseInsensitiveErrorComparer();
        var set = new HashSet<Result<int, string>>(comparer)
        {
            Result<int, string>.Error("Missing"),
            Result<int, string>.Error("MISSING"),
            default,
            Result<int, string>.Ok(1),
        };

        set.Count.ShouldBe(3);
        Result<int, string>.Error("MISSING").ShouldNotBe(Result<int, string>.Error("Missing"));
    }

    private sealed class CaseInsensitiveErrorComparer : IEqualityComparer<Result<int, string>> {
        public bool Equals(Result<int, string> x, Result<int, string> y) {
            if (!x.IsInitialized || !y.IsInitialized) {
                return x.IsInitialized == y.IsInitialized;
            }

            bool xSuccess = x.TryGetValue(out int xValue, out string? xError);
            bool ySuccess = y.TryGetValue(out int yValue, out string? yError);
            if (xSuccess != ySuccess) {
                return false;
            }

            return xSuccess ? xValue == yValue : StringComparer.OrdinalIgnoreCase.Equals(xError, yError);
        }

        public int GetHashCode(Result<int, string> obj) {
            if (!obj.IsInitialized) {
                return 0;
            }

            return obj.TryGetValue(out int value, out string? error)
                ? HashCode.Combine(1, value)
                : HashCode.Combine(2, StringComparer.OrdinalIgnoreCase.GetHashCode(error));
        }
    }
}
