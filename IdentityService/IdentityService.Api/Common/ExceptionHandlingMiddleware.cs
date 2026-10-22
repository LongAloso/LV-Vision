using FluentValidation;
using System.Net;
using System.Text.Json;

namespace IdentityService.Api.Common;

public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException vex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var errors = vex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());

            var payload = new ApiError
            {
                TraceId = context.TraceIdentifier,
                Status = context.Response.StatusCode,
                Code = "validation_error",
                Message = "Validation failed",
                Errors = errors
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
        catch (KeyNotFoundException kex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "application/json";

            var payload = new ApiError
            {
                TraceId = context.TraceIdentifier,
                Status = context.Response.StatusCode,
                Code = "not_found",
                Message = kex.Message
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
        catch (InvalidOperationException ioex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";

            var payload = new ApiError
            {
                TraceId = context.TraceIdentifier,
                Status = context.Response.StatusCode,
                Code = "conflict",
                Message = ioex.Message
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
        catch (UnauthorizedAccessException uaex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            var payload = new ApiError
            {
                TraceId = context.TraceIdentifier,
                Status = context.Response.StatusCode,
                Code = "unauthorized",
                Message = uaex.Message
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var payload = new ApiError
            {
                TraceId = context.TraceIdentifier,
                Status = context.Response.StatusCode,
                Code = "internal_error",
                Message = "An unexpected error occurred"
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}