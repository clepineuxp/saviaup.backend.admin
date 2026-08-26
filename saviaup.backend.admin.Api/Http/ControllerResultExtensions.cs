using Microsoft.AspNetCore.Mvc;
using SaviaUp.Admin.Domain.Common;

namespace SaviaUp.Admin.Api.Http;

internal static class ControllerResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(this ControllerBase controller, Result<T> result)
        => result.IsSuccess ? controller.Ok(result.Value) : controller.Error(result.Error!);

    public static ObjectResult Error(this ControllerBase controller, Error error)
        => controller.StatusCode(error.StatusCode, ErrorEnvelope.From(error));
}

internal sealed record ErrorEnvelope(bool Success, ErrorBody Error)
{
    public static ErrorEnvelope From(Error error) => new(false, new ErrorBody(error.Code, error.Message, error.Details));
}

internal sealed record ErrorBody(string Code, string Message, object? Details);
