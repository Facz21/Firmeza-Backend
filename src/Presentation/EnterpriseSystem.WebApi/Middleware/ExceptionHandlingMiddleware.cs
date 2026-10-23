using System.Net;
using System.Text.Json;
using EnterpriseSystem.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseSystem.WebApi.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate _next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        this._next = _next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred during HTTP request execution: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, problemDetails) = exception switch
        {
            ValidationException valEx => (
                HttpStatusCode.BadRequest,
                new ValidationProblemDetails(valEx.Errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation Failure",
                    Detail = valEx.Message,
                    Instance = context.Request.Path
                } as ProblemDetails
            ),

            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Resource Not Found",
                    Detail = notFoundEx.Message,
                    Instance = context.Request.Path
                }
            ),

            UnauthorizedException unauthEx => (
                HttpStatusCode.Unauthorized,
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = unauthEx.Message,
                    Instance = context.Request.Path
                }
            ),

            ForbiddenAccessException forbiddenEx => (
                HttpStatusCode.Forbidden,
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden Access",
                    Detail = forbiddenEx.Message,
                    Instance = context.Request.Path
                }
            ),

            UnauthorizedAccessException unauthorizedAccess => (
                HttpStatusCode.Unauthorized,
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = unauthorizedAccess.Message,
                    Instance = context.Request.Path
                }
            ),

            _ => (
                HttpStatusCode.InternalServerError,
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred while processing your request.",
                    Instance = context.Request.Path
                }
            )
        };

        context.Response.StatusCode = (int)statusCode;
        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
