using System.Globalization;
using System.Reflection;

namespace Rezultat.Tests;

public sealed class UnitTests
{
    [Fact]
    public void Value_default_and_new_are_equal()
    {
        Unit[] values = [Unit.Value, default, new Unit()];

        foreach (var left in values)
        {
            foreach (var right in values)
            {
                Assert.True(left.Equals(right));
                Assert.True(left == right);
                Assert.False(left != right);
                Assert.Equal(left.GetHashCode(), right.GetHashCode());
            }
        }
    }

    [Fact]
    public void Object_equality_accepts_only_boxed_unit()
    {
        Assert.True(Unit.Value.Equals((object)default(Unit)));
        Assert.False(Unit.Value.Equals(null));
        Assert.False(Unit.Value.Equals((object)ValueTuple.Create()));
        Assert.False(Unit.Value.Equals((object)false));
        Assert.False(Unit.Value.Equals(new object()));
    }

    [Fact]
    public void Formats_as_parentheses_independent_of_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");

            Assert.Equal("()", Unit.Value.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Nullable_unit_adds_absence_outside_unit()
    {
        Unit? absent = null;
        Unit? present = Unit.Value;

        Assert.False(absent.HasValue);
        Assert.True(present.HasValue);
        Assert.NotEqual(absent, present);
    }

    [Fact]
    public void Has_no_instance_fields()
    {
        var fields = typeof(Unit).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.Empty(fields);
    }

    [Fact]
    public async Task Awaited_unit_result_is_the_same_carrier()
    {
        var expected = Result<Unit, string>.Ok(Unit.Value);

        var observed = await Task.FromResult(expected);

        Assert.Equal(expected, observed);
        Assert.True(observed.IsSuccess);
    }
}
