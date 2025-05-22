# filepath: c:\s\the-golfapp\dev.ps1
# Supabase Development Helper Script
# This script simplifies working with Supabase locally

# Function to show help
function show_help {
  Write-Host "The Golf App Supabase Development Helper" -ForegroundColor Cyan
  Write-Host ""
  Write-Host "Usage: ./dev.ps1 [command]"
  Write-Host ""
  Write-Host "Commands:" -ForegroundColor Yellow
  Write-Host "  setup         - Complete setup of local Supabase environment (start, migrate, seed, types)" -ForegroundColor Green
  Write-Host "  start         - Start local Supabase development instance"
  Write-Host "  stop          - Stop local Supabase development instance"
  Write-Host "  reset         - Reset local database (apply migrations and seed data)"
  Write-Host "  migrate       - Apply migrations to local database"
  Write-Host "  seed          - Seed the database with development data"
  Write-Host "  migration     - Create a new migration file"
  Write-Host "  types         - Generate TypeScript types from database schema"
  Write-Host "  deploy-dev    - Deploy to development environment"
  Write-Host "  deploy-prod   - Deploy to production environment"
  Write-Host "  dump          - Dump local database to SQL file"
  Write-Host "  status        - Check Supabase status"
  Write-Host "  functions     - List all edge functions"
  Write-Host "  serve         - Run edge functions locally"
  Write-Host "  cache-clear   - Clear local cache settings"
  Write-Host "  help          - Show this help"
  Write-Host ""
}

# Get the Supabase directory path
$SupabasePath = Join-Path $PSScriptRoot "supabase"

# Process commands
if ($args.Count -eq 0) {
  show_help
  exit 0
}

$command = $args[0]

