using System.Collections.Immutable;
using FsCheck;
using FsCheck.Xunit;

namespace Rezultat.Testing.Xunit.Tests;

public sealed class BranchMatrixTests {
    public enum State {
        Uninitialized,
        First,
        Second,
    }

    public enum Assertion {
        ShouldBeOk,
        ShouldBeError,
        ShouldBeValid,
        ShouldBeInvalid,
    }

    public enum Form {
        WithoutFlag,
        IncludePayload,
        ExcludePayload,
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExtractsOnlyTheExpectedInitializedBranch(Assertion assertion, State state, Form form, bool extracts) {
        var payload = new Probe("payload");
        var other = new Probe("other");
        var act = Act(assertion, state, form, payload, other);

        if (extracts) {
            act().ShouldBeSameAs(payload);
            (payload.Calls + other.Calls).ShouldBe(0);
        } else {
            Should.Throw<FailException>(act).ShouldBeOfType<FailException>();
        }
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public void CallWithoutFlagFailsExactlyLikeIncludePayloadTrue(Assertion assertion, State state) {
        var withoutFlag = Should.Throw<FailException>(Act(assertion, state, Form.WithoutFlag, new Probe("payload"), new Probe("other"))).ShouldBeOfType<FailException>();
        var included = Should.Throw<FailException>(Act(assertion, state, Form.IncludePayload, new Probe("payload"), new Probe("other"))).ShouldBeOfType<FailException>();

        included.Message.ShouldBe(withoutFlag.Message);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public void ExcludedPayloadFailureNamesOnlyTheBranches(Assertion assertion, State state) {
        const string Secret = "secret-sentinel";
        var payload = new Probe(Secret);
        var other = new Probe(Secret) { Throws = new InvalidOperationException(Secret) };

        var failure = Should.Throw<FailException>(Act(assertion, state, Form.ExcludePayload, payload, other)).ShouldBeOfType<FailException>();

        (payload.Calls + other.Calls).ShouldBe(0);
        failure.Message.ShouldNotContain(Secret, Case.Sensitive);
        failure.Message.ShouldNotContain("ToString", Case.Sensitive);
        failure.Message.ShouldNotContain(character => char.IsDigit(character));
        var carrier = assertion is Assertion.ShouldBeOk or Assertion.ShouldBeError ? "Result" : "Validation";
        failure.Message.ShouldContain($"{carrier}.{assertion.ToString()["ShouldBe".Length..]}", Case.Sensitive);
        failure.Message.ShouldContain(state == State.Uninitialized ? $"{carrier}.Uninitialized" : $"{carrier}.{ActualBranch(assertion)}", Case.Sensitive);
    }

    public static TheoryData<Assertion, State, Form, bool> Cases() {
        var cases = new TheoryData<Assertion, State, Form, bool>();
        foreach (var form in Enum.GetValues<Form>()) {
            foreach (var assertion in Enum.GetValues<Assertion>()) {
                var extracting = assertion is Assertion.ShouldBeOk or Assertion.ShouldBeValid ? State.First : State.Second;
                foreach (var state in Enum.GetValues<State>()) {
                    cases.Add(assertion, state, form, state == extracting);
                }
            }
        }

        return cases;
    }

    public static TheoryData<Assertion, State> Failures() => new()
    {
        { Assertion.ShouldBeOk, State.Uninitialized },
        { Assertion.ShouldBeOk, State.Second },
        { Assertion.ShouldBeError, State.Uninitialized },
        { Assertion.ShouldBeError, State.First },
        { Assertion.ShouldBeValid, State.Uninitialized },
        { Assertion.ShouldBeValid, State.Second },
        { Assertion.ShouldBeInvalid, State.Uninitialized },
        { Assertion.ShouldBeInvalid, State.First },
    };

    [Property]
    public void ShouldBeInvalidReturnsArbitraryIssuesUnchangedAndShouldBeValidListsThem(NonEmptyArray<string?> generated) {
        var issues = ImmutableArray.Create(generated.Get);
        var validation = Validation<int, string?>.InvalidMany(issues);

        var extracted = validation.ShouldBeInvalid();
        var failure = Should.Throw<FailException>(() => validation.ShouldBeValid()).ShouldBeOfType<FailException>();

        (extracted == issues).ShouldBeTrue();
        var listed = string.Join(", ", issues.Select((issue, index) => $"{index}={issue ?? "null"}"));
        failure.Message.ShouldBe($"Expected Validation.Valid; actual Validation.Invalid; issues={issues.Length} [{listed}].");
    }

    [Property]
    public void ExcludedPayloadKeepsArbitraryIssuesOutOfTheMessage(NonEmptyArray<NonEmptyString> generated) {
        var issues = ImmutableArray.CreateRange(generated.Get.Select(issue => $"secret:{issue.Get}"));
        var validation = Validation<int, string>.InvalidMany(issues);

        var extracted = validation.ShouldBeInvalid(includePayload: false);
        var failure = Should.Throw<FailException>(() => validation.ShouldBeValid(includePayload: false)).ShouldBeOfType<FailException>();

        (extracted == issues).ShouldBeTrue();
        failure.Message.ShouldBe("Expected Validation.Valid; actual Validation.Invalid.");
    }

    private static Func<object?> Act(Assertion assertion, State state, Form form, Probe payload, Probe other) => (assertion, form) switch {
        (Assertion.ShouldBeOk, Form.WithoutFlag) => () => ResultIn(state, payload, other).ShouldBeOk(),
        (Assertion.ShouldBeOk, _) => () => ResultIn(state, payload, other).ShouldBeOk(form == Form.IncludePayload),
        (Assertion.ShouldBeError, Form.WithoutFlag) => () => ResultIn(state, other, payload).ShouldBeError(),
        (Assertion.ShouldBeError, _) => () => ResultIn(state, other, payload).ShouldBeError(form == Form.IncludePayload),
        (Assertion.ShouldBeValid, Form.WithoutFlag) => () => ValidationIn(state, payload, other).ShouldBeValid(),
        (Assertion.ShouldBeValid, _) => () => ValidationIn(state, payload, other).ShouldBeValid(form == Form.IncludePayload),
        (_, Form.WithoutFlag) => () => ValidationIn(state, other, payload).ShouldBeInvalid().ShouldHaveSingleItem(),
        _ => () => ValidationIn(state, other, payload).ShouldBeInvalid(form == Form.IncludePayload).ShouldHaveSingleItem(),
    };

    private static string ActualBranch(Assertion assertion) => assertion switch {
        Assertion.ShouldBeOk => "Error",
        Assertion.ShouldBeError => "Ok",
        Assertion.ShouldBeValid => "Invalid",
        _ => "Valid",
    };

    private static Result<Probe, Probe> ResultIn(State state, Probe ok, Probe error) => state switch {
        State.First => Result<Probe, Probe>.Ok(ok),
        State.Second => Result<Probe, Probe>.Error(error),
        _ => default,
    };

    private static Validation<Probe, Probe> ValidationIn(State state, Probe valid, Probe issue) => state switch {
        State.First => Validation<Probe, Probe>.Valid(valid),
        State.Second => Validation<Probe, Probe>.Invalid(issue),
        _ => default,
    };
}
