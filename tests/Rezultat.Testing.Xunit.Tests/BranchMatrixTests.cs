using System.Collections.Immutable;
using FsCheck;
using FsCheck.Xunit;

namespace Rezultat.Testing.Xunit.Tests;

public sealed class BranchMatrixTests
{
    public enum State
    {
        Uninitialized,
        First,
        Second,
    }

    public enum Assertion
    {
        ShouldBeOk,
        ShouldBeError,
        ShouldBeValid,
        ShouldBeInvalid,
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExtractsOnlyTheExpectedInitializedBranch(Assertion assertion, State state, bool extracts)
    {
        var payload = new Probe("payload");
        var other = new Probe("other");
        Func<object?> act = assertion switch
        {
            Assertion.ShouldBeOk => () => ResultIn(state, payload, other).ShouldBeOk(),
            Assertion.ShouldBeError => () => ResultIn(state, other, payload).ShouldBeError(),
            Assertion.ShouldBeValid => () => ValidationIn(state, payload, other).ShouldBeValid(),
            _ => () => Assert.Single(ValidationIn(state, other, payload).ShouldBeInvalid()),
        };

        if (extracts)
        {
            Assert.Same(payload, act());
        }
        else
        {
            Assert.Throws<FailException>(act);
        }
    }

    public static TheoryData<Assertion, State, bool> Cases() => new()
    {
        { Assertion.ShouldBeOk, State.Uninitialized, false },
        { Assertion.ShouldBeOk, State.First, true },
        { Assertion.ShouldBeOk, State.Second, false },
        { Assertion.ShouldBeError, State.Uninitialized, false },
        { Assertion.ShouldBeError, State.First, false },
        { Assertion.ShouldBeError, State.Second, true },
        { Assertion.ShouldBeValid, State.Uninitialized, false },
        { Assertion.ShouldBeValid, State.First, true },
        { Assertion.ShouldBeValid, State.Second, false },
        { Assertion.ShouldBeInvalid, State.Uninitialized, false },
        { Assertion.ShouldBeInvalid, State.First, false },
        { Assertion.ShouldBeInvalid, State.Second, true },
    };

    [Property]
    public void ShouldBeInvalidReturnsArbitraryIssuesUnchangedAndShouldBeValidListsThem(NonEmptyArray<string?> generated)
    {
        var issues = ImmutableArray.Create(generated.Get);
        var validation = Validation<int, string?>.InvalidMany(issues);

        var extracted = validation.ShouldBeInvalid();
        var failure = Assert.Throws<FailException>(() => validation.ShouldBeValid());

        Assert.True(extracted == issues);
        var listed = string.Join(", ", issues.Select((issue, index) => $"{index}={issue ?? "null"}"));
        Assert.Equal($"Expected Validation.Valid; actual Validation.Invalid; issues={issues.Length} [{listed}].", failure.Message);
    }

    private static Result<Probe, Probe> ResultIn(State state, Probe ok, Probe error) => state switch
    {
        State.First => Result<Probe, Probe>.Ok(ok),
        State.Second => Result<Probe, Probe>.Error(error),
        _ => default,
    };

    private static Validation<Probe, Probe> ValidationIn(State state, Probe valid, Probe issue) => state switch
    {
        State.First => Validation<Probe, Probe>.Valid(valid),
        State.Second => Validation<Probe, Probe>.Invalid(issue),
        _ => default,
    };
}
