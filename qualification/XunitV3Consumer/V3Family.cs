using Rezultat;
using Rezultat.Testing.Xunit;
using Xunit;
using Xunit.Sdk;

namespace RunnerConsumer;

public sealed class Family
{
    [Fact]
    public void FailureIsTheV3FailException() =>
        Assert.IsType<FailException>(Record.Exception(() => Result<int, string>.Error("family").ShouldBeOk()));
}
