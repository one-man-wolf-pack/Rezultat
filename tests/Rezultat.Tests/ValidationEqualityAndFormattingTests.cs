namespace Rezultat.Tests;

public sealed class ValidationEqualityAndFormattingTests {
    [Fact]
    public void Outcomes_differ_by_state_before_payload_comparison() {
        new Validation<int, int>().ShouldBe(default(Validation<int, int>));
        Validation<int, int>.Invalid(1).ShouldNotBe(Validation<int, int>.Valid(1));
        Validation<int, int>.Valid(0).ShouldNotBe(default);
        Validation<int, int>.Invalid(0).ShouldNotBe(default);
        (Validation<int, int>.Valid(1) == Validation<int, int>.Valid(1)).ShouldBeTrue();
        (Validation<int, int>.Valid(1) != Validation<int, int>.Valid(2)).ShouldBeTrue();
    }

    [Fact]
    public void Separately_built_issue_buffers_with_equal_positions_are_equal() {
        var left = Validation<int, string>.InvalidMany(["a", "b"]);
        var right = Validation<int, string>.InvalidMany([.. new[] { "a", "b" }]);

        right.ShouldBe(left);
        right.GetHashCode().ShouldBe(left.GetHashCode());
        Validation<int, string>.InvalidMany(["a"]).ShouldBe(Validation<int, string>.Invalid("a"));
    }

    [Fact]
    public void Issue_order_duplicates_and_length_are_significant() {
        var ab = Validation<int, string>.InvalidMany(["a", "b"]);

        Validation<int, string>.InvalidMany(["b", "a"]).ShouldNotBe(ab);
        Validation<int, string>.InvalidMany(["a", "a", "b"]).ShouldNotBe(ab);
        Validation<int, string>.Invalid("a").ShouldNotBe(ab);
        Validation<int, string?>.InvalidMany([null, "a"]).ShouldBe(Validation<int, string?>.InvalidMany([null, "a"]));
    }

    [Fact]
    public void Equal_individual_issues_make_permutations_equal() {
        var left = Validation<int, string>.InvalidMany(["x", "X"]);
        var right = Validation<int, string>.InvalidMany(["X", "x"]);

        right.ShouldNotBe(left);
        Validation<int, CaseInsensitive>.InvalidMany([new("X"), new("x")]).ShouldBe(Validation<int, CaseInsensitive>.InvalidMany([new("x"), new("X")]));
    }

    [Fact]
    public void Array_issue_keeps_reference_equality() {
        int[] shared = [1];

        Validation<int, int[]>.Invalid([1]).ShouldNotBe(Validation<int, int[]>.Invalid([1]));
        Validation<int, int[]>.Invalid(shared).ShouldBe(Validation<int, int[]>.Invalid(shared));
    }

    [Fact]
    public void Inactive_contents_are_not_compared_or_hashed() {
        ValueProbe.Reset();
        var valid = Validation<ValueProbe, ValueProbe>.Valid(new ValueProbe(1));
        var invalid = Validation<ValueProbe, ValueProbe>.Invalid(new ValueProbe(1));
        var uninitialized = default(Validation<ValueProbe, ValueProbe>);

        _ = valid.Equals(invalid) || invalid.Equals(valid) || uninitialized.Equals(valid) || uninitialized.Equals(uninitialized);
        _ = uninitialized.GetHashCode();
        ValueProbe.Touches.ShouldBe(0);

        _ = valid.Equals(valid);
        ValueProbe.Touches.ShouldBe(1);
        _ = invalid.Equals(invalid);
        ValueProbe.Touches.ShouldBe(2);
        _ = invalid.GetHashCode();
        ValueProbe.Touches.ShouldBe(3);
    }

    [Fact]
    public void Issue_comparison_stops_at_first_difference() {
        var first = new ReferenceProbe(1);
        var second = new ReferenceProbe(2);

        _ = Validation<int, ReferenceProbe>.InvalidMany([new ReferenceProbe(9), first, second])
            .Equals(Validation<int, ReferenceProbe>.InvalidMany([new ReferenceProbe(8), first, second]));

        (first.EqualsCalls, second.EqualsCalls).ShouldBe((0, 0));
    }

