using Rezultat;
using Rezultat.Testing.Xunit;
using Xunit;
using Xunit.Sdk;

namespace RunnerConsumer;

public sealed class Family
{
    [Fact]
    public void FailureIsTheV3FailExceptionNotTheV2AssertionFamily()
    {
        var failure = Record.Exception(() => Result<int, string>.Error("family").ShouldBeOk());

        Assert.IsNotAssignableFrom<XunitException>(failure);
        Assert.Equal("Xunit.Sdk.FailException", failure.GetType().FullName);
        Assert.Equal("xunit.v3.assert", failure.GetType().Assembly.GetName().Name);
    }
}
