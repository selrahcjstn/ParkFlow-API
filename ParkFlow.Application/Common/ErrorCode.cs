namespace ParkFlow.Application.Common;

public enum ErrorCode
{
    None = 0,

    BadRequest = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    RequestTimeout = 408,
    Conflict = 409,
    Gone = 410,
    PayloadTooLarge = 413,
    UnsupportedMediaType = 415,
    UnprocessableEntity = 422,
    TooManyRequests = 429,

    ServerError = 500,
    BadGateway = 502,
    ServiceUnavailable = 503,
    GatewayTimeout = 504
}
