using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Rezultat;
using Shouldly;
using Xunit;

public sealed class RecipeWitness {
    private const string Secret = "secret-sentinel";

    [Fact]
    public void SuccessReturnsExactPayloadsWithoutTouchingThem() {
        var payload = new Probe();
        var other = new Probe();

        Assert.Same(payload, Result<Probe, Probe>.Ok(payload).ShouldBeOk());
        Assert.Same(payload, Result<Probe, Probe>.Error(payload).ShouldBeError());
        Assert.Same(payload, Validation<Probe, Probe>.Valid(payload).ShouldBeValid());
        Assert.Same(payload, Assert.Single(Validation<Probe, Probe>.Invalid(payload).ShouldBeInvalid()));
        Assert.Equal(0, payload.Calls + other.Calls);
    }

    [Fact]
    public void SuccessKeepsNullablePayloads() {
        string? ok = Result<string?, string>.Ok(null).ShouldBeOk();
        string? error = Result<int, string?>.Error(null).ShouldBeError();
        int? valid = Validation<int?, string>.Valid(null).ShouldBeValid();
        ImmutableArray<string?> issues = Validation<int, string?>.Invalid(null).ShouldBeInvalid();

        Assert.Null(ok);
        Assert.Null(error);
        Assert.Null(valid);
        Assert.Null(Assert.Single(issues));
    }

    [Fact]
    public void ShouldBeInvalidReturnsTheStoredIssueBuffer() {
        var stored = ImmutableArray.Create<string?>("b", null, "b");

        var extracted = Validation<int, string?>.InvalidMany(stored).ShouldBeInvalid();

        Assert.Same(ImmutableCollectionsMarshal.AsArray(stored), ImmutableCollectionsMarshal.AsArray(extracted));
    }

    [Fact]
    public void WrongBranchesThrowShouldAssertExceptionWithoutFormatting() {
        var payload = new Probe(Secret) { Throws = true };
        var second = new Probe(Secret) { Throws = true };
        Action[] wrong =
        [
            () => Result<int, Probe>.Error(payload).ShouldBeOk(),
            () => Result<Probe, int>.Ok(payload).ShouldBeError(),
            () => Validation<int, Probe>.InvalidMany([payload, second]).ShouldBeValid(),
            () => Validation<Probe, int>.Valid(payload).ShouldBeInvalid(),
        ];

        foreach (var action in wrong) {
            var failure = Assert.Throws<ShouldAssertException>(action);
            Assert.DoesNotContain(Secret, failure.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(failure.Message, char.IsDigit);
            Assert.Null(failure.InnerException);
        }

        Assert.Equal(0, payload.Calls + second.Calls);
    }

    [Fact]
    public void UninitializedKeepsTheNativeGuard() {
        Action[] uninitialized =
        [
            () => default(Result<Probe, Probe>).ShouldBeOk(),
            () => default(Result<Probe, Probe>).ShouldBeError(),
            () => default(Validation<Probe, Probe>).ShouldBeValid(),
            () => default(Validation<Probe, Probe>).ShouldBeInvalid(),
        ];

        foreach (var action in uninitialized) {
            Assert.IsType<InvalidOperationException>(Record.Exception(action));
        }
    }

    [Fact]
    public void RecipeDoesNotNeedTheXunitAssertionsPackage() =>
        Assert.Null(Type.GetType("Rezultat.Testing.Xunit.ResultAssertions, Rezultat.Testing.Xunit"));

    private sealed class Probe(string text = "probe") {
        public int Calls { get; private set; }

        public bool Throws { get; init; }

        public override string ToString() {
            Calls++;
            return Throws ? throw new InvalidOperationException(text) : text;
        }

        public override bool Equals(object? obj) {
            Calls++;
            return ReferenceEquals(this, obj);
        }

        public override int GetHashCode() {
            Calls++;
            return 0;
        }
    }
}
