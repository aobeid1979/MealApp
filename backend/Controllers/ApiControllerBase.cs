using MealApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace MealApp.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    [NonAction]
    public new OkObjectResult Ok(object? value)
    {
        return base.Ok(CreateSuccessResponse(value, StatusCodes.Status200OK));
    }

    [NonAction]
    public new CreatedAtActionResult CreatedAtAction(string? actionName, object? routeValues, object? value)
    {
        return base.CreatedAtAction(
            actionName,
            routeValues,
            CreateSuccessResponse(value, StatusCodes.Status201Created));
    }

    [NonAction]
    public new BadRequestObjectResult BadRequest(object? error)
    {
        return base.BadRequest(CreateErrorResponse(error, StatusCodes.Status400BadRequest));
    }

    [NonAction]
    public new NotFoundObjectResult NotFound(object? error)
    {
        return base.NotFound(CreateErrorResponse(error, StatusCodes.Status404NotFound));
    }

    [NonAction]
    public new UnauthorizedObjectResult Unauthorized()
    {
        return base.Unauthorized(CreateErrorResponse("Unauthorized.", StatusCodes.Status401Unauthorized));
    }

    private static ApiResponse CreateSuccessResponse(object? result, int statusCode)
    {
        return new ApiResponse
        {
            IsSuccess = true,
            Result = result,
            StatusCode = statusCode
        };
    }

    private static ApiResponse CreateErrorResponse(object? error, int statusCode)
    {
        return new ApiResponse
        {
            IsSuccess = false,
            ErrorMessages = ExtractErrorMessages(error),
            StatusCode = statusCode
        };
    }

    private static List<string> ExtractErrorMessages(object? error)
    {
        if (error is null)
        {
            return ["An error occurred."];
        }

        if (error is string message)
        {
            return [message];
        }

        var errorType = error.GetType();
        var messageProperty = errorType.GetProperty("Message");
        var errorsProperty = errorType.GetProperty("Errors");
        var messages = new List<string>();

        if (messageProperty?.GetValue(error) is string propertyMessage)
        {
            messages.Add(propertyMessage);
        }

        if (errorsProperty?.GetValue(error) is IEnumerable<string> errors)
        {
            messages.AddRange(errors);
        }

        return messages.Count > 0 ? messages : ["An error occurred."];
    }
}
