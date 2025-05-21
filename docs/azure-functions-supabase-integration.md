# Azure Functions Integration with Supabase

This document outlines the integration approach between Azure Functions (.NET 8) and Supabase in the Golf Tournament Organizer application. It covers authentication, data access, caching, and best practices for this hybrid architecture.

## Overview

The Golf Tournament Organizer application uses Azure Functions (.NET 8) for complex business logic while leveraging Supabase for authentication, database, storage, and real-time features. This integration requires careful design to ensure security, performance, and maintainability.

## Architecture

```
                         ┌─────────────────────┐
                         │                     │
                         │  React Frontend     │◄────────┐
                         │                     │         │
                         └─────────┬───────────┘         │
                                   │                     │
                                   │                     │
                                   ▼                     │
                         ┌─────────────────────┐         │
                         │                     │         │
                         │  Supabase Client    │         │
                         │                     │         │
                         └─────────┬───────────┘         │
                                   │                     │
                                   │                     │
           ┌───────────────────────┼───────────────────┐ │
           │                       │                   │ │
           ▼                       ▼                   ▼ │
┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐
│                     │  │                     │  │                     │
│  Supabase Auth      │  │  Supabase Database  │  │  Azure Functions    │
│                     │  │                     │  │  (.NET 8)           │
└─────────┬───────────┘  └─────────────────────┘  └─────────────────────┘
          │                                                ▲
          │                                                │
          │                                                │
          └────────────────────────────────────────────────┘
                           JWT Token Flow
```

## Authentication Integration

### JWT Validation in Azure Functions

Azure Functions validate Supabase JWTs to authenticate users:

```csharp
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using System.Text;

public class SupabaseAuthHandler
{
    private readonly SupabaseOptions _options;

    public SupabaseAuthHandler(IOptions<SupabaseOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(HttpRequestData req)
    {
        // Extract the JWT token from the Authorization header
        if (!req.Headers.TryGetValues("Authorization", out var authValues))
        {
            return null;
        }

        var authHeader = authValues.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return null;
        }

        var token = authHeader.Substring("Bearer ".Length);

        try
        {
            // Validate the JWT token using Supabase's JWT key
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.ASCII.GetBytes(_options.JwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = _options.JwtIssuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (Exception ex)
        {
            // Log token validation failure
            return null;
        }
    }
}
```

### Authentication Middleware

A middleware simplifies authentication across all functions:

```csharp
public class SupabaseAuthMiddleware : IFunctionsWorkerMiddleware
{
    private readonly SupabaseAuthHandler _authHandler;

    public SupabaseAuthMiddleware(SupabaseAuthHandler authHandler)
    {
        _authHandler = authHandler;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Check if this is an HTTP trigger
        var httpReqData = await context.GetHttpRequestDataAsync();
        if (httpReqData != null)
        {
            // Try to validate JWT token
            var user = await _authHandler.ValidateTokenAsync(httpReqData);
            
            // Store the user in the function context for later use
            if (user != null)
            {
                context.Items.Add("User", user);
            }
        }
        
        // Continue processing the request
        await next(context);
    }
}
```

### User Context Accessor

A service to access the authenticated user within a function:

```csharp
public interface IUserContext
{
    ClaimsPrincipal? User { get; }
    string? GetUserId();
    bool IsInRole(string role);
}

public class SupabaseUserContext : IUserContext
{
    private readonly FunctionContext _functionContext;

    public SupabaseUserContext(FunctionContext functionContext)
    {
        _functionContext = functionContext;
    }

    public ClaimsPrincipal? User => 
        _functionContext.Items.TryGetValue("User", out var user) ? user as ClaimsPrincipal : null;

    public string? GetUserId() => User?.FindFirst("sub")?.Value;

    public bool IsInRole(string role) => 
        User?.HasClaim(c => c.Type == "app_role" && c.Value == role) ?? false;
}
```

## Data Access Integration

### Supabase .NET SDK Integration

Azure Functions use the Supabase .NET SDK for database access:

