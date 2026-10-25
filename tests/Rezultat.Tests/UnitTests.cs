using System.Globalization;
using System.Reflection;

namespace Rezultat.Tests;

public sealed class UnitTests {
    [Fact]
    public void Value_default_and_new_are_equal() {
        Unit[] values = [Unit.Value, default, new Unit()];

        foreach (var left in values) {
            foreach (var right in values) {
                left.Equals(right).ShouldBeTrue();
                (left == right).ShouldBeTrue();
                (left != right).ShouldBeFalse();
                right.GetHashCode().ShouldBe(left.GetHashCode());
            }
        }
    }

    [Fact]
    public void Object_equality_accepts_only_boxed_unit() {
        Unit.Value.Equals((object)default(Unit)).ShouldBeTrue();
        Unit.Value.Equals(null).ShouldBeFalse();
        Unit.Value.Equals((object)ValueTuple.Create()).ShouldBeFalse();
        Unit.Value.Equals((object)false).ShouldBeFalse();
        Unit.Value.Equals(new object()).ShouldBeFalse();
    }

    [Fact]
    public void Formats_as_parentheses_independent_of_culture() {
        var original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");

            Unit.Value.ToString().ShouldBe("()");
        } finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Nullable_unit_adds_absence_outside_unit() {
        Unit? absent = null;
        Unit? present = Unit.Value;

        absent.HasValue.ShouldBeFalse();
        present.HasValue.ShouldBeTrue();
        present.ShouldNotBe(absent);
    }

    [Fact]
    public void Has_no_instance_fields() {
        var fields = typeof(Unit).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        fields.ShouldBeEmpty();
    }

    [Fact]
    public async Task Awaited_unit_result_is_the_same_carrier() {
        var expected = Result<Unit, string>.Ok(Unit.Value);

        var observed = await Task.FromResult(expected);

        observed.ShouldBe(expected);
        observed.IsSuccess.ShouldBeTrue();
    }
}
