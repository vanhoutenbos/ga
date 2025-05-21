# Supabase Development Helper Script
# This script simplifies working with Supabase locally

# Function to show help
show_help() {
  echo "The Golf App Supabase Development Helper"
  echo ""
  echo "Usage: ./dev.ps1 [command]"
  echo ""
  echo "Commands:"
  echo "  start         - Start local Supabase development instance"
  echo "  stop          - Stop local Supabase development instance"
  echo "  reset         - Reset local database (apply migrations and seed data)"
  echo "  migrate       - Apply migrations to local database"
  echo "  migration     - Create a new migration file"
  echo "  deploy-dev    - Deploy to development environment"
  echo "  deploy-prod   - Deploy to production environment"
  echo "  dump          - Dump local database to SQL file"
  echo "  status        - Check Supabase status"
  echo "  functions     - List all edge functions"
  echo "  serve         - Run edge functions locally"
  echo "  help          - Show this help"
  echo ""
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
    Write-Host "Starting local Supabase development instance..."
    supabase start
  }
  
  "stop" {
    Write-Host "Stopping local Supabase development instance..."
    supabase stop
  }
  
  "reset" {
    Write-Host "Resetting local database (applying migrations and seed data)..."
    supabase db reset
  }
  
  "migrate" {
    Write-Host "Applying migrations to local database..."
    supabase db push
  }
  
  "migration" {
    if ($args.Count -lt 2) {
      Write-Host "Error: Migration name is required."
      Write-Host "Usage: ./dev.ps1 migration [migration-name]"
      exit 1
    }
    $migrationName = $args[1]
    Write-Host "Creating new migration: $migrationName"
    supabase migration new $migrationName
  }
  
  "deploy-dev" {
    Write-Host "Deploying to development environment..."
    
    if (-not $env:SUPABASE_DEV_PROJECT_ID -or -not $env:SUPABASE_ACCESS_TOKEN) {
      Write-Host "Error: Missing required environment variables."
      Write-Host "Please set SUPABASE_DEV_PROJECT_ID and SUPABASE_ACCESS_TOKEN."
      exit 1
    }
    
    # Link to development project
    supabase link --project-ref $env:SUPABASE_DEV_PROJECT_ID
    
    # Apply migrations
    supabase db push
    
    # Deploy edge functions
    Get-ChildItem -Path (Join-Path $SupabasePath "functions") -Directory | ForEach-Object {
      $functionName = $_.Name
      Write-Host "Deploying function: $functionName"
      supabase functions deploy $functionName --project-ref $env:SUPABASE_DEV_PROJECT_ID
    }
    
    Write-Host "Deployment to development completed."
  }
  
  "deploy-prod" {
    Write-Host "Deploying to production environment..."
    
    if (-not $env:SUPABASE_PROD_PROJECT_ID -or -not $env:SUPABASE_ACCESS_TOKEN) {
      Write-Host "Error: Missing required environment variables."
      Write-Host "Please set SUPABASE_PROD_PROJECT_ID and SUPABASE_ACCESS_TOKEN."
      exit 1
    }
    
    Write-Host "This will deploy to PRODUCTION. Are you sure? (y/N)"
    $confirmation = Read-Host
    if ($confirmation.ToLower() -ne "y") {
      Write-Host "Deployment cancelled."
      exit 0
    }
    
    # Link to production project
    supabase link --project-ref $env:SUPABASE_PROD_PROJECT_ID
    
    # Dry run migrations first
    supabase db push --dry-run
    
    Write-Host "Above is a dry run of the migrations. Continue with the actual deployment? (y/N)"
    $confirmation = Read-Host
    if ($confirmation.ToLower() -ne "y") {
      Write-Host "Deployment cancelled."
      exit 0
    }
    
    # Apply migrations
    supabase db push
    
    # Deploy edge functions
    Get-ChildItem -Path (Join-Path $SupabasePath "functions") -Directory | ForEach-Object {
      $functionName = $_.Name
      Write-Host "Deploying function: $functionName"
      supabase functions deploy $functionName --project-ref $env:SUPABASE_PROD_PROJECT_ID
    }
    
    Write-Host "Deployment to production completed."
  }
  
  "dump" {
    $timestamp = Get-Date -Format "yyyyMMddHHmmss"
    $dumpFile = "supabase_dump_$timestamp.sql"
    Write-Host "Dumping local database to $dumpFile..."
    supabase db dump -f $dumpFile
  }
  
  "status" {
    Write-Host "Checking Supabase status..."
    supabase status
  }
  
  "functions" {
    Write-Host "Listing all edge functions..."
    supabase functions list
  }
  
  "serve" {
    Write-Host "Running edge functions locally..."
    supabase functions serve
  }
  
  "help" {
    show_help
  }
  
  default {
    Write-Host "Unknown command: $command"
    show_help
    exit 1
  }
}