```csharp
using Supabase;
using Microsoft.Extensions.Options;

public class SupabaseService : ISupabaseService
{
    private readonly Client _client;
    private readonly ILogger<SupabaseService> _logger;

    public SupabaseService(IOptions<SupabaseOptions> options, ILogger<SupabaseService> logger)
    {
        var supabaseOptions = options.Value;
        
        _client = new Client(
            supabaseOptions.Url,
            supabaseOptions.Key,
            new ClientOptions 
            { 
                AutoConnectRealtime = false 
            });
            
        _logger = logger;
    }

    public async Task<IEnumerable<T>> GetAsync<T>(string table, string filter = null)
    {
        try
        {
            var query = _client.From<T>(table);
            
            if (!string.IsNullOrEmpty(filter))
            {
                query = query.Filter(filter);
            }
            
            return await query.Get();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data from Supabase table {Table}", table);
            throw;
        }
    }

    public async Task<T> InsertAsync<T>(string table, T data)
    {
        try
        {
            return await _client.From<T>(table).Insert(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting data into Supabase table {Table}", table);
            throw;
        }
    }

    // Additional methods for update, delete, and other operations
}
```

### Repository Pattern Implementation

A repository layer abstracts Supabase data access:

```csharp
public interface ITournamentRepository
{
    Task<Tournament> GetByIdAsync(string id);
    Task<IEnumerable<Tournament>> GetByTenantAsync(string tenantId);
    Task<Tournament> CreateAsync(Tournament tournament);
    Task UpdateAsync(Tournament tournament);
    Task DeleteAsync(string id);
}

public class TournamentRepository : ITournamentRepository
{
    private readonly ISupabaseService _supabaseService;
    private readonly IUserContext _userContext;
    private readonly ILogger<TournamentRepository> _logger;
    private const string TableName = "tournaments";

    public TournamentRepository(
        ISupabaseService supabaseService,
        IUserContext userContext,
        ILogger<TournamentRepository> logger)
    {
        _supabaseService = supabaseService;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<Tournament> GetByIdAsync(string id)
    {
        var userId = _userContext.GetUserId();
        _logger.LogInformation("User {UserId} retrieving tournament {TournamentId}", userId, id);
        
        var filter = $"id=eq.{id}";
        var results = await _supabaseService.GetAsync<Tournament>(TableName, filter);
        
        return results.FirstOrDefault();
    }

    public async Task<IEnumerable<Tournament>> GetByTenantAsync(string tenantId)
    {
        var filter = $"tenant_id=eq.{tenantId}";
        return await _supabaseService.GetAsync<Tournament>(TableName, filter);
    }

    public async Task<Tournament> CreateAsync(Tournament tournament)
    {
        // Ensure the tournament has an ID
        if (string.IsNullOrEmpty(tournament.Id))
        {
            tournament.Id = Guid.NewGuid().ToString();
        }
        
        // Set audit fields
        tournament.CreatedBy = _userContext.GetUserId();
        tournament.CreatedAt = DateTime.UtcNow.ToString("o");
        tournament.UpdatedAt = tournament.CreatedAt;
        
        return await _supabaseService.InsertAsync(TableName, tournament);
    }

    public async Task UpdateAsync(Tournament tournament)
    {
        // Set update timestamp
        tournament.UpdatedAt = DateTime.UtcNow.ToString("o");
        
        var filter = $"id=eq.{tournament.Id}";
        await _supabaseService.UpdateAsync(TableName, tournament, filter);
    }

    public async Task DeleteAsync(string id)
    {
        var filter = $"id=eq.{id}";
        await _supabaseService.DeleteAsync<Tournament>(TableName, filter);
    }
}
```

## API Caching Strategy

### Response Caching with Redis

