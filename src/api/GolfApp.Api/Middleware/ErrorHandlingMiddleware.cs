using System.Net;
using System.Text.Json;
using GolfApp.Api.Exceptions;
using GolfApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Middleware;

/// <summary>
/// Middleware that catches unhandled exceptions and returns a consistent error response
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;
    
    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred");
        
        var statusCode = GetStatusCode(exception);
        var response = CreateErrorResponse(context, exception, statusCode);
        
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;
        
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
      private static int GetStatusCode(Exception exception) => exception switch
    {
        ArgumentException or InvalidOperationException => (int)HttpStatusCode.BadRequest,
        UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
        KeyNotFoundException => (int)HttpStatusCode.NotFound,
        ValidationException => (int)HttpStatusCode.BadRequest,
        ResourceNotFoundException => (int)HttpStatusCode.NotFound,
        ResourceConflictException => (int)HttpStatusCode.Conflict,
        InsufficientPermissionsException => (int)HttpStatusCode.Forbidden,
        RateLimitExceededException => (int)HttpStatusCode.TooManyRequests,
        // Add additional custom exceptions and their status codes here
        _ => (int)HttpStatusCode.InternalServerError
    };
    
    private ApiErrorResponse CreateErrorResponse(HttpContext context, Exception exception, int statusCode)
    {
        // Get the correlation ID from the request if available
        context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId);
        
        var errorCode = GetErrorCode(exception);
        var isTransient = IsTransientError(statusCode);
        
        var response = new ApiErrorResponse
        {
            Status = statusCode,
            Title = GetErrorTitle(statusCode),
            Detail = _environment.IsDevelopment() 
                ? exception.ToString() 
                : exception.Message,
            Type = $"https://golfapp.com/errors/{errorCode.ToLowerInvariant()}",
            Instance = context.Request.Path,
            ErrorCode = errorCode,
            CorrelationId = correlationId.FirstOrDefault() ?? Guid.NewGuid().ToString(),
            IsTransient = isTransient
        };
        
        // Add additional details in development environment only
        if (_environment.IsDevelopment())
        {
            response.Details = new
            {
                StackTrace = exception.StackTrace,
                Source = exception.Source,
                InnerException = exception.InnerException?.Message
            };
        }
        
        return response;
    }
      private static string GetErrorCode(Exception exception) => exception switch
    {
        ArgumentException => "InvalidArgument",
        InvalidOperationException => "InvalidOperation",
        UnauthorizedAccessException => "Unauthorized",
        KeyNotFoundException => "ResourceNotFound",
        DomainException domainEx => domainEx.ErrorCode,
        // Add additional custom exceptions and their error codes here
        _ => "ServerError"
    };
      private static string GetErrorTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Resource Not Found",
        409 => "Resource Conflict",
        429 => "Too Many Requests",
        500 => "Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        // Add additional status codes and their titles here
        _ => "An error occurred"
    };
    
    private static bool IsTransientError(int statusCode) => statusCode switch
    {
        429 => true, // Too Many Requests
        500 => true, // Internal Server Error
        502 => true, // Bad Gateway
        503 => true, // Service Unavailable
        504 => true, // Gateway Timeout
        _ => false
    };
}

// Extension method to register the middleware
public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
