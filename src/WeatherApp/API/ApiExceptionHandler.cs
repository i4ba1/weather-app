using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using WeatherApp.Application.Common;

namespace WeatherApp.API;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }
        var status = exception switch
        {
            ValidationException or BadHttpRequestException => 400,
            LocationNotFoundException or ResourceNotFoundException => 404,
            WeatherServiceException { Failure: WeatherFailure.Configuration } => 503,
            WeatherServiceException { Failure: WeatherFailure.Timeout } => 504,
            WeatherServiceException => 502,
            _ => 500
        };
        if (status == 500) logger.LogError(exception, "Unexpected API error for {Path}", context.Request.Path);
        var errors = exception is ValidationException validation
            ? validation.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray())
            : null;
        await Results.Problem(statusCode: status,
            title: status switch { 400 => "Invalid request", 404 => "Resource not found", 502 or 503 or 504 => "Weather unavailable", _ => "Server error" },
            detail: status == 500 ? "An unexpected error occurred." : exception is BadHttpRequestException ? "The request body or parameters are invalid." : exception.Message,
            extensions: errors is null ? null : new Dictionary<string, object?> { ["errors"] = errors })
            .ExecuteAsync(context);
        return true;
    }
}