To optimize performance and reduce load on Supabase, we implement Redis caching:

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
}

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var data = await _cache.GetStringAsync(key);
            
            if (string.IsNullOrEmpty(data))
            {
                return default;
            }
            
            return JsonSerializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached item with key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
        };
        
        var data = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, data, options);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        var cached = await GetAsync<T>(key);
        
        if (cached != null)
        {
            return cached;
        }
        
        var value = await factory();
        
        await SetAsync(key, value, expiration);
        
        return value;
    }
}
```

### Cached Repository Implementation

Adding caching to our repositories:

```csharp
public class CachedTournamentRepository : ITournamentRepository
{
    private readonly TournamentRepository _innerRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedTournamentRepository> _logger;
    
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(10);
    private const string KeyPrefix = "tournament:";

    public CachedTournamentRepository(
        TournamentRepository innerRepository,
        ICacheService cacheService,
        ILogger<CachedTournamentRepository> logger)
    {
        _innerRepository = innerRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Tournament> GetByIdAsync(string id)
    {
        var cacheKey = $"{KeyPrefix}{id}";
        
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            () => _innerRepository.GetByIdAsync(id),
            CacheExpiration);
    }

    public async Task<IEnumerable<Tournament>> GetByTenantAsync(string tenantId)
    {
        var cacheKey = $"{KeyPrefix}tenant:{tenantId}";
        
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            () => _innerRepository.GetByTenantAsync(tenantId),
            CacheExpiration);
    }

    public async Task<Tournament> CreateAsync(Tournament tournament)
    {
        var result = await _innerRepository.CreateAsync(tournament);
        
        // Invalidate tenant cache
        await _cacheService.RemoveAsync($"{KeyPrefix}tenant:{tournament.TenantId}");
        
        // Cache the new tournament
        await _cacheService.SetAsync(
            $"{KeyPrefix}{result.Id}",
            result,
            CacheExpiration);
            
        return result;
    }

    public async Task UpdateAsync(Tournament tournament)
    {
        await _innerRepository.UpdateAsync(tournament);
        
        // Invalidate caches
        await _cacheService.RemoveAsync($"{KeyPrefix}{tournament.Id}");
        await _cacheService.RemoveAsync($"{KeyPrefix}tenant:{tournament.TenantId}");
    }

    public async Task DeleteAsync(string id)
    {
        // Get the tournament first to know the tenant ID
        var tournament = await GetByIdAsync(id);
        
        if (tournament != null)
        {
            await _innerRepository.DeleteAsync(id);
            
            // Invalidate caches
            await _cacheService.RemoveAsync($"{KeyPrefix}{id}");
            await _cacheService.RemoveAsync($"{KeyPrefix}tenant:{tournament.TenantId}");
        }
    }
}
```

## Connection Pooling and Performance

### HTTP Client Management

Using `IHttpClientFactory` for optimal connection management:

```csharp
public class SupabaseServiceWithPooling
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SupabaseOptions _options;
    private readonly ILogger<SupabaseServiceWithPooling> _logger;

    public SupabaseServiceWithPooling(
        IHttpClientFactory httpClientFactory,
        IOptions<SupabaseOptions> options,
        ILogger<SupabaseServiceWithPooling> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request)
    {
        // Use named client with pooling
        var httpClient = _httpClientFactory.CreateClient("Supabase");
        
        // Add authentication
        request.Headers.Add("apikey", _options.Key);
        
        return await httpClient.SendAsync(request);
    }
}
```

### Database Connection Pooling

Configuring optimal connection pooling:

```csharp
public static IServiceCollection AddSupabaseServices(this IServiceCollection services, IConfiguration configuration)
{
    // Register Supabase options
    services.Configure<SupabaseOptions>(configuration.GetSection("Supabase"));
    
    // Register HTTP client factory with connection pooling
    services.AddHttpClient("Supabase", client =>
    {
        client.BaseAddress = new Uri(configuration["Supabase:Url"]);
        client.DefaultRequestHeaders.Add("apikey", configuration["Supabase:Key"]);
        client.Timeout = TimeSpan.FromSeconds(30);
    }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        MaxConnectionsPerServer = 100,
        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
        EnableMultipleHttp2Connections = true
    });
    
    // Register Supabase services
    services.AddScoped<ISupabaseService, SupabaseService>();
    
    // Register repositories
    services.AddScoped<ITournamentRepository, CachedTournamentRepository>();
    services.AddScoped<TournamentRepository>();  // Inner repository
    
    // Add Redis cache
    services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = configuration.GetConnectionString("Redis");
        options.InstanceName = "GolfApp:";
    });
    services.AddSingleton<ICacheService, RedisCacheService>();
    
    return services;
}
```

## Real-Time Data Synchronization

### Webhook Integration

Azure Functions receive real-time updates from Supabase via webhooks:

```csharp
[Function("SupabaseWebhook")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
    FunctionContext executionContext)
{
    var logger = executionContext.GetLogger<SupabaseWebhookFunction>();
    logger.LogInformation("Processing Supabase webhook");
    
    // Get webhook payload
    var payload = await req.ReadFromJsonAsync<WebhookPayload>();
    
    // Verify webhook signature
    if (!IsValidWebhookSignature(req, _options.WebhookSecret))
    {
        logger.LogWarning("Invalid webhook signature");
        return req.CreateResponse(HttpStatusCode.Unauthorized);
    }
    
    // Process based on event type
    switch (payload.Type)
    {
        case "INSERT":
            await ProcessInsertEvent(payload);
            break;
        case "UPDATE":
            await ProcessUpdateEvent(payload);
            break;
        case "DELETE":
            await ProcessDeleteEvent(payload);
            break;
        default:
            logger.LogWarning("Unhandled webhook event type: {Type}", payload.Type);
            break;
    }
    
    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new { processed = true });
    return response;
}

