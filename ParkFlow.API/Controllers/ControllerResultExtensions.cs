using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;

namespace ParkFlow.API.Controllers;

public static class ControllerResultExtensions
{
    public static ActionResult<Result<TResponse>> ToActionResult<TResponse>(this ControllerBase controller, Result<TResponse> result)
    {
        if (result.IsSuccess)
            return controller.Ok(result);

        var statusCode = result.ErrorCode == ErrorCode.None ? 400 : (int)result.ErrorCode;
        return controller.StatusCode(statusCode, result);
    }
}