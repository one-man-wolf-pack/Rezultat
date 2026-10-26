namespace Rezultat.Tests;

public sealed class ResultCompositionGuardTests {
    private static readonly Dictionary<string, Action<Result<int, string>>> _nullCallbackCalls = new() {
        ["Map"] = static result => result.Map<int>(null!),
        ["MapError"] = static result => result.MapError<int>(null!),
        ["MapError(state)"] = static result => result.MapError<int, string?>(null, null!),
        ["AndThen"] = static result => result.AndThen<int>(null!),
        ["Ensure(predicate)"] = static result => result.Ensure(null!, static _ => "e"),
        ["Ensure(errorFactory)"] = static result => result.Ensure(static _ => true, null!),
        ["Ensure(both)"] = static result => result.Ensure(null!, null!),
        ["OrElse"] = static result => result.OrElse<int>(null!),
        ["Zip"] = static result => result.Zip<int, int>(Result<int, string>.Ok(2), null!),
    };

    public static TheoryData<string, bool, string> NullCallbackCases() {
        var data = new TheoryData<string, bool, string>();
        foreach (bool success in new[] { true, false }) {
            data.Add("Map", success, "selector");
            data.Add("MapError", success, "selector");
            data.Add("MapError(state)", success, "selector");
            data.Add("AndThen", success, "next");
            data.Add("Ensure(predicate)", success, "predicate");
            data.Add("Ensure(errorFactory)", success, "errorFactory");
            data.Add("Ensure(both)", success, "predicate");
            data.Add("OrElse", success, "fallback");
            data.Add("Zip", success, "selector");
        }

        return data;
    }

    public static TheoryData<string> Operations() => new(_nullCallbackCalls.Keys);

    [Theory]
    [MemberData(nameof(NullCallbackCases))]
    public void Null_callback_on_initialized_receiver_is_rejected_on_either_branch(string operation, bool success, string parameter) {
        var receiver = success ? Result<int, string>.Ok(1) : Result<int, string>.Error("e");

        var exception = Should.Throw<ArgumentNullException>(() => _nullCallbackCalls[operation](receiver)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void Uninitialized_receiver_is_rejected_before_callback_validation(string operation) {
        Should.Throw<InvalidOperationException>(() => _nullCallbackCalls[operation](default)).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Uninitialized_receiver_is_rejected_before_any_callback_invocation() {
        int calls = 0;
        var receiver = default(Result<int, string>);

        Should.Throw<InvalidOperationException>(() => receiver.Map(_ => ++calls)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => receiver.MapError(_ => ++calls)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => receiver.MapError(0, (_, _) => ++calls)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => receiver.AndThen(_ => Result<int, string>.Ok(++calls))).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => receiver.Ensure(_ => ++calls > 0, _ => $"{++calls}")).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => receiver.OrElse(_ => Result<int, int>.Ok(++calls))).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => receiver.Zip(Result<int, string>.Ok(1), (_, _) => ++calls)).ShouldBeOfType<InvalidOperationException>();
        calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Zip_rejects_uninitialized_other_before_error_selection_and_selector_validation(bool receiverSuccess) {
        var receiver = receiverSuccess ? Result<int, string>.Ok(1) : Result<int, string>.Error("left");
        int calls = 0;

        Should.Throw<InvalidOperationException>(() => receiver.Zip(default(Result<int, string>), (_, _) => ++calls)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => receiver.Zip<int, int>(default, null!)).ShouldBeOfType<InvalidOperationException>();
        calls.ShouldBe(0);
    }
}