private bool IsValidWebhookSignature(HttpRequestData req, string secret)
{
    if (!req.Headers.TryGetValues("X-Supabase-Signature", out var signatures))
    {
        return false;
    }
    
    var signature = signatures.FirstOrDefault();
    if (string.IsNullOrEmpty(signature))
    {
        return false;
    }
    
    // TODO: Implement HMAC verification of request body with webhook secret
    // This is a placeholder - actual implementation would validate the signature
    return true;
}
```

### Cache Invalidation

Using webhooks to keep caches in sync:

```csharp
private async Task ProcessUpdateEvent(WebhookPayload payload)
{
    if (payload.Table == "tournaments")
    {
        var tournamentId = payload.Record["id"].ToString();
        var tenantId = payload.Record["tenant_id"].ToString();
        
        // Invalidate relevant caches
        await _cacheService.RemoveAsync($"tournament:{tournamentId}");
        await _cacheService.RemoveAsync($"tournament:tenant:{tenantId}");
        
        _logger.LogInformation("Invalidated cache for tournament {TournamentId}", tournamentId);
    }
}
```

## Security Considerations

### Service Role Key Management

Securely managing Supabase service role keys:

```csharp
public class SupabaseKeyVaultOptions
{
    public string SupabaseServiceRoleKeyName { get; set; }
    public string SupabaseJwtSecretName { get; set; }
}

public class SecureSupabaseService
{
    private readonly Client _client;
    private readonly ILogger<SecureSupabaseService> _logger;

    public SecureSupabaseService(
        IKeyVaultService keyVaultService,
        IOptions<SupabaseOptions> options,
        IOptions<SupabaseKeyVaultOptions> keyVaultOptions,
        ILogger<SecureSupabaseService> logger)
    {
        var supabaseOptions = options.Value;
        
        // Retrieve service role key from Key Vault
        var serviceRoleKey = keyVaultService.GetSecretAsync(
            keyVaultOptions.Value.SupabaseServiceRoleKeyName).GetAwaiter().GetResult();
        
        _client = new Client(
            supabaseOptions.Url,
            serviceRoleKey,
            new ClientOptions { AutoConnectRealtime = false });
            
        _logger = logger;
    }
    
