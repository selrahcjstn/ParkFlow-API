using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Common;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public ErrorCode ErrorCode { get; private set; }

    private Result() { }

    // SUCCESS
    public static Result<T> Success(T data, string message = "Success")
    {
        return new Result<T>
        {
            IsSuccess = true,
            Data = data,
            Message = message,
            ErrorCode = ErrorCode.None
        };
    }

    // FAILURE
    public static Result<T> Failure(string message, ErrorCode errorCode = ErrorCode.None)
    {
        return new Result<T>
        {
            IsSuccess = false,
            Data = default,
            Message = message,
            ErrorCode = errorCode
        };
    }
}