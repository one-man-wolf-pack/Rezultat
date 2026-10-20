namespace Rezultat.Tests;

public sealed class ResultMatchTests
{
    [Fact]
    public void Success_invokes_only_success_handler_once_with_active_value()
    {
        var calls = new List<string>();

        var text = Result<int, string>.Ok(5).Match(
            value => { calls.Add($"ok:{value}"); return "ok"; },
            error => { calls.Add($"error:{error}"); return "error"; });

        Assert.Equal("ok", text);
        Assert.Equal(["ok:5"], calls);
    }

    [Fact]
    public void Failure_invokes_only_error_handler_once_with_active_error()
    {
        var calls = new List<string>();

        var text = Result<int, int>.Error(5).Match(
            value => { calls.Add($"ok:{value}"); return "ok"; },
            error => { calls.Add($"error:{error}"); return "error"; });

        Assert.Equal("error", text);
        Assert.Equal(["error:5"], calls);
    }

    [Fact]
    public void Handler_receives_null_payload_and_may_return_null()
    {
        string? received = "unset";

        var result = Result<string?, int>.Ok(null).Match<string?>(
            value => { received = value; return null; },
            _ => "error");

        Assert.Null(received);
        Assert.Null(result);
    }

    [Fact]
    public void Uninitialized_throws_before_handler_validation_and_invocation()
    {
        var calls = 0;

        Assert.Throws<InvalidOperationException>(() => default(Result<int, string>).Match(null!, (Func<string, int>)null!));
        Assert.Throws<InvalidOperationException>(() => default(Result<int, string>).Match(
            _ => ++calls,
            _ => ++calls));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Null_handler_is_rejected_before_any_invocation_regardless_of_branch(
        bool success, bool nullSuccessHandler, bool nullErrorHandler)
    {
        var result = success ? Result<int, string>.Ok(1) : Result<int, string>.Error("e");
        var calls = 0;
        Func<int, int>? onSuccess = nullSuccessHandler ? null : _ => ++calls;
        Func<string, int>? onError = nullErrorHandler ? null : _ => ++calls;

        var exception = Assert.Throws<ArgumentNullException>(() => result.Match(onSuccess!, onError!));

        Assert.Equal(nullSuccessHandler ? "onSuccess" : "onError", exception.ParamName);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Handler_exception_propagates_unchanged()
    {
        var thrown = new ProbeException("handler");

        var observed = Assert.Throws<ProbeException>(
            () => Result<int, string>.Error("e").Match<int>(_ => 0, _ => throw thrown));

        Assert.Same(thrown, observed);
    }
}
