using System.Globalization;

namespace Rezultat.Tests;

public sealed class ResultFormattingTests
{
    [Fact]
    public void Formats_branches_with_active_payload_text()
    {
        Assert.Equal("Result.Ok(42)", Result<int, string>.Ok(42).ToString());
        Assert.Equal("Result.Error(not found)", Result<int, string>.Error("not found").ToString());
        Assert.Equal("Result.Uninitialized", default(Result<int, string>).ToString());
    }

    [Fact]
    public void Formats_null_payload_as_null_without_formatter()
    {
        Assert.Equal("Result.Ok(null)", Result<string?, int>.Ok(null).ToString());
        Assert.Equal("Result.Error(null)", Result<int, string?>.Error(null).ToString());
        Assert.Equal("Result.Ok(null)", Result<int?, int>.Ok(null).ToString());
    }

    [Fact]
    public void Formats_unit_payload()
    {
        Assert.Equal("Result.Ok(())", Result<Unit, string>.Ok(Unit.Value).ToString());
        Assert.Equal("Result.Error(())", Result<Unit, Unit>.Error(Unit.Value).ToString());
    }

    [Fact]
    public void Text_is_neither_quoted_nor_escaped()
    {
        Assert.Equal("Result.Error(line1\n\"quoted\")", Result<int, string>.Error("line1\n\"quoted\"").ToString());
        Assert.Equal("Result.Ok(null)", Result<string, int>.Ok("null").ToString());
    }

    [Fact]
    public void Null_and_empty_formatter_results_become_empty_text()
    {
        Assert.Equal("Result.Ok()", Result<ReferenceProbe, int>.Ok(new ReferenceProbe(1) { ReturnNullText = true }).ToString());
        Assert.Equal("Result.Error()", Result<int, string>.Error(string.Empty).ToString());
    }

    [Fact]
    public void Invokes_active_payload_formatter_exactly_once()
    {
        var probe = new ReferenceProbe(1, "text");

        Assert.Equal("Result.Ok(text)", Result<ReferenceProbe, int>.Ok(probe).ToString());
        Assert.Equal(1, probe.ToStringCalls);
    }

    [Fact]
    public void Does_not_format_inactive_or_uninitialized_payload()
    {
        ValueProbe.Reset();

        _ = Result<ValueProbe, ValueProbe>.Error(new ValueProbe(1)).ToString();
        var touchesAfterActiveError = ValueProbe.Touches;
        _ = default(Result<ValueProbe, ValueProbe>).ToString();

        Assert.Equal(1, touchesAfterActiveError);
        Assert.Equal(1, ValueProbe.Touches);
    }

    [Fact]
    public void Formatter_exception_propagates_unchanged()
    {
        var result = Result<int, ReferenceProbe>.Error(new ReferenceProbe(1) { ThrowOnToString = true });

        Assert.Throws<ProbeException>(() => result.ToString());
    }

    [Fact]
    public void Numeric_payload_uses_current_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("Result.Ok(1,5)", Result<double, int>.Ok(1.5).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Creation_and_observation_do_not_format_payload()
    {
        var probe = new ReferenceProbe(1);

        var result = Result<ReferenceProbe, int>.Ok(probe);
        _ = result.IsSuccess;
        _ = result.TryGetValue(out _, out _);
        _ = result.Match(p => p.Id, e => e);

        Assert.Equal(0, probe.ToStringCalls);
    }

    [Fact]
    public void Nested_result_uses_its_own_text()
    {
        var nested = Result<Result<int, string>, string>.Ok(Result<int, string>.Error("inner"));

        Assert.Equal("Result.Ok(Result.Error(inner))", nested.ToString());
    }
}
