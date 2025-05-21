// This file defines consistent error handling for the Golf Tournament API
// Error response will use ProblemDetails from RFC 7807 (https://datatracker.ietf.org/doc/html/rfc7807)

using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Errors
{
    /// <summary>
    /// RFC 7807 compliant Problem Details for HTTP APIs
    /// </summary>
    public class ApiProblemDetails
    {
        /// <summary>
        /// A URI reference that identifies the problem type
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "https://api.golfapp.com/errors/general";

        /// <summary>
        /// A short, human-readable summary of the problem type
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// The HTTP status code
        /// </summary>
        [JsonPropertyName("status")]
        public int Status { get; set; }

        /// <summary>
        /// A human-readable explanation specific to this occurrence of the problem
        /// </summary>
        [JsonPropertyName("detail")]
        public string Detail { get; set; }

        /// <summary>
        /// A URI reference that identifies the specific occurrence of the problem
        /// </summary>
        [JsonPropertyName("instance")]
        public string Instance { get; set; }

        /// <summary>
        /// Request correlation ID for tracing errors across systems
        /// </summary>
        [JsonPropertyName("correlationId")]
        public string CorrelationId { get; set; }

        /// <summary>
        /// Additional properties relevant to the error
        /// </summary>
        [JsonPropertyName("errors")]
        public object Errors { get; set; }
    }

    /// <summary>
    /// Exception middleware for Azure Functions to standardize error responses
    /// </summary>
    public class ApiExceptionMiddleware
    {
        private readonly ILogger<ApiExceptionMiddleware> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiExceptionMiddleware(ILogger<ApiExceptionMiddleware> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task InvokeAsync(FunctionContext context, Func<Task> next)
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in function execution");
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(FunctionContext context, Exception exception)
        {
            var httpReqData = await context.GetHttpRequestDataAsync();
            var httpResponseData = httpReqData.CreateResponse(HttpStatusCode.InternalServerError);

            var problemDetails = new ApiProblemDetails
            {
                Title = "An unexpected error occurred",
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = exception.Message,
                Instance = httpReqData.Url.ToString(),
                CorrelationId = context.TraceContext.TraceParent
            };

            // Customize based on exception type
            switch (exception)
            {
                case BadHttpRequestException badRequestEx:
                    problemDetails.Status = (int)HttpStatusCode.BadRequest;
                    problemDetails.Title = "Bad Request";
                    problemDetails.Type = "https://api.golfapp.com/errors/bad-request";
                    break;
                case UnauthorizedAccessException unauthorizedEx:
                    problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                    problemDetails.Title = "Unauthorized";
                    problemDetails.Type = "https://api.golfapp.com/errors/unauthorized";
                    break;
                // Add additional exception types as needed
            }

            httpResponseData.StatusCode = (HttpStatusCode)problemDetails.Status;
            httpResponseData.Headers.Add("Content-Type", "application/problem+json");
            
            await httpResponseData.WriteAsJsonAsync(problemDetails, _jsonOptions);
            
            // Set the response on the context
            var invocationResult = context.GetInvocationResult();
            invocationResult.Value = httpResponseData;
        }
    }

    /// <summary>
    /// Custom exceptions for the API
    /// </summary>
    public class ApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string Type { get; }
        public object Errors { get; }

        public ApiException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, string type = null, object errors = null)
            : base(message)
        {
            StatusCode = statusCode;
            Type = type ?? "https://api.golfapp.com/errors/api-error";
            Errors = errors;
        }
    }

    /// <summary>
    /// Validation exception for invalid inputs
    /// </summary>
    public class ValidationException : ApiException
    {
        public ValidationException(string message, object errors = null)
            : base(message, HttpStatusCode.BadRequest, "https://api.golfapp.com/errors/validation", errors)
        {
        }
    }

    /// <summary>
    /// Resource not found exception
    /// </summary>
    public class NotFoundException : ApiException
    {
        public NotFoundException(string message)
            : base(message, HttpStatusCode.NotFound, "https://api.golfapp.com/errors/not-found")
        {
        }
    }

    /// <summary>
    /// Extension method for registering the exception middleware
    /// </summary>
    public static class ApiExceptionMiddlewareExtensions
    {
        public static IFunctionsWorkerApplicationBuilder UseApiExceptionHandler(
            this IFunctionsWorkerApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiExceptionMiddleware>();
        }
    }
}
