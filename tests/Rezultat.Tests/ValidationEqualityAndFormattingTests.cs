using System.Collections.Immutable;

namespace Rezultat.Tests;

public sealed class ValidationEqualityAndFormattingTests
{
    [Fact]
    public void Outcomes_differ_by_state_before_payload_comparison()
    {
        Assert.Equal(default(Validation<int, int>), new Validation<int, int>());
        Assert.NotEqual(Validation<int, int>.Valid(1), Validation<int, int>.Invalid(1));
        Assert.NotEqual(default, Validation<int, int>.Valid(0));
        Assert.NotEqual(default, Validation<int, int>.Invalid(0));
        Assert.True(Validation<int, int>.Valid(1) == Validation<int, int>.Valid(1));
        Assert.True(Validation<int, int>.Valid(1) != Validation<int, int>.Valid(2));
    }

    [Fact]
    public void Separately_built_issue_buffers_with_equal_positions_are_equal()
    {
        var left = Validation<int, string>.InvalidMany(["a", "b"]);
        var right = Validation<int, string>.InvalidMany([.. new[] { "a", "b" }]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.Equal(Validation<int, string>.Invalid("a"), Validation<int, string>.InvalidMany(["a"]));
    }

    [Fact]
    public void Issue_order_duplicates_and_length_are_significant()
    {
        var ab = Validation<int, string>.InvalidMany(["a", "b"]);

        Assert.NotEqual(ab, Validation<int, string>.InvalidMany(["b", "a"]));
        Assert.NotEqual(ab, Validation<int, string>.InvalidMany(["a", "a", "b"]));
        Assert.NotEqual(ab, Validation<int, string>.Invalid("a"));
        Assert.Equal(Validation<int, string?>.InvalidMany([null, "a"]), Validation<int, string?>.InvalidMany([null, "a"]));
    }

    [Fact]
    public void Equal_individual_issues_make_permutations_equal()
    {
        var left = Validation<int, string>.InvalidMany(["x", "X"]);
        var right = Validation<int, string>.InvalidMany(["X", "x"]);

        Assert.NotEqual(left, right);
        Assert.Equal(
            Validation<int, CaseInsensitive>.InvalidMany([new("x"), new("X")]),
            Validation<int, CaseInsensitive>.InvalidMany([new("X"), new("x")]));
    }

    [Fact]
    public void Array_issue_keeps_reference_equality()
    {
        int[] shared = [1];

        Assert.NotEqual(Validation<int, int[]>.Invalid([1]), Validation<int, int[]>.Invalid([1]));
        Assert.Equal(Validation<int, int[]>.Invalid(shared), Validation<int, int[]>.Invalid(shared));
    }

    [Fact]
    public void Inactive_contents_are_not_compared_or_hashed()
    {
        ValueProbe.Reset();
        var valid = Validation<ValueProbe, ValueProbe>.Valid(new ValueProbe(1));
        var invalid = Validation<ValueProbe, ValueProbe>.Invalid(new ValueProbe(1));
        var uninitialized = default(Validation<ValueProbe, ValueProbe>);

        _ = valid.Equals(invalid) || invalid.Equals(valid) || uninitialized.Equals(valid) || uninitialized.Equals(uninitialized);
        _ = uninitialized.GetHashCode();
        Assert.Equal(0, ValueProbe.Touches);

        _ = valid.Equals(valid);
        Assert.Equal(1, ValueProbe.Touches);
        _ = invalid.Equals(invalid);
        Assert.Equal(2, ValueProbe.Touches);
        _ = invalid.GetHashCode();
        Assert.Equal(3, ValueProbe.Touches);
    }

    [Fact]
    public void Issue_comparison_stops_at_first_difference()
    {
        var first = new ReferenceProbe(1);
        var second = new ReferenceProbe(2);

        _ = Validation<int, ReferenceProbe>.InvalidMany([new ReferenceProbe(9), first, second])
            .Equals(Validation<int, ReferenceProbe>.InvalidMany([new ReferenceProbe(8), first, second]));

        Assert.Equal((0, 0), (first.EqualsCalls, second.EqualsCalls));
    }

    [Fact]
    public void Object_equality_requires_the_same_closed_type()
    {
        object boxed = Validation<int, string>.Valid(1);

        Assert.True(boxed.Equals(Validation<int, string>.Valid(1)));
        Assert.False(boxed.Equals(Validation<int, object>.Valid(1)));
        Assert.False(boxed.Equals(Result<int, string>.Ok(1)));
        Assert.False(default(Validation<int, string>).Equals(null));
    }

    [Fact]
    public void Comparer_exceptions_propagate()
    {
        var probe = new ReferenceProbe(1) { ThrowOnEquals = true, ThrowOnHash = true };

        Assert.Throws<ProbeException>(() => Validation<int, ReferenceProbe>.Invalid(probe).Equals(Validation<int, ReferenceProbe>.Invalid(new ReferenceProbe(1))));
        Assert.Throws<ProbeException>(() => Validation<int, ReferenceProbe>.Invalid(probe).GetHashCode());
        Assert.Throws<ProbeException>(() => Validation<ReferenceProbe, int>.Valid(probe).GetHashCode());
    }

    [Fact]
    public void Usable_as_dictionary_key()
    {
        var lookup = new Dictionary<Validation<int, string?>, string>
        {
            [default] = "uninitialized",
            [Validation<int, string?>.Valid(0)] = "valid",
            [Validation<int, string?>.InvalidMany([null, "a"])] = "invalid",
        };

        Assert.Equal("invalid", lookup[Validation<int, string?>.InvalidMany([.. new string?[] { null, "a" }])]);
        Assert.Equal("uninitialized", lookup[default]);
    }

    [Fact]
    public void Text_has_the_rfc_shape()
    {
        Assert.Equal("Validation.Uninitialized", default(Validation<int, int>).ToString());
        Assert.Equal("Validation.Valid(0)", Validation<int, int>.Valid(0).ToString());
        Assert.Equal("Validation.Valid(null)", Validation<string?, int>.Valid(null).ToString());
        Assert.Equal("Validation.Valid(null)", Validation<int?, int>.Valid(null).ToString());
        Assert.Equal("Validation.Invalid([a, null, a])", Validation<int, string?>.InvalidMany(["a", null, "a"]).ToString());
        Assert.Equal("Validation.Invalid([])", Validation<int, string>.Invalid(string.Empty).ToString());
        Assert.Equal("Validation.Invalid([System.Int32[]])", Validation<int, int[]>.Invalid([1]).ToString());
    }

    [Fact]
    public void Text_formats_each_active_position_once_and_normalizes_null_text()
    {
        var repeated = new ReferenceProbe(1, "p");
        var silent = new ReferenceProbe(2) { ReturnNullText = true };
        ValueProbe.Reset();

        Assert.Equal("Validation.Invalid([p, , p])", Validation<ValueProbe, ReferenceProbe>.InvalidMany([repeated, silent, repeated]).ToString());
        Assert.Equal((2, 1), (repeated.ToStringCalls, silent.ToStringCalls));
        Assert.Equal(0, ValueProbe.Touches);
    }

    [Fact]
    public void Formatter_exception_stops_formatting_without_partial_text()
    {
        var failing = new ReferenceProbe(1) { ThrowOnToString = true };
        var later = new ReferenceProbe(2);

        Assert.Throws<ProbeException>(() => Validation<int, ReferenceProbe>.InvalidMany([failing, later]).ToString());
        Assert.Equal(0, later.ToStringCalls);
    }

    [Fact]
    public void Creation_and_observation_do_not_format()
    {
        var probe = new ReferenceProbe(1);

        var validation = Validation<ReferenceProbe, ReferenceProbe>.Invalid(probe);
        _ = validation.TryGetValue(out _, out _);
        _ = validation.Equals(validation);
        _ = validation.GetHashCode();

        Assert.Equal(0, probe.ToStringCalls);
    }

    private sealed record CaseInsensitive(string Text)
    {
        public bool Equals(CaseInsensitive? other) => other is not null && string.Equals(Text, other.Text, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Text);
    }
}