    // Service methods...
}
```

### Secure Configuration

Configuration set up for Azure Functions:

```json
{
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "Key": "public-anon-key-for-reference-only",
    "JwtIssuer": "supabase"
  },
  "SupabaseKeyVault": {
    "SupabaseServiceRoleKeyName": "supabase-service-role-key",
    "SupabaseJwtSecretName": "supabase-jwt-secret"
  },
  "KeyVault": {
    "VaultUri": "https://your-keyvault.vault.azure.net/"
  },
  "ConnectionStrings": {
    "Redis": "your-redis-connection-string"
  }
}
```

### Authorization within Azure Functions

Implementing role-based authorization:

```csharp
[Function("ManageTournament")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
    FunctionContext executionContext)
{
    var userContext = executionContext.InstanceServices.GetService<IUserContext>();
    
    // Check if user is authenticated
    if (userContext?.User == null)
    {
        return req.CreateUnauthorizedResponse("Authentication required");
    }
    
    // Check if user has the required role
    if (!userContext.IsInRole("admin") && !userContext.IsInRole("tournament_manager"))
    {
        return req.CreateForbiddenResponse("Insufficient permissions");
    }
    
    // Process the authenticated and authorized request
    var tournamentRequest = await req.ReadFromJsonAsync<TournamentRequest>();
    
    // Business logic here...
    
    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new { success = true });
    return response;
}

public static class HttpRequestDataExtensions
{
    public static HttpResponseData CreateUnauthorizedResponse(this HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.Unauthorized);
        response.Headers.Add("WWW-Authenticate", "Bearer");
        response.WriteString(message);
        return response;
    }
    
    public static HttpResponseData CreateForbiddenResponse(this HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.Forbidden);
        response.WriteString(message);
        return response;
    }
}
```

## Deployment and Configuration

### Configuration Pipeline

Setting up configuration for Azure Functions:

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        // Add authentication middleware
        builder.UseMiddleware<SupabaseAuthMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        // Add configuration
        services.Configure<SupabaseOptions>(context.Configuration.GetSection("Supabase"));
        services.Configure<SupabaseKeyVaultOptions>(
            context.Configuration.GetSection("SupabaseKeyVault"));
        
        // Add Key Vault integration
        services.AddAzureKeyVault(
            new Uri(context.Configuration["KeyVault:VaultUri"]),
            new DefaultAzureCredential());
            
        // Add services
        services.AddSingleton<IKeyVaultService, KeyVaultService>();
        services.AddScoped<SupabaseAuthHandler>();
        services.AddScoped<IUserContext, SupabaseUserContext>();
        
        // Add Supabase services
        services.AddSupabaseServices(context.Configuration);
    })
    .Build();
```

## Testing and Development

### Local Development Setup

```powershell
# Set up local development secrets
dotnet user-secrets init --project src/api/GolfApp.Functions
dotnet user-secrets set "Supabase:Url" "https://your-project.supabase.co" --project src/api/GolfApp.Functions
dotnet user-secrets set "Supabase:Key" "your-anon-key" --project src/api/GolfApp.Functions
dotnet user-secrets set "Supabase:ServiceRoleKey" "your-service-role-key" --project src/api/GolfApp.Functions
dotnet user-secrets set "Supabase:JwtSecret" "your-jwt-secret" --project src/api/GolfApp.Functions

# Run the function app locally
func start
```

### Testing Supabase Integration

Writing tests for the Supabase integration:

```csharp
[Fact]
public async Task TournamentRepository_GetById_ReturnsCorrectTournament()
{
    // Arrange
    var tournamentId = "test-tournament-id";
    var expectedTournament = new Tournament { Id = tournamentId, Name = "Test Tournament" };
    
    var supabaseServiceMock = new Mock<ISupabaseService>();
    supabaseServiceMock
        .Setup(s => s.GetAsync<Tournament>(
            It.IsAny<string>(), 
            It.Is<string>(filter => filter.Contains(tournamentId))))
        .ReturnsAsync(new[] { expectedTournament });
        
    var userContextMock = new Mock<IUserContext>();
    userContextMock
        .Setup(u => u.GetUserId())
        .Returns("test-user-id");
        
    var loggerMock = new Mock<ILogger<TournamentRepository>>();
    
    var repository = new TournamentRepository(
        supabaseServiceMock.Object, 
        userContextMock.Object,
        loggerMock.Object);
    
    // Act
    var result = await repository.GetByIdAsync(tournamentId);
    
    // Assert
    Assert.Equal(tournamentId, result.Id);
    Assert.Equal("Test Tournament", result.Name);
}
```

