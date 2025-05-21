namespace GolfApp.Api.Exceptions;

/// <summary>
/// Base exception for domain-specific errors
/// </summary>
public abstract class DomainException : Exception
{
    public string ErrorCode { get; }

    protected DomainException(string message, string errorCode) 
        : base(message)
    {
        ErrorCode = errorCode;
    }
    
    protected DomainException(string message, string errorCode, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Thrown when validation of an entity or value object fails
/// </summary>
public class ValidationException : DomainException
{
    public ValidationException(string message) 
        : base(message, "Validation.Failed")
    {
    }
    
    public ValidationException(string message, Exception innerException) 
        : base(message, "Validation.Failed", innerException)
    {
    }
}

/// <summary>
/// Thrown when a requested resource was not found
/// </summary>
public class ResourceNotFoundException : DomainException
{
    public ResourceNotFoundException(string resourceType, string identifier) 
        : base($"{resourceType} with identifier {identifier} was not found.", "Resource.NotFound")
    {
    }
}

/// <summary>
/// Thrown when a conflict occurs during data manipulation
/// </summary>
public class ResourceConflictException : DomainException
{
    public ResourceConflictException(string message) 
        : base(message, "Resource.Conflict")
    {
    }
}

/// <summary>
/// Thrown when the user doesn't have sufficient permissions to access a resource
/// </summary>
public class InsufficientPermissionsException : DomainException
{
    public InsufficientPermissionsException(string message) 
        : base(message, "Permission.Denied")
    {
    }
}

/// <summary>
/// Thrown when too many requests are made in a given time period (rate limiting)
/// </summary>
public class RateLimitExceededException : DomainException
{
    public RateLimitExceededException(string message) 
        : base(message, "RateLimit.Exceeded")
    {
    }
    
    public RateLimitExceededException() 
        : base("Rate limit exceeded. Please try again later.", "RateLimit.Exceeded")
    {
    }
}
