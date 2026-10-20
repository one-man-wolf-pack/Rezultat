using System.Globalization;

namespace Rezultat.Tests;

public sealed class MaybeFormattingTests
{
    [Fact]
    public void Formats_alternatives()
    {
        Assert.Equal("Maybe.Some(42)", Maybe<int>.Some(42).ToString());
        Assert.Equal("Maybe.None", Maybe<int>.None.ToString());
        Assert.Equal("Maybe.None", default(Maybe<string>).ToString());
        Assert.Equal("Maybe.None", new Maybe<string>().ToString());
    }

    [Fact]
    public void Formats_present_null_as_null()
    {
        Assert.Equal("Maybe.Some(null)", Maybe<string?>.Some(null).ToString());
        Assert.Equal("Maybe.Some(null)", Maybe<int?>.Some(null).ToString());
        Assert.Equal("Maybe.Some(null)", Maybe<string>.Some("null").ToString());
    }

    [Fact]
    public void Formats_unit_and_nested_carriers_with_their_own_text()
    {
        Assert.Equal("Maybe.Some(())", Maybe<Unit>.Some(Unit.Value).ToString());
        Assert.Equal("Maybe.Some(Maybe.None)", Maybe<Maybe<int>>.Some(Maybe<int>.None).ToString());
        Assert.Equal("Maybe.Some(Maybe.Some(1))", Maybe<Maybe<int>>.Some(Maybe<int>.Some(1)).ToString());
        Assert.Equal("Maybe.Some(Result.Error(e))", Maybe<Result<int, string>>.Some(Result<int, string>.Error("e")).ToString());
    }

    [Fact]
    public void Text_is_neither_quoted_nor_escaped()
    {
        Assert.Equal("Maybe.Some(line1\n\"quoted\")", Maybe<string>.Some("line1\n\"quoted\"").ToString());
    }

    [Fact]
    public void Null_and_empty_formatter_results_become_empty_text()
    {
        Assert.Equal("Maybe.Some()", Maybe<ReferenceProbe>.Some(new ReferenceProbe(1) { ReturnNullText = true }).ToString());
        Assert.Equal("Maybe.Some()", Maybe<string>.Some(string.Empty).ToString());
    }

    [Fact]
    public void Invokes_active_payload_formatter_exactly_once_per_call()
    {
        var probe = new ReferenceProbe(1, "text");
        var maybe = Maybe<ReferenceProbe>.Some(probe);

        Assert.Equal(0, probe.ToStringCalls);
        Assert.Equal("Maybe.Some(text)", maybe.ToString());
        Assert.Equal(1, probe.ToStringCalls);
        Assert.Equal("Maybe.Some(text)", maybe.ToString());
        Assert.Equal(2, probe.ToStringCalls);
    }

    [Fact]
    public void None_does_not_format_value_type_slot()
    {
        ValueProbe.Reset();

        _ = Maybe<ValueProbe>.None.ToString();
        Assert.Equal(0, ValueProbe.Touches);
        Assert.Equal("Maybe.Some(value1)", Maybe<ValueProbe>.Some(new ValueProbe(1)).ToString());
        Assert.Equal(1, ValueProbe.Touches);
    }

    [Fact]
    public void Formatter_exceptions_propagate()
    {
        Assert.Throws<ProbeException>(() => Maybe<ReferenceProbe>.Some(new ReferenceProbe(1) { ThrowOnToString = true }).ToString());
    }

    [Fact]
    public void Payload_text_follows_current_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("Maybe.Some(1,5)", Maybe<double>.Some(1.5).ToString());
            Assert.Equal("Maybe.Some(1,5)", $"{Maybe<double>.Some(1.5)}");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
