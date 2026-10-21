using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Rezultat.Testing.Xunit.Tests;

public sealed class ValidationAssertionsTests
{
    [Fact]
    public void ShouldBeValid_ReturnsTheExactPayloadWithoutTouchingIt()
    {
        var payload = new Probe();

        var extracted = Validation<Probe, Probe>.Valid(payload).ShouldBeValid();

        Assert.Same(payload, extracted);
        Assert.Equal(0, payload.Calls);
    }

    [Fact]
    public void ShouldBeValid_ReturnsValueUnitAndNullPayloads()
    {
        Assert.Equal(42, Validation<int, string>.Valid(42).ShouldBeValid());
        Assert.Equal(Unit.Value, Validation<Unit, string>.Valid(Unit.Value).ShouldBeValid());
        Assert.Null(Validation<string?, string>.Valid(null).ShouldBeValid());
    }

    [Fact]
    public void ShouldBeInvalid_ReturnsTheStoredIssueBuffer()
    {
        var first = new Probe();
        var stored = ImmutableArray.Create<Probe?>(first, null, first);
        var validation = Validation<int, Probe?>.InvalidMany(stored);
        validation.TryGetValue(out _, out var observed);

        var extracted = validation.ShouldBeInvalid();

        Assert.Same(ImmutableCollectionsMarshal.AsArray(observed), ImmutableCollectionsMarshal.AsArray(extracted));
        Assert.Same(ImmutableCollectionsMarshal.AsArray(stored), ImmutableCollectionsMarshal.AsArray(extracted));
        Assert.Equal(0, first.Calls);
    }

    [Fact]
    public void ShouldBeInvalid_KeepsASingleValueIssue()
    {
        ValueProbe.Reset();

        var extracted = Validation<string, ValueProbe>.Invalid(new ValueProbe(3)).ShouldBeInvalid();

        Assert.Equal(0, ValueProbe.Calls);
        Assert.Equal(3, Assert.Single(extracted).Id);
    }

    [Fact]
    public void ShouldBeValid_OnInvalidListsEveryIssueInOrder()
    {
        var issues = ImmutableArray.Create<string?>("bad name", "bad age", "bad name", null);

        var failure = Assert.Throws<FailException>(() => Validation<int, string?>.InvalidMany(issues).ShouldBeValid());

        Assert.Equal(
            "Expected Validation.Valid; actual Validation.Invalid; issues=4 [0=bad name, 1=bad age, 2=bad name, 3=null].",
            failure.Message);
    }

    [Fact]
    public void ThrowingIssueFormatterDoesNotStopTheOtherIssues()
    {
        var formatterFault = new PoisonException();
        var issues = ImmutableArray.Create(
            new Probe("first"),
            new Probe { Throws = formatterFault },
            new Probe(null),
            new Probe { Throws = new OperationCanceledException() },
            new Probe("last"));

        var failure = Assert.Throws<FailException>(() => Validation<int, Probe>.InvalidMany(issues).ShouldBeValid());

        Assert.Equal(
            "Expected Validation.Valid; actual Validation.Invalid; issues=5 [0=first, "
            + $"1=<ToString threw {typeof(PoisonException).FullName}>, 2=, 3=<ToString threw System.OperationCanceledException>, 4=last].",
            failure.Message);
        Assert.Equal((0, 0), (formatterFault.MessageReads, formatterFault.TextReads));
        Assert.All(issues, issue => Assert.Equal(1, issue.Calls));
    }

    [Fact]
    public void IssueFormattingUsesTheCurrentCulture()
    {
        using var culture = new CultureScope("de-DE");

        var failure = Assert.Throws<FailException>(() => Validation<int, double>.Invalid(2.5).ShouldBeValid());

        Assert.Equal("Expected Validation.Valid; actual Validation.Invalid; issues=1 [0=2,5].", failure.Message);
    }

    [Fact]
    public void ShouldBeInvalid_OnValidFailsWithTheActivePayload()
    {
        var failure = Assert.Throws<FailException>(() => Validation<Probe?, string>.Valid(null).ShouldBeInvalid());

        Assert.Equal("Expected Validation.Invalid; actual Validation.Valid(null).", failure.Message);
    }

    [Fact]
    public void Uninitialized_FailsAsUninitializedWithoutPayloadEffects()
    {
        ValueProbe.Reset();

        var valid = Assert.Throws<FailException>(() => default(Validation<ValueProbe, ValueProbe>).ShouldBeValid());
        var invalid = Assert.Throws<FailException>(() => default(Validation<ValueProbe, ValueProbe>).ShouldBeInvalid());

        Assert.Equal("Expected Validation.Valid; actual Validation.Uninitialized.", valid.Message);
        Assert.Equal("Expected Validation.Invalid; actual Validation.Uninitialized.", invalid.Message);
        Assert.Equal(0, ValueProbe.Calls);
    }

    [Fact]
    public void FailureStopsTheCurrentLambda()
    {
        var continued = false;

        Assert.Throws<FailException>(() =>
        {
            _ = Validation<int, string>.Valid(1).ShouldBeInvalid();
            continued = true;
        });

        Assert.False(continued);
    }

    [Fact]
    public void FailureKeepsTheCallerAsTheTopFrame()
    {
        var failure = Assert.Throws<FailException>(ExtractFromValid);

        var stack = failure.StackTrace ?? string.Empty;
        Assert.StartsWith($"   at {typeof(ValidationAssertionsTests).FullName}.{nameof(ExtractFromValid)}()", stack, StringComparison.Ordinal);
        Assert.DoesNotContain($"{typeof(ValidationAssertions).FullName}.", stack, StringComparison.Ordinal);
    }

    private static void ExtractFromValid() => Validation<int, string>.Valid(1).ShouldBeInvalid();
}
