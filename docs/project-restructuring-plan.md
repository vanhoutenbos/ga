# GolfApp Project Restructuring Plan

## Current Structure
```
/the-golfapp/
  ├── api/
  │   └── GolfApp.Api/              # .NET Backend API
  ├── src/                          # Frontend code
  ├── supabase/                     # Supabase config and migrations
  ├── docs/                         # Documentation
  ├── dev.ps1                       # Development scripts
  ├── package.json                  # Frontend package config
  └── README.md                     # Project documentation
```

## Proposed Structure
```
/GolfApp/
  ├── GolfApp.Client/              # Frontend (moved from /src)
  │   ├── src/                     # Source code
  │   ├── public/                  # Static assets
  │   └── package.json            # Frontend dependencies
  │
  ├── GolfApp.Api/                # Backend (moved from /api/GolfApp.Api)
  │   ├── Controllers/
  │   ├── Models/
  │   └── ...
  │
  ├── GolfApp.sln                 # Solution file (once .NET SDK is installed)
  ├── supabase/                   # Supabase configuration
  ├── docs/                       # Documentation
  └── scripts/                    # Development scripts
      └── dev.ps1                 # Improved dev script
```

## Migration Steps

### 1. Create the new directory structure

```powershell
# Create main directories
mkdir -Force C:\s\GolfApp
mkdir -Force C:\s\GolfApp\GolfApp.Client
mkdir -Force C:\s\GolfApp\GolfApp.Api
mkdir -Force C:\s\GolfApp\supabase
mkdir -Force C:\s\GolfApp\docs
mkdir -Force C:\s\GolfApp\scripts
```

### 2. Copy Backend Files

```powershell
# Copy API files
Copy-Item -Recurse -Path "C:\s\the-golfapp\api\GolfApp.Api\*" -Destination "C:\s\GolfApp\GolfApp.Api\"
```

### 3. Copy Frontend Files

```powershell
# Copy frontend source files
Copy-Item -Recurse -Path "C:\s\the-golfapp\src\*" -Destination "C:\s\GolfApp\GolfApp.Client\src\"

# Copy package files
Copy-Item -Path "C:\s\the-golfapp\package.json" -Destination "C:\s\GolfApp\GolfApp.Client\"
Copy-Item -Path "C:\s\the-golfapp\package-lock.json" -Destination "C:\s\GolfApp\GolfApp.Client\" -ErrorAction SilentlyContinue
```

### 4. Copy Supabase Configuration

```powershell
# Copy Supabase files
Copy-Item -Recurse -Path "C:\s\the-golfapp\supabase\*" -Destination "C:\s\GolfApp\supabase\"
```

### 5. Copy Documentation

```powershell
# Copy documentation
Copy-Item -Recurse -Path "C:\s\the-golfapp\docs\*" -Destination "C:\s\GolfApp\docs\"
Copy-Item -Path "C:\s\the-golfapp\README.md" -Destination "C:\s\GolfApp\"
```

### 6. Improve Development Script

```powershell
# Copy and rename dev script
Copy-Item -Path "C:\s\the-golfapp\dev.ps1" -Destination "C:\s\GolfApp\scripts\dev.ps1"
```

### 7. Create Solution File (once .NET SDK is installed)

```powershell
# Navigate to solution directory
cd C:\s\GolfApp

# Create solution file
dotnet new sln -n GolfApp

# Add API project to solution
dotnet sln add GolfApp.Api/GolfApp.Api.csproj
```

## Frontend Integration with Entity Framework and Supabase

For your question about using Entity Framework with Supabase - yes, it's possible since Supabase is built on PostgreSQL. Here's how to set it up in the new structure:

### 1. Add Required NuGet Packages to the API project

```powershell
cd C:\s\GolfApp\GolfApp.Api
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
```

### 2. Create a Database Context Class

```csharp
// GolfApp.Api/Data/GolfAppDbContext.cs
using Microsoft.EntityFrameworkCore;
using GolfApp.Api.Models;

namespace GolfApp.Api.Data
{
    public class GolfAppDbContext : DbContext
    {
        public GolfAppDbContext(DbContextOptions<GolfAppDbContext> options)
            : base(options)
        {
        }

        // Define your entity sets here
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<Player> Players { get; set; }
        // Add other entities as needed

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure your entity relationships here
            
            // Apply Supabase RLS-friendly configurations
            // This ensures EF Core works well with Supabase RLS policies
            modelBuilder.HasDefaultSchema("public");
            
            // Example: Configure audit fields
            modelBuilder.Entity<Tournament>()
                .Property(t => t.CreatedAt)
                .HasDefaultValueSql("now()");
                
            base.OnModelCreating(modelBuilder);
        }
    }
}
```

### 3. Configure the DB Connection in Startup/Program.cs

```csharp
// In Program.cs or Startup.cs
builder.Services.AddDbContext<GolfAppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SupabaseConnection")));
```

### 4. Connection String in appsettings.json or local.settings.json

```json
{
  "ConnectionStrings": {
    "SupabaseConnection": "Host=db.yoursupbaseproject.supabase.co;Database=postgres;Username=postgres;Password=your_db_password;Port=5432;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

## Benefits of the New Structure

1. **Clear Separation of Concerns**: Frontend and backend are clearly separated
2. **Standard .NET Project Structure**: Follows standard .NET conventions
3. **Better Organization**: Related files are grouped together logically
4. **Easier CI/CD Integration**: Separate pipelines can be set up for frontend and backend
5. **Better Dependency Management**: Frontend and backend dependencies are properly isolated
6. **Improved Developer Experience**: Clearer paths and better organization

## Next Steps After Restructuring

1. Update GitHub Actions workflows to account for the new structure
2. Update documentation references to reflect new file paths
3. Set up proper CI/CD pipelines for both frontend and backend
4. Create proper environment configurations for different deployment environments

## Best Practices for Supabase and .NET Integration

1. Use migrations in both systems but let Supabase handle the schema
2. Use Entity Framework as a read/write layer on top of Supabase
3. Leverage Supabase for authentication but implement proper JWT validation in .NET
4. Use Supabase Realtime for live updates and .NET SignalR for backend events
