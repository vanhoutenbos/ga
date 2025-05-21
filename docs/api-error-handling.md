# API Error Handling Guide

This document describes the standard error format and error codes used throughout the Golf Tournament Organizer API.

## Standard Error Response Format

All API errors are returned using a consistent format based on [RFC 7807 - Problem Details for HTTP APIs](https://datatracker.ietf.org/doc/html/rfc7807). This provides a machine-readable format for error responses that includes enough detail for both API clients and human debuggers.

### Error Response Structure

```json
{
  "type": "https://api.golfapp.com/errors/error-type",
  "title": "A short, human-readable summary of the problem",
  "status": 400,
  "detail": "A human-readable explanation specific to this occurrence of the problem",
  "instance": "https://api.golfapp.com/tournaments/123/scores",
  "correlationId": "00-0123456789abcdef0123456789abcdef-0123456789abcdef-00",
  "errors": {
    // Optional additional error details, such as validation errors
  }
}
```

### Field Explanations

- **type**: A URI reference that identifies the problem type
- **title**: A short, human-readable summary of the problem
- **status**: The HTTP status code
- **detail**: A detailed explanation specific to this error instance
- **instance**: The specific API endpoint that generated the error
- **correlationId**: A unique identifier for tracing this request through logs
- **errors**: Additional error details where appropriate

## Common HTTP Status Codes

| Status Code | Description | Common Scenarios |
|-------------|-------------|-----------------|
| 400 | Bad Request | Invalid input, missing fields, malformed request |
| 401 | Unauthorized | Missing or invalid authentication token |
| 403 | Forbidden | Authenticated but insufficient permissions |
| 404 | Not Found | Resource does not exist |
| 409 | Conflict | Resource state conflicts with request (e.g., concurrent modification) |
| 422 | Unprocessable Entity | Validation errors (details in the `errors` field) |
| 429 | Too Many Requests | Client has exceeded rate limits |
| 500 | Internal Server Error | Unexpected server error |
| 503 | Service Unavailable | Service temporarily unavailable (e.g., maintenance) |

## Error Types and Codes

The API uses specific error types to help clients understand and handle errors appropriately.

### General Errors

| Error Type | Status Code | Description |
|------------|-------------|-------------|
| https://api.golfapp.com/errors/general | 500 | A general server error occurred |
| https://api.golfapp.com/errors/bad-request | 400 | The request was malformed or invalid |
| https://api.golfapp.com/errors/unauthorized | 401 | Authentication is required or has failed |
| https://api.golfapp.com/errors/forbidden | 403 | The client does not have permission |
| https://api.golfapp.com/errors/not-found | 404 | The requested resource was not found |
| https://api.golfapp.com/errors/validation | 422 | Request failed validation |
| https://api.golfapp.com/errors/rate-limited | 429 | Request was rate limited |

### Domain-Specific Errors

| Error Type | Status Code | Description |
|------------|-------------|-------------|
| https://api.golfapp.com/errors/tournament/not-found | 404 | Tournament not found |
| https://api.golfapp.com/errors/tournament/registration-closed | 409 | Tournament registration is closed |
| https://api.golfapp.com/errors/tournament/player-limit-reached | 409 | Tournament player limit has been reached |
| https://api.golfapp.com/errors/score/invalid | 422 | Score is invalid for the hole |
| https://api.golfapp.com/errors/score/already-submitted | 409 | Score has already been submitted for this hole |
| https://api.golfapp.com/errors/user/duplicate-email | 409 | User with this email already exists |

## Rate Limiting

The API implements rate limiting to protect the service. Rate limits apply differently based on the endpoint and the user's role.

### Rate Limit Headers

When a request is rate limited, the API returns a 429 Too Many Requests status code along with the following headers:

- `Retry-After`: Seconds until the client can try again
- `X-Rate-Limit-Limit`: Number of requests allowed in the period
- `X-Rate-Limit-Remaining`: Number of requests remaining in the period
- `X-Rate-Limit-Reset`: Unix timestamp when the limit resets

### Error Response Example

```json
{
  "type": "https://api.golfapp.com/errors/rate-limited",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "You have exceeded the rate limit of 60 requests per minute",
  "instance": "https://api.golfapp.com/tournaments/123/scores",
  "correlationId": "00-0123456789abcdef0123456789abcdef-0123456789abcdef-00",
  "errors": {
    "retryAfter": 45,
    "limit": 60,
    "period": "60 seconds"
  }
}
```

## Client Implementation Guidelines

### Error Handling Best Practices

1. **Parse the error response**: Always parse the full error response to get detailed information.
2. **Check the status code first**: Use the HTTP status code for high-level error categorization.
3. **Use the error type for detailed handling**: The `type` field provides more specific error information.
4. **Display detailed error messages**: Use the `detail` field for user-friendly error messages.
5. **Log the correlation ID**: Always log the `correlationId` for troubleshooting.
6. **Implement retries for specific errors**: Implement retries with backoff for 429 and 5xx errors.

### Example Error Handling (TypeScript)

```typescript
async function fetchTournament(id: string) {
  try {
    const response = await fetch(`https://api.golfapp.com/tournaments/${id}`);
    
    if (!response.ok) {
      const errorData = await response.json();
      
      // Log the error with correlation ID for troubleshooting
      console.error(`API Error (${errorData.correlationId}):`, errorData);
      
      // Handle specific error types
      switch (errorData.type) {
        case 'https://api.golfapp.com/errors/not-found':
          throw new Error(`Tournament not found: ${id}`);
          
        case 'https://api.golfapp.com/errors/unauthorized':
          // Redirect to login
          redirectToLogin();
          break;
          
        case 'https://api.golfapp.com/errors/rate-limited':
          // Implement retry with exponential backoff
          const retryAfter = errorData.errors?.retryAfter || 60;
          await new Promise(resolve => setTimeout(resolve, retryAfter * 1000));
          return fetchTournament(id);
          
        default:
          throw new Error(errorData.detail || 'An error occurred');
      }
    }
    
    return await response.json();
  } catch (error) {
    // Handle network errors or other exceptions
    console.error('Error fetching tournament:', error);
    throw error;
  }
}
```

## Contact Support

If you encounter persistent errors or need assistance with API integration, please contact our API support team:

- Email: api-support@golfapp.com
- Include the `correlationId` from the error response to help us investigate
