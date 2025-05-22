# Using Entity Framework Core with Supabase in GolfApp

This guide explains how to use Entity Framework Core (EF Core) with your Supabase database in the GolfApp .NET backend.

## Overview

The integration allows you to:
1. Use familiar EF Core patterns and LINQ queries with your Supabase database
2. Leverage EF Core's change tracking and navigation properties
3. Keep your Supabase schema as the source of truth while providing a typed .NET interface
4. Handle row-level security (RLS) correctly with proper authentication

## Components Added

1. **Entity Framework Core Context**: `GolfAppDbContext` provides the entry point for database operations
2. **Entity Models**: C# classes that map to your Supabase tables
3. **Services**: Using EF Core for business logic operations 
4. **Program Setup**: Configuration for integrating EF Core with your Azure Functions app

## Implementation Details

### Entity Framework Core Context

The `GolfAppDbContext` maps your C# entity classes to the existing Supabase tables:

```csharp
public class GolfAppDbContext : DbContext
{
    public GolfAppDbContext(DbContextOptions<GolfAppDbContext> options)
        : base(options)
    {
    }

    // DbSets for your entities that map to Supabase tables
    public DbSet<Tournament> Tournaments { get; set; }
    public DbSet<Player> Players { get; set; }
    // ...

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure entity relationships and mapping to match your Supabase schema
        modelBuilder.HasDefaultSchema("public");
        
        // Entity-specific configurations
        ConfigureTournaments(modelBuilder);
        // ...
        
        base.OnModelCreating(modelBuilder);
    }
}
```

### Entity Models

These are C# classes that map to your Supabase tables. For example:

```csharp
public class Tournament
{
    [Key]
    public Guid Id { get; set; }
    
    public string Name { get; set; } = null!;
    
    public DateTime StartDate { get; set; }
    
    // Other properties mapped to database columns
    
    // Navigation properties
    public virtual ICollection<TournamentPlayer>? Players { get; set; }
}
```

### Services

Services use EF Core to perform database operations:

```csharp
public class TournamentService : ITournamentService
{
    private readonly GolfAppDbContext _context;
    
    public TournamentService(GolfAppDbContext context)
    {
        _context = context;
    }
    
    public async Task<IEnumerable<Tournament>> GetTournamentsAsync(string? status = null)
    {
        IQueryable<Tournament> query = _context.Tournaments;
        
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status == status);
        }
        
        return await query.OrderByDescending(t => t.StartDate)
                        .ToListAsync();
    }
    
    // Other methods for CRUD operations
}
```

## Row-Level Security (RLS) Considerations

When using EF Core with Supabase, you must consider Supabase's Row-Level Security (RLS) policies:

1. **Authentication**: The database connection from your .NET backend should include JWT tokens that satisfy Supabase RLS policies

2. **Connection Options**: Use `FunctionRoleOverride` in your Npgsql connection string to run as a specific role (optional for bypassing RLS)

3. **Service-level enforcement**: Sometimes it's better to implement additional authorization logic in your C# services

## Setup Instructions

### 1. Install Required NuGet Packages

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
```

### 2. Configure Database Connection

Update your `local.settings.json` (and deployment settings) with the correct connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.yourproject.supabase.co;Database=postgres;Username=postgres;Password=your-db-password;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

### 3. Register EF Core in Your Application

Update your `Program.cs` to register EF Core:

```csharp
services.AddDbContext<GolfAppDbContext>(options =>
    options.UseNpgsql(context.Configuration.GetConnectionString("DefaultConnection")));

// Register services that use EF Core
services.AddScoped<ITournamentService, TournamentService>();
```

### 4. Using EF Core with Existing Supabase Schema

1. **Do not run EF Core migrations**: Since you're using an existing Supabase schema, do not generate EF Core migrations

2. **Match column names exactly**: Configure your entity mappings to match Supabase column names precisely 

3. **Configure relationships**: Make sure navigation properties match your foreign key relationships in Supabase

## Best Practices

1. **Keep Supabase as the Schema Authority**: Use Supabase migrations for schema changes, not EF Core migrations

2. **Handle Errors Gracefully**: Account for potential conflicts with row-level security policies

3. **Balance Between Approaches**: Consider when to use direct Supabase SDK calls versus EF Core:
   - Use EF Core for complex querying, tracking, and navigation properties
   - Use Supabase SDK for real-time updates and specialized Supabase features

4. **Performance Considerations**:
   - Use `AsNoTracking()` for read-only queries
   - Be mindful of navigation property loading (lazy vs eager)
   - Consider caching for frequently used data

## Example Workflow: Creating a New Tournament

```csharp
// Controller or Function
[Function("CreateTournament")]
public async Task<HttpResponseData> CreateTournament(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    var requestData = await JsonSerializer.DeserializeAsync<TournamentCreateDto>(req.Body);
    var userId = req.FunctionContext.GetSupabaseUserId();
    
    var tournament = new Tournament
    {
        Name = requestData.Name,
        StartDate = requestData.StartDate,
        EndDate = requestData.EndDate,
        OrganizerId = userId, // Set by authenticated user
        // Other properties
    };
    
    var createdTournament = await _tournamentService.CreateTournamentAsync(tournament);
    
    var response = req.CreateResponse(HttpStatusCode.Created);
    await response.WriteAsJsonAsync(createdTournament);
    return response;
}
```

## Conclusion

Using Entity Framework Core with Supabase gives you the best of both worlds:
- Supabase's authentication, real-time capabilities, and PostgreSQL features
- EF Core's strongly-typed querying, navigation properties, and change tracking

Consider your specific needs when choosing between direct Supabase SDK calls and EF Core operations for different parts of your application.
