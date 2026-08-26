namespace SaviaUp.Admin.Domain.Common;

public sealed record Error(string Code, string Message, int StatusCode, object? Details = null);

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public Error? Error { get; }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, Error? error) : base(isSuccess, error) => Value = value;

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public new static Result<T> Failure(Error error) => new(false, default, error);
}
