namespace Rezultat.Tests;

public sealed class PresenceObservationTests {
    public static TheoryData<string> States => ["missing", "null", "value"];

    [Theory]
    [MemberData(nameof(States))]
    public void Match_invokes_only_the_selected_handler_once(string state) {
        var presence = Create(state, "payload");
        var calls = new List<string>();

        var result = presence.Match(
            value => { calls.Add($"value:{value}"); return 1; },
            () => { calls.Add("null"); return 2; },
            () => { calls.Add("missing"); return 3; });

        var expected = state switch { "value" => ("value:payload", 1), "null" => ("null", 2), _ => ("missing", 3) };
        calls.ShouldBe([expected.Item1]);
        result.ShouldBe(expected.Item2);
    }

    [Theory]
    [MemberData(nameof(States))]
    public void Match_rejects_every_null_handler_regardless_of_state(string state) {
        var presence = Create(state, "payload");
        var calls = 0;
        Func<string, int> onValue = _ => ++calls;
        Func<int> other = () => ++calls;

        Should.Throw<ArgumentNullException>(() => presence.Match(null!, other, other)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("onValue");
        Should.Throw<ArgumentNullException>(() => presence.Match(onValue, null!, other)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("onNull");
        Should.Throw<ArgumentNullException>(() => presence.Match(onValue, other, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("onMissing");
        calls.ShouldBe(0);
    }

    [Fact]
    public void Match_passes_the_exact_payload_and_allows_null_result() {
        var payload = new ReferenceProbe(1);

        var observed = Presence<ReferenceProbe>.Value(payload).Match<ReferenceProbe?>(static value => value, static () => null, static () => null);
        var nullResult = Presence<int>.Missing.Match<string?>(static _ => "value", static () => "null", static () => null);

        observed.ShouldBeSameAs(payload);
        nullResult.ShouldBeNull();
    }

    [Fact]
    public void Match_handler_exception_propagates_unchanged() {
        var thrown = new OperationCanceledException();

        var observed = Should.Throw<OperationCanceledException>(() => Presence<int>.Null.Match<int>(static _ => 0, () => throw thrown, static () => 0)).ShouldBeOfType<OperationCanceledException>();

        observed.ShouldBeSameAs(thrown);
    }

    [Fact]
    public void TryGetValue_passes_the_exact_active_payload() {
        var payload = new ReferenceProbe(1);

        Presence<ReferenceProbe>.Value(payload).TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        (payload.EqualsCalls, payload.HashCalls, payload.ToStringCalls).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void Missing_and_null_states_are_distinguished_by_flags_not_by_TryGetValue() {
        Presence<int>[] inactive = [Presence<int>.Missing, Presence<int>.Null];

        foreach (var presence in inactive) {
            presence.TryGetValue(out _).ShouldBeFalse();
        }
        inactive.Select(static presence => presence.IsMissing).ShouldBe([true, false]);
        inactive.Select(static presence => presence.IsNull).ShouldBe([false, true]);
    }

    private static Presence<string> Create(string state, string payload) => state switch {
        "value" => Presence<string>.Value(payload),
        "null" => Presence<string>.Null,
        _ => Presence<string>.Missing,
    };
}