switch ($command) {
  "start" {
    Write-Host "Starting local Supabase development instance..." -ForegroundColor Green
    try {
      supabase start
      Write-Host "Supabase started successfully" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error starting Supabase: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "stop" {
    Write-Host "Stopping local Supabase development instance..." -ForegroundColor Yellow
    try {
      supabase stop
      Write-Host "Supabase stopped successfully" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error stopping Supabase: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "reset" {
    Write-Host "Resetting local database (applying migrations and seed data)..." -ForegroundColor Yellow
    Write-Host "This will delete all local data. Are you sure? (y/N)"
    $confirmation = Read-Host
    if ($confirmation.ToLower() -ne "y") {
      Write-Host "Reset cancelled."
      exit 0
    }
    
    try {
      supabase db reset
      Write-Host "Database reset successfully" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error resetting database: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "migrate" {
    Write-Host "Applying migrations to local database..." -ForegroundColor Green
    try {
      supabase db push
      Write-Host "Migrations applied successfully" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error applying migrations: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "migration" {
    if ($args.Count -lt 2) {
      Write-Host "Error: Migration name is required." -ForegroundColor Red
      Write-Host "Usage: ./dev.ps1 migration [migration-name]" -ForegroundColor Yellow
      exit 1
    }
    $migrationName = $args[1]
    Write-Host "Creating new migration: $migrationName" -ForegroundColor Green
    
    # Check if the migration name follows the proper format
    if ($migrationName -notmatch "^[a-z0-9_]+$") {
      Write-Host "Warning: Migration name should contain only lowercase letters, numbers, and underscores" -ForegroundColor Yellow
    }
    
    try {
      supabase migration new $migrationName
      Write-Host "Migration created successfully" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error creating migration: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "deploy-dev" {
    Write-Host "Deploying to development environment..." -ForegroundColor Cyan
    
    if (-not $env:SUPABASE_DEV_PROJECT_ID -or -not $env:SUPABASE_ACCESS_TOKEN) {
      Write-Host "Error: Missing required environment variables." -ForegroundColor Red
      Write-Host "Please set SUPABASE_DEV_PROJECT_ID and SUPABASE_ACCESS_TOKEN." -ForegroundColor Yellow
      Write-Host "You can set these by running:" -ForegroundColor Yellow
      Write-Host '$env:SUPABASE_DEV_PROJECT_ID = "your-project-id"' -ForegroundColor Gray
      Write-Host '$env:SUPABASE_ACCESS_TOKEN = "your-access-token"' -ForegroundColor Gray
      exit 1
    }
    
    # Verify Supabase CLI is installed
    try {
      $supabaseVersion = (supabase --version) 2>&1
      if (-not $?) {
        throw "Supabase CLI not found"
      }
      Write-Host "Using Supabase CLI: $supabaseVersion" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error: Supabase CLI not found or not working properly." -ForegroundColor Red
      Write-Host "Please install it with: npm install -g supabase" -ForegroundColor Yellow
      exit 1
    }
    
    # Link to development project
    try {
      Write-Host "Linking to development project..." -ForegroundColor Green
      supabase link --project-ref $env:SUPABASE_DEV_PROJECT_ID
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error linking to development project: $errorMessage" -ForegroundColor Red
      exit 1
    }
    
    # Apply migrations
    try {
      Write-Host "Applying database migrations..." -ForegroundColor Green
      supabase db push
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error applying migrations: $errorMessage" -ForegroundColor Red
      exit 1
    }
    
    # Deploy edge functions
    $functionsPath = Join-Path $SupabasePath "functions"
    if (Test-Path $functionsPath) {
      $functions = Get-ChildItem -Path $functionsPath -Directory
      if ($functions.Count -gt 0) {
        Write-Host "Deploying edge functions..." -ForegroundColor Green
        
        foreach ($function in $functions) {
          $functionName = $function.Name
          try {
            Write-Host "Deploying function: $functionName" -ForegroundColor Cyan
            supabase functions deploy $functionName --project-ref $env:SUPABASE_DEV_PROJECT_ID
            Write-Host "Successfully deployed $functionName" -ForegroundColor Green
          } catch {
            $errorMessage = $_.Exception.Message
            Write-Host "Error deploying function ${functionName}: ${errorMessage}" -ForegroundColor Red
            # Continue with other functions even if one fails
          }
        }
      } else {
        Write-Host "No edge functions found to deploy." -ForegroundColor Yellow
      }
    } else {
      Write-Host "Functions directory not found. Skipping function deployment." -ForegroundColor Yellow
    }
    
    Write-Host "Deployment to development completed." -ForegroundColor Green
  }
  
  "deploy-prod" {
    Write-Host "Deploying to production environment..." -ForegroundColor Red
    
    if (-not $env:SUPABASE_PROD_PROJECT_ID -or -not $env:SUPABASE_ACCESS_TOKEN) {
      Write-Host "Error: Missing required environment variables." -ForegroundColor Red
      Write-Host "Please set SUPABASE_PROD_PROJECT_ID and SUPABASE_ACCESS_TOKEN." -ForegroundColor Yellow
      Write-Host "You can set these by running:" -ForegroundColor Yellow
      Write-Host '$env:SUPABASE_PROD_PROJECT_ID = "your-project-id"' -ForegroundColor Gray
      Write-Host '$env:SUPABASE_ACCESS_TOKEN = "your-access-token"' -ForegroundColor Gray
      exit 1
    }
    
    Write-Host "⚠️  WARNING: This will deploy to PRODUCTION. Are you sure? (y/N)" -ForegroundColor Red
    $confirmation = Read-Host
    if ($confirmation.ToLower() -ne "y") {
      Write-Host "Deployment cancelled." -ForegroundColor Yellow
      exit 0
    }
    
    # Verify Supabase CLI
    try {
      $supabaseVersion = (supabase --version) 2>&1
      if (-not $?) {
        throw "Supabase CLI not found"
      }
      Write-Host "Using Supabase CLI: $supabaseVersion" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error: Supabase CLI not found or not working properly." -ForegroundColor Red
      Write-Host "Please install it with: npm install -g supabase" -ForegroundColor Yellow
      exit 1
    }
    
    # Link to production project
    try {
      Write-Host "Linking to production project..." -ForegroundColor Green
      supabase link --project-ref $env:SUPABASE_PROD_PROJECT_ID
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error linking to production project: $errorMessage" -ForegroundColor Red
      exit 1
    }
    
    # Dry run migrations first
    try {
      Write-Host "Performing dry run of migrations..." -ForegroundColor Yellow
      supabase db push --dry-run
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error during dry run: $errorMessage" -ForegroundColor Red
      Write-Host "Aborting deployment to production." -ForegroundColor Red
      exit 1
    }
    
    Write-Host "Above is a dry run of the migrations. Continue with the actual deployment? (y/N)" -ForegroundColor Red
    $confirmation = Read-Host
    if ($confirmation.ToLower() -ne "y") {
      Write-Host "Deployment cancelled." -ForegroundColor Yellow
      exit 0
    }
    
    # Apply migrations
    try {
      Write-Host "Applying database migrations to production..." -ForegroundColor Yellow
      supabase db push
      Write-Host "Migrations applied successfully to production" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error applying migrations to production: $errorMessage" -ForegroundColor Red
      exit 1
    }
    
    # Deploy edge functions
    $functionsPath = Join-Path $SupabasePath "functions"
    if (Test-Path $functionsPath) {
      $functions = Get-ChildItem -Path $functionsPath -Directory
      if ($functions.Count -gt 0) {
        Write-Host "Deploying edge functions to production..." -ForegroundColor Yellow
        $deploymentErrors = 0
        
        foreach ($function in $functions) {
          $functionName = $function.Name
          try {
            Write-Host "Deploying function: $functionName to production" -ForegroundColor Cyan
            supabase functions deploy $functionName --project-ref $env:SUPABASE_PROD_PROJECT_ID
            Write-Host "Successfully deployed $functionName to production" -ForegroundColor Green
          } catch {
            $errorMessage = $_.Exception.Message
            Write-Host "Error deploying function $functionName to production: $errorMessage" -ForegroundColor Red
            $deploymentErrors++
          }
        }
        
        if ($deploymentErrors -gt 0) {
          Write-Host "Warning: $deploymentErrors function(s) failed to deploy." -ForegroundColor Yellow
        }
      } else {
        Write-Host "No edge functions found to deploy." -ForegroundColor Yellow
      }
    } else {
      Write-Host "Functions directory not found. Skipping function deployment." -ForegroundColor Yellow
    }
    
    Write-Host "Deployment to production completed." -ForegroundColor Green
  }
  
  "dump" {
    $timestamp = Get-Date -Format "yyyyMMddHHmmss"
    $dumpFile = "supabase_dump_$timestamp.sql"
    Write-Host "Dumping local database to $dumpFile..." -ForegroundColor Green
    
    try {
      supabase db dump -f $dumpFile
      Write-Host "Database dumped successfully to $dumpFile" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error dumping database: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "status" {
    Write-Host "Checking Supabase status..." -ForegroundColor Green
    try {
      supabase status
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error checking status: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "functions" {
    Write-Host "Listing all edge functions..." -ForegroundColor Green
    try {
      $functions = supabase functions list
      if ($null -eq $functions -or $functions -eq "") {
        Write-Host "No functions found" -ForegroundColor Yellow
      }
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error listing functions: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "serve" {
    Write-Host "Running edge functions locally..." -ForegroundColor Green
    
    # Check if functions directory exists and has functions
    $functionsPath = Join-Path $SupabasePath "functions"
    if (-not (Test-Path $functionsPath)) {
      Write-Host "Error: Functions directory not found at $functionsPath" -ForegroundColor Red
      exit 1
    }
    
    $functionsExist = Get-ChildItem -Path $functionsPath -Directory
    if ($functionsExist.Count -eq 0) {
      Write-Host "Warning: No functions found in $functionsPath" -ForegroundColor Yellow
    }
    
    try {
      Write-Host "Starting functions server. Press Ctrl+C to exit." -ForegroundColor Cyan
      supabase functions serve
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error serving functions: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "cache-clear" {
    Write-Host "Clearing local cache settings..." -ForegroundColor Yellow
    
    $cacheDir = "$env:LOCALAPPDATA\Supabase\Cache"
    if (Test-Path $cacheDir) {
      try {
        Remove-Item -Path $cacheDir -Recurse -Force
        Write-Host "Cache cleared successfully" -ForegroundColor Green
      } catch {
        $errorMessage = $_.Exception.Message
        Write-Host "Error clearing cache: $errorMessage" -ForegroundColor Red
        exit 1
      }
    } else {
      Write-Host "No cache directory found at $cacheDir" -ForegroundColor Yellow
    }
  }
    "seed" {
    Write-Host "Seeding database with development data..." -ForegroundColor Green
    
    $seedFile = Join-Path $SupabasePath "seed\development.sql"
    
    if (-not (Test-Path $seedFile)) {
      Write-Host "Error: Seed file not found at $seedFile" -ForegroundColor Red
      exit 1
    }
    
    try {
      # Apply seed data using psql
      supabase db reset --db-url postgres://postgres:postgres@localhost:54322/postgres
      Write-Host "Development data seeded successfully" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error seeding database: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "types" {
    Write-Host "Generating TypeScript types from database schema..." -ForegroundColor Green
    
    $outputDir = Join-Path $PSScriptRoot "src\types"
    
    # Create output directory if it doesn't exist
    if (-not (Test-Path $outputDir)) {
      New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    
    try {
      supabase gen types typescript --local > "$outputDir\supabase.ts"
      Write-Host "TypeScript types generated successfully at $outputDir\supabase.ts" -ForegroundColor Green
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error generating types: $errorMessage" -ForegroundColor Red
      exit 1
    }
  }
  
  "setup" {
    Write-Host "Setting up local Supabase development environment..." -ForegroundColor Cyan
    
    # 1. Start Supabase
    Write-Host "1. Starting Supabase..." -ForegroundColor Green
    try {
      supabase start
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error starting Supabase: $errorMessage" -ForegroundColor Red
      exit 1
    }
    
    # 2. Apply migrations
    Write-Host "2. Applying migrations..." -ForegroundColor Green
    try {
      supabase db push
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error applying migrations: $errorMessage" -ForegroundColor Red
      exit 1
    }
    
    # 3. Seed development data
    Write-Host "3. Seeding development data..." -ForegroundColor Green
    $seedFile = Join-Path $SupabasePath "seed\development.sql"
    if (Test-Path $seedFile) {
      try {
        supabase db reset --db-url postgres://postgres:postgres@localhost:54322/postgres
      } catch {
        $errorMessage = $_.Exception.Message
        Write-Host "Error seeding database: $errorMessage" -ForegroundColor Red
        # Continue even if seeding fails
      }
    } else {
      Write-Host "No seed file found. Skipping." -ForegroundColor Yellow
    }
    
    # 4. Generate types
    Write-Host "4. Generating TypeScript types..." -ForegroundColor Green
    $outputDir = Join-Path $PSScriptRoot "src\types"
    if (-not (Test-Path $outputDir)) {
      New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    
    try {
      supabase gen types typescript --local > "$outputDir\supabase.ts"
    } catch {
      $errorMessage = $_.Exception.Message
      Write-Host "Error generating types: $errorMessage" -ForegroundColor Red
      # Continue even if type generation fails
    }
    
    Write-Host "✅ Local Supabase development environment setup complete!" -ForegroundColor Green
    Write-Host "Supabase Studio URL: http://localhost:54323" -ForegroundColor Cyan
    Write-Host "API URL: http://localhost:54321" -ForegroundColor Cyan
    
    # Display database credentials
    Write-Host "Database Connection Details:" -ForegroundColor Yellow
    Write-Host "  Host: localhost" -ForegroundColor Gray
    Write-Host "  Port: 54322" -ForegroundColor Gray
    Write-Host "  Database: postgres" -ForegroundColor Gray
    Write-Host "  Username: postgres" -ForegroundColor Gray
    Write-Host "  Password: postgres" -ForegroundColor Gray
  }
  
  "help" {
    show_help
  }
  
  default {
    Write-Host "Unknown command: $command" -ForegroundColor Red
    show_help
    exit 1
  }
}
