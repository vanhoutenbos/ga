using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace GolfApp.Api.Models;

/// <summary>
/// Standardized error response format based on RFC 7807 (Problem Details)
/// </summary>
public class ApiErrorResponse : ProblemDetails
{
    /// <summary>
    /// One of the defined error codes for the application
    /// </summary>
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Request correlation ID for tracing
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Additional data that may help with debugging or providing context
    /// </summary>
    [JsonPropertyName("details")]
    public object? Details { get; set; }
    
    /// <summary>
    /// Whether the error is transient (can be retried)
    /// </summary>
    [JsonPropertyName("isTransient")]
    public bool IsTransient { get; set; }
}
