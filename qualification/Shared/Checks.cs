internal sealed class Checks
{
    private int _failures;
    private int _passed;

    public void That(string name, bool condition)
    {
        if (condition)
        {
            _passed++;
        }
        else
        {
            Fail(name);
        }
    }

    public void Text(string name, string expected, string actual) =>
        That($"{name}: expected '{expected}', got '{actual}'", expected == actual);

    public void Throws<TException>(string name, Action action)
        where TException : Exception
    {
        try
        {
            action();
            Fail($"{name}: no exception");
        }
        catch (Exception exception) when (exception.GetType() == typeof(TException))
        {
            _passed++;
        }
    }

    public void Fail(string name)
    {
        _failures++;
        Console.Error.WriteLine($"FAIL {name}");
    }

    public int Complete()
    {
        Console.WriteLine($"{AppContext.GetData("RUNTIME_IDENTIFIER") ?? "unknown-rid"}: {_passed} passed, {_failures} failed");
        return _failures == 0 ? 0 : 1;
    }
}
