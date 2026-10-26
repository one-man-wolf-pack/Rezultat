using System.Globalization;

namespace Rezultat.Tests;

public sealed class ResultFormattingTests {
    [Fact]
    public void Formats_branches_with_active_payload_text() {
        Result<int, string>.Ok(42).ToString().ShouldBe("Result.Ok(42)");
        Result<int, string>.Error("not found").ToString().ShouldBe("Result.Error(not found)");
        default(Result<int, string>).ToString().ShouldBe("Result.Uninitialized");
    }

    [Fact]
    public void Formats_null_payload_as_null_without_formatter() {
        Result<string?, int>.Ok(null).ToString().ShouldBe("Result.Ok(null)");
        Result<int, string?>.Error(null).ToString().ShouldBe("Result.Error(null)");
        Result<int?, int>.Ok(null).ToString().ShouldBe("Result.Ok(null)");
    }

    [Fact]
    public void Formats_unit_payload() {
        Result<Unit, string>.Ok(Unit.Value).ToString().ShouldBe("Result.Ok(())");
        Result<Unit, Unit>.Error(Unit.Value).ToString().ShouldBe("Result.Error(())");
    }

    [Fact]
    public void Text_is_neither_quoted_nor_escaped() {
        Result<int, string>.Error("line1\n\"quoted\"").ToString().ShouldBe("Result.Error(line1\n\"quoted\")");
        Result<string, int>.Ok("null").ToString().ShouldBe("Result.Ok(null)");
    }

    [Fact]
    public void Null_and_empty_formatter_results_become_empty_text() {
        Result<ReferenceProbe, int>.Ok(new ReferenceProbe(1) { ReturnNullText = true }).ToString().ShouldBe("Result.Ok()");
        Result<int, string>.Error(string.Empty).ToString().ShouldBe("Result.Error()");
    }

    [Fact]
    public void Invokes_active_payload_formatter_exactly_once() {
        var probe = new ReferenceProbe(1, "text");

        Result<ReferenceProbe, int>.Ok(probe).ToString().ShouldBe("Result.Ok(text)");
        probe.ToStringCalls.ShouldBe(1);
    }

    [Fact]
    public void Does_not_format_inactive_or_uninitialized_payload() {
        ValueProbe.Reset();

        _ = Result<ValueProbe, ValueProbe>.Error(new ValueProbe(1)).ToString();
        int touchesAfterActiveError = ValueProbe.Touches;
        _ = default(Result<ValueProbe, ValueProbe>).ToString();

        touchesAfterActiveError.ShouldBe(1);
        ValueProbe.Touches.ShouldBe(1);
    }

    [Fact]
    public void Formatter_exception_propagates_unchanged() {
        var result = Result<int, ReferenceProbe>.Error(new ReferenceProbe(1) { ThrowOnToString = true });

        Should.Throw<ProbeException>(() => result.ToString());
    }

    [Fact]
    public void Numeric_payload_uses_current_culture() {
        var original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Result<double, int>.Ok(1.5).ToString().ShouldBe("Result.Ok(1,5)");
        } finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Creation_and_observation_do_not_format_payload() {
        var probe = new ReferenceProbe(1);

        var result = Result<ReferenceProbe, int>.Ok(probe);
        _ = result.IsSuccess;
        _ = result.TryGetValue(out _, out _);
        _ = result.Match(p => p.Id, e => e);

        probe.ToStringCalls.ShouldBe(0);
    }

    [Fact]
    public void Nested_result_uses_its_own_text() {
        var nested = Result<Result<int, string>, string>.Ok(Result<int, string>.Error("inner"));

        nested.ToString().ShouldBe("Result.Ok(Result.Error(inner))");
    }
}
