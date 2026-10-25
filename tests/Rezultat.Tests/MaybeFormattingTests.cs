using System.Globalization;

namespace Rezultat.Tests;

public sealed class MaybeFormattingTests {
    [Fact]
    public void Formats_alternatives() {
        Maybe<int>.Some(42).ToString().ShouldBe("Maybe.Some(42)");
        Maybe<int>.None.ToString().ShouldBe("Maybe.None");
        default(Maybe<string>).ToString().ShouldBe("Maybe.None");
        new Maybe<string>().ToString().ShouldBe("Maybe.None");
    }

    [Fact]
    public void Formats_present_null_as_null() {
        Maybe<string?>.Some(null).ToString().ShouldBe("Maybe.Some(null)");
        Maybe<int?>.Some(null).ToString().ShouldBe("Maybe.Some(null)");
        Maybe<string>.Some("null").ToString().ShouldBe("Maybe.Some(null)");
    }

    [Fact]
    public void Formats_unit_and_nested_carriers_with_their_own_text() {
        Maybe<Unit>.Some(Unit.Value).ToString().ShouldBe("Maybe.Some(())");
        Maybe<Maybe<int>>.Some(Maybe<int>.None).ToString().ShouldBe("Maybe.Some(Maybe.None)");
        Maybe<Maybe<int>>.Some(Maybe<int>.Some(1)).ToString().ShouldBe("Maybe.Some(Maybe.Some(1))");
        Maybe<Result<int, string>>.Some(Result<int, string>.Error("e")).ToString().ShouldBe("Maybe.Some(Result.Error(e))");
    }

    [Fact]
    public void Text_is_neither_quoted_nor_escaped() {
        Maybe<string>.Some("line1\n\"quoted\"").ToString().ShouldBe("Maybe.Some(line1\n\"quoted\")");
    }

    [Fact]
    public void Null_and_empty_formatter_results_become_empty_text() {
        Maybe<ReferenceProbe>.Some(new ReferenceProbe(1) { ReturnNullText = true }).ToString().ShouldBe("Maybe.Some()");
        Maybe<string>.Some(string.Empty).ToString().ShouldBe("Maybe.Some()");
    }

    [Fact]
    public void Invokes_active_payload_formatter_exactly_once_per_call() {
        var probe = new ReferenceProbe(1, "text");
        var maybe = Maybe<ReferenceProbe>.Some(probe);

        probe.ToStringCalls.ShouldBe(0);
        maybe.ToString().ShouldBe("Maybe.Some(text)");
        probe.ToStringCalls.ShouldBe(1);
        maybe.ToString().ShouldBe("Maybe.Some(text)");
        probe.ToStringCalls.ShouldBe(2);
    }

    [Fact]
    public void None_does_not_format_value_type_slot() {
        ValueProbe.Reset();

        _ = Maybe<ValueProbe>.None.ToString();
        ValueProbe.Touches.ShouldBe(0);
        Maybe<ValueProbe>.Some(new ValueProbe(1)).ToString().ShouldBe("Maybe.Some(value1)");
        ValueProbe.Touches.ShouldBe(1);
    }

    [Fact]
    public void Formatter_exceptions_propagate() {
        Should.Throw<ProbeException>(() => Maybe<ReferenceProbe>.Some(new ReferenceProbe(1) { ThrowOnToString = true }).ToString());
    }

    [Fact]
    public void Payload_text_follows_current_culture() {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Maybe<double>.Some(1.5).ToString().ShouldBe("Maybe.Some(1,5)");
            $"{Maybe<double>.Some(1.5)}".ShouldBe("Maybe.Some(1,5)");
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
