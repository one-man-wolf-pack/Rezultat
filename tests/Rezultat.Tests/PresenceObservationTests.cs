namespace Rezultat.Tests;

public sealed class PresenceObservationTests
{
    public static TheoryData<string> States => ["missing", "null", "value"];

    [Theory]
    [MemberData(nameof(States))]
    public void Match_invokes_only_the_selected_handler_once(string state)
    {
        var presence = Create(state, "payload");
        var calls = new List<string>();

        var result = presence.Match(
            value => { calls.Add($"value:{value}"); return 1; },
            () => { calls.Add("null"); return 2; },
            () => { calls.Add("missing"); return 3; });

        var expected = state switch { "value" => ("value:payload", 1), "null" => ("null", 2), _ => ("missing", 3) };
        Assert.Equal([expected.Item1], calls);
        Assert.Equal(expected.Item2, result);
    }

    [Theory]
    [MemberData(nameof(States))]
    public void Match_rejects_every_null_handler_regardless_of_state(string state)
    {
        var presence = Create(state, "payload");
        var calls = 0;
        Func<string, int> onValue = _ => ++calls;
        Func<int> other = () => ++calls;

        Assert.Equal("onValue", Assert.Throws<ArgumentNullException>(() => presence.Match(null!, other, other)).ParamName);
        Assert.Equal("onNull", Assert.Throws<ArgumentNullException>(() => presence.Match(onValue, null!, other)).ParamName);
        Assert.Equal("onMissing", Assert.Throws<ArgumentNullException>(() => presence.Match(onValue, other, null!)).ParamName);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Match_passes_the_exact_payload_and_allows_null_result()
    {
        var payload = new ReferenceProbe(1);

        var observed = Presence<ReferenceProbe>.Value(payload).Match<ReferenceProbe?>(static value => value, static () => null, static () => null);
        var nullResult = Presence<int>.Missing.Match<string?>(static _ => "value", static () => "null", static () => null);

        Assert.Same(payload, observed);
        Assert.Null(nullResult);
    }

    [Fact]
    public void Match_handler_exception_propagates_unchanged()
    {
        var thrown = new OperationCanceledException();

        var observed = Assert.Throws<OperationCanceledException>(
            () => Presence<int>.Null.Match<int>(static _ => 0, () => throw thrown, static () => 0));

        Assert.Same(thrown, observed);
    }

    [Fact]
    public void TryGetValue_passes_the_exact_active_payload()
    {
        var payload = new ReferenceProbe(1);

        Assert.True(Presence<ReferenceProbe>.Value(payload).TryGetValue(out var value));
        Assert.Same(payload, value);
        Assert.Equal((0, 0, 0), (payload.EqualsCalls, payload.HashCalls, payload.ToStringCalls));
    }

    [Fact]
    public void Missing_and_null_states_are_distinguished_by_flags_not_by_TryGetValue()
    {
        Presence<int>[] inactive = [Presence<int>.Missing, Presence<int>.Null];

        Assert.All(inactive, presence => Assert.False(presence.TryGetValue(out _)));
        Assert.Equal([true, false], inactive.Select(static presence => presence.IsMissing));
        Assert.Equal([false, true], inactive.Select(static presence => presence.IsNull));
    }

    private static Presence<string> Create(string state, string payload) => state switch
    {
        "value" => Presence<string>.Value(payload),
        "null" => Presence<string>.Null,
        _ => Presence<string>.Missing,
    };
}