    [Fact]
    public void Object_equality_requires_the_same_closed_type() {
        object boxed = Validation<int, string>.Valid(1);

        boxed.Equals(Validation<int, string>.Valid(1)).ShouldBeTrue();
        boxed.Equals(Validation<int, object>.Valid(1)).ShouldBeFalse();
        boxed.Equals(Result<int, string>.Ok(1)).ShouldBeFalse();
        default(Validation<int, string>).Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Comparer_exceptions_propagate() {
        var probe = new ReferenceProbe(1) { ThrowOnEquals = true, ThrowOnHash = true };

        Should.Throw<ProbeException>(() => Validation<int, ReferenceProbe>.Invalid(probe).Equals(Validation<int, ReferenceProbe>.Invalid(new ReferenceProbe(1))));
        Should.Throw<ProbeException>(() => Validation<int, ReferenceProbe>.Invalid(probe).GetHashCode());
        Should.Throw<ProbeException>(() => Validation<ReferenceProbe, int>.Valid(probe).GetHashCode());
    }

    [Fact]
    public void Usable_as_dictionary_key() {
        var lookup = new Dictionary<Validation<int, string?>, string> {
            [default] = "uninitialized",
            [Validation<int, string?>.Valid(0)] = "valid",
            [Validation<int, string?>.InvalidMany([null, "a"])] = "invalid",
        };

        lookup[Validation<int, string?>.InvalidMany([.. new string?[] { null, "a" }])].ShouldBe("invalid");
        lookup[default].ShouldBe("uninitialized");
    }

    [Fact]
    public void Text_has_the_rfc_shape() {
        default(Validation<int, int>).ToString().ShouldBe("Validation.Uninitialized");
        Validation<int, int>.Valid(0).ToString().ShouldBe("Validation.Valid(0)");
        Validation<string?, int>.Valid(null).ToString().ShouldBe("Validation.Valid(null)");
        Validation<int?, int>.Valid(null).ToString().ShouldBe("Validation.Valid(null)");
        Validation<int, string?>.InvalidMany(["a", null, "a"]).ToString().ShouldBe("Validation.Invalid([a, null, a])");
        Validation<int, string>.Invalid(string.Empty).ToString().ShouldBe("Validation.Invalid([])");
        Validation<int, int[]>.Invalid([1]).ToString().ShouldBe("Validation.Invalid([System.Int32[]])");
    }

    [Fact]
    public void Text_formats_each_active_position_once_and_normalizes_null_text() {
        var repeated = new ReferenceProbe(1, "p");
        var silent = new ReferenceProbe(2) { ReturnNullText = true };
        ValueProbe.Reset();

        Validation<ValueProbe, ReferenceProbe>.InvalidMany([repeated, silent, repeated]).ToString().ShouldBe("Validation.Invalid([p, , p])");
        (repeated.ToStringCalls, silent.ToStringCalls).ShouldBe((2, 1));
        ValueProbe.Touches.ShouldBe(0);
    }

    [Fact]
    public void Formatter_exception_stops_formatting_without_partial_text() {
        var failing = new ReferenceProbe(1) { ThrowOnToString = true };
        var later = new ReferenceProbe(2);

        Should.Throw<ProbeException>(() => Validation<int, ReferenceProbe>.InvalidMany([failing, later]).ToString());
        later.ToStringCalls.ShouldBe(0);
    }

    [Fact]
    public void Creation_and_observation_do_not_format() {
        var probe = new ReferenceProbe(1);

        var validation = Validation<ReferenceProbe, ReferenceProbe>.Invalid(probe);
        _ = validation.TryGetValue(out _, out _);
        _ = validation.Equals(validation);
        _ = validation.GetHashCode();

        probe.ToStringCalls.ShouldBe(0);
    }

    private sealed record CaseInsensitive(string Text) {
        public bool Equals(CaseInsensitive? other) => other is not null && string.Equals(Text, other.Text, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Text);
    }
}
