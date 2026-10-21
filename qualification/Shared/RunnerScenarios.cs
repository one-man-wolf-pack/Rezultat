using Rezultat;
using Rezultat.Testing.Xunit;
using Xunit;

namespace RunnerConsumer;

public sealed class Positive
{
    [Fact]
    public void ExtractsAllFourBranches()
    {
        Assert.Equal(42, Result<int, string>.Ok(42).ShouldBeOk());
        Assert.Equal("refused", Result<int, string>.Error("refused").ShouldBeError());
        Assert.Equal("Ada", Validation<string, string>.Valid("Ada").ShouldBeValid());
        Assert.Equal(new[] { "bad", "bad", null }, Validation<int, string?>.InvalidMany(["bad", "bad", null]).ShouldBeInvalid());
    }

    [Fact]
    public void KeepsNullablePayloads()
    {
        string? text = Result<string?, string>.Ok(null).ShouldBeOk();
        Assert.Null(text);
    }

    [Fact]
    public void FailureStopsOnlyTheCurrentMultipleLambda()
    {
        var continued = false;
        var otherRan = false;

        Assert.ThrowsAny<Exception>(() => Assert.Multiple(
            () =>
            {
                _ = Result<int, string>.Error("stop").ShouldBeOk();
                continued = true;
            },
            () => otherRan = true));

        Assert.False(continued);
        Assert.True(otherRan);
    }
}

public sealed class ExpectedFailures
{
    [Fact]
    public void WrongBranch() => _ = Result<int, string>.Error("wrong-branch-evidence").ShouldBeOk();

    [Fact]
    public void Uninitialized() => _ = default(Validation<int, string>).ShouldBeValid();

    [Fact]
    public void FormatterFault() => _ = Validation<int, ThrowingText>.Invalid(new ThrowingText()).ShouldBeValid();

    [Fact]
    public void InsideMultiple() => Assert.Multiple(
        () => _ = Result<int, string>.Ok(7).ShouldBeError(),
        () => Assert.True(true));

    public sealed class ThrowingText
    {
        public override string ToString() => throw new InvalidOperationException("formatter");
    }
}
