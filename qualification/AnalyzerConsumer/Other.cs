namespace Other;

public readonly struct Result<T, TError>
{
    public static Result<T, TError> Make() => default;
}