## Scaling and Performance

### Caching Strategy for Production

As user numbers grow, implement a multi-level caching strategy:

1. **In-Memory Cache**: For fastest access to frequently used items
2. **Distributed Redis Cache**: For shared cache across function instances
3. **CDN Caching**: For static resources and public data

```csharp
public class MultiLevelCache : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly RedisCacheService _distributedCache;
    
    public MultiLevelCache(IMemoryCache memoryCache, RedisCacheService distributedCache)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
    }
    
    public async Task<T?> GetAsync<T>(string key)
    {
        // Try memory cache first
        if (_memoryCache.TryGetValue(key, out T value))
        {
            return value;
        }
        
        // Try distributed cache
        var result = await _distributedCache.GetAsync<T>(key);
        
        // If found in distributed cache, add to memory cache
        if (result != null)
        {
            _memoryCache.Set(key, result, TimeSpan.FromMinutes(1));
        }
        
        return result;
    }
    
    // Other methods similarly implemented...
}
```

### Connection Optimization

For handling high concurrency:

```csharp
services.AddHttpClient("Supabase", client =>
{
    client.BaseAddress = new Uri(configuration["Supabase:Url"]);
    client.DefaultRequestHeaders.Add("apikey", configuration["Supabase:Key"]);
})
.SetHandlerLifetime(TimeSpan.FromMinutes(5))
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    MaxConnectionsPerServer = 200,
    EnableMultipleHttp2Connections = true,
    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
    KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
    KeepAlivePingDelay = TimeSpan.FromSeconds(30),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(15)
});
```

## Troubleshooting and Error Handling

### Error Handling Strategy

Common error handling for Supabase integration:

```csharp
public async Task<Tournament> GetByIdAsync(string id)
{
    try
    {
        var filter = $"id=eq.{id}";
        var results = await _supabaseService.GetAsync<Tournament>(TableName, filter);
        return results.FirstOrDefault();
    }
    catch (Exception ex) when (ex is HttpRequestException || ex.Message.Contains("Supabase"))
    {
        _logger.LogError(ex, "Supabase communication error when getting tournament {TournamentId}", id);
        throw new RepositoryException(
            $"Unable to retrieve tournament data: {ex.Message}",
            ex);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unknown error when getting tournament {TournamentId}", id);
        throw;
    }
}
```

### Custom Exception Types

```csharp
public class SupabaseException : Exception
{
    public string ErrorCode { get; }
    
    public SupabaseException(string message) : base(message)
    {
    }
    
    public SupabaseException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
    
    public SupabaseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class RepositoryException : Exception
{
    public RepositoryException(string message) : base(message)
    {
    }
    
    public RepositoryException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
```

## Conclusion

The integration between Azure Functions (.NET 8) and Supabase leverages the strengths of both platforms while maintaining security, performance, and reliability. This approach allows the Golf Tournament Organizer application to use complex business logic within the .NET ecosystem while taking advantage of Supabase's real-time database, authentication, and storage capabilities.

By following the patterns described in this document, the application achieves:

1. **Secure Authentication**: Validated Supabase JWTs for authentication in Azure Functions
2. **Optimized Data Access**: Connection pooling and multi-level caching for performance
3. **Real-Time Updates**: Webhook integration for data synchronization
4. **Scalable Architecture**: Patterns that scale from small to large deployments
5. **Cost-Effective Operation**: Targeted use of each platform based on strengths

This hybrid approach offers the best of both worlds, creating a robust foundation that can grow with the application's needs.
