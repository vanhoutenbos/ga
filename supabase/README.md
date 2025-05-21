# Supabase Database Schema Management

This directory contains the Infrastructure as Code (IaC) for managing the Supabase database schema for the Golf Tournament Organizer application. The database schema is managed using Supabase CLI migrations and deployed through GitHub Actions CI/CD pipelines.

## Directory Structure

- **migrations/**: Contains SQL migration files for database schema changes
- **functions/**: Contains Supabase Edge Functions and PostgreSQL functions 
- **scripts/**: Contains utility scripts for working with Supabase
- **seed/**: Contains seed data for development and testing purposes
- **config.toml**: Supabase project configuration file

## Migration Naming Convention

Migration files follow a specific naming convention:

```
YYYYMMDDHHMMSS_descriptive_name.sql
```

For example:
```
20250501000000_initial_schema.sql
20250502000000_row_level_security_policies.sql
```

Each migration file should:
1. Begin with a comment block describing the migration
2. Include a `BEGIN;` and `COMMIT;` transaction to ensure atomicity
3. Include both the changes and any cleanup (like creating indexes)
4. In production migrations, avoid destructive changes when possible

## Working with Migrations

### Creating a New Migration

1. Install the Supabase CLI:
   ```
   npm install -g supabase
   ```

2. Create a new migration:
   ```
   supabase migration new add_new_feature_name
   ```
   This creates a timestamped migration file in the `migrations/` directory.

3. Edit the migration file to add your schema changes.

### Testing Migrations Locally

1. Start a local Supabase instance:
   ```
   supabase start
   ```

2. Apply migrations to your local database:
   ```
   supabase db reset
   ```
   or
   ```
   supabase db push
   ```

3. Verify your changes using the Supabase Studio at `http://localhost:54323`.

## CI/CD Pipeline Integration

The database schema is automatically deployed through our GitHub Actions workflows:

1. **CI Workflow**: Validates SQL syntax and tests migrations on pull requests
2. **Development Deployment**: Applies migrations to the development environment on push to `develop`
3. **Production Deployment**: Applies migrations to production on push to `main`

### How Migrations are Deployed

1. The workflow detects changes to files in the `supabase/` directory
2. For each environment, the workflow:
   - Links to the appropriate Supabase project
   - Applies pending migrations using `supabase db push`
   - Verifies migrations were applied correctly

### Migration Safety Practices

To ensure safe migrations:

1. **Reversibility**: Each migration should be reversible when possible
2. **Idempotency**: Migrations should be idempotent (can be applied multiple times safely)
3. **Atomic Changes**: Use transactions to ensure all-or-nothing changes
4. **Validation**: The CI process validates migrations before deployment
5. **Dry Runs**: The production deployment includes a dry run step

## Row Level Security (RLS) Policies

The application uses Supabase's Row Level Security (RLS) for data isolation and security. Key policy files:

- `20250502000000_row_level_security_policies.sql`: Main RLS policy definitions

## Common Tasks

### Adding a New Table

1. Create a new migration file:
   ```
   supabase migration new add_new_table
   ```

2. Define the table structure in the migration file:
   ```sql
   BEGIN;

   CREATE TABLE new_table (
       id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
       name TEXT NOT NULL,
       created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
       updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
   );

   -- Enable RLS
   ALTER TABLE new_table ENABLE ROW LEVEL SECURITY;

   -- Add RLS policies
   CREATE POLICY new_table_select_policy ON new_table FOR SELECT USING (...);
   
   -- Add updated_at trigger
   CREATE TRIGGER update_new_table_modtime
       BEFORE UPDATE ON new_table
       FOR EACH ROW EXECUTE FUNCTION update_modified_column();

   COMMIT;
   ```

### Modifying an Existing Table

1. Create a new migration file:
   ```
   supabase migration new alter_existing_table
   ```

2. Define the changes in the migration file:
   ```sql
   BEGIN;

   -- Add a new column
   ALTER TABLE existing_table ADD COLUMN new_column TEXT;

   -- Create an index
   CREATE INDEX idx_existing_table_new_column ON existing_table(new_column);

   COMMIT;
   ```

## Troubleshooting

### Common Issues

1. **Migration Conflicts**: If migrations fail due to conflicts:
   - Check if the schema has diverged from expected state
   - Use `supabase db remote changes` to inspect current schema
   - Consider creating a new migration to fix the conflict

2. **RLS Policy Issues**: If data access is not working correctly:
   - Verify RLS policies in Studio
   - Check role assignments in the tenant_users table
   - Use `supabase db test` to run policy unit tests (if defined)

### Getting Help

For issues with Supabase database schema management, contact the platform team or refer to:
1. [Supabase CLI Documentation](https://supabase.com/docs/reference/cli)
2. [PostgreSQL Documentation](https://www.postgresql.org/docs/)

## Local Development Scripts

The repository includes several utility scripts to simplify local development and environment management:

### Setup Scripts

1. **Local Setup**:
   ```
   node supabase/scripts/local-setup.js
   ```
   - Initializes a local Supabase instance
   - Configures project settings
   - Creates necessary database structures

2. **Migration Management**:
   ```
   node supabase/scripts/migrate.js
   ```
   - Applies migrations in the correct order
   - Handles schema changes safely
   - Reports migration status

3. **Project Setup**:
   ```
   node supabase/scripts/setup-project.js [projectName]
   ```
   - Creates a new Supabase project
   - Configures initial settings
   - Sets up authentication and storage

4. **Environment Sync**:
   ```
   node supabase/scripts/sync-environments.js [sourceEnv] [targetEnv]
   ```
   - Synchronizes schemas between environments
   - Copies data selectively (optional)
   - Preserves environment-specific settings

### PowerShell Development Script

The root PowerShell script `dev.ps1` provides common development commands:

```powershell
# Start local development
./dev.ps1 start

# Apply migrations
./dev.ps1 migrate

# Reset local database
./dev.ps1 reset

# Generate types
./dev.ps1 types
```

## Best Practices

1. **Keep Migrations Small**: Smaller, focused migrations are easier to review and less likely to cause issues
2. **Test Thoroughly**: Test migrations locally before committing
3. **Comment Your SQL**: Add comments to explain complex parts of your migrations
4. **Use Transactions**: Wrap migrations in transactions to ensure atomicity
5. **Preserve Data**: Design migrations to preserve existing data when possible
6. **Document Breaking Changes**: If a migration could break existing functionality, document it clearly

## Caching System

The application includes a multi-level caching system to improve performance and support offline capabilities:

### Database-Level Caching

The caching system is implemented in the `20250521000000_caching_support.sql` migration and includes:

1. **Cache Invalidation Tracking**:
   - `cache_invalidation_events` table tracks when specific resources are updated
   - PostgreSQL functions for cache invalidation and timestamp retrieval
   - Database triggers that automatically invalidate related caches when data changes

2. **Cache Invalidation**:
   - `invalidate_cache(resource_type, resource_id, tenant_id)` - Function to manually invalidate cache
   - `get_cache_timestamp(resource_type, resource_id, tenant_id)` - Function to get the last invalidation timestamp

3. **Automatic Triggers**:
   - `tournament_cache_invalidation` - Trigger for tournament updates
   - `score_cache_invalidation` - Trigger for score updates

### Client-Side Caching

The client-side caching is implemented in `src/lib/supabase-cache.js` and provides:

1. **Browser Storage**:
   - Uses localforage for persistent browser-based storage
   - Configurable cache expiration (TTL)
   - Handles offline access with graceful fallbacks

2. **Key Features**:
   - Offline data access
   - Automatic cache invalidation based on timestamps
   - Fallback to stale data when network errors occur
   - Pre-fetching for offline use

### Real-time Data Management

Real-time data handling is implemented in `src/lib/realtime-leaderboard.ts`:

1. **Supabase Realtime**:
   - Subscribes to Postgres changes for leaderboards and scores
   - Automatically refreshes client data when changes occur

2. **Fallback Polling**:
   - Gracefully switches to interval-based polling when real-time connections fail
   - Automatically reconnects to real-time when connection is restored
   - Configurable polling intervals

## Rate Limiting System

To protect backend resources and ensure fair usage as the platform scales, a rate limiting system is implemented:

### Edge Function Rate Limiter

Located in `functions/rate-limiter/index.ts`, this Supabase Edge Function:

1. **Configurable Limits**:
   - Different limits for anonymous vs. authenticated users
   - Different limits by endpoint type (scoring, leaderboards, etc.)
   - Configurable time windows and request thresholds

2. **Implementation**:
   - Acts as a middleware that processes requests before they reach the database
   - Returns appropriate 429 status codes with Retry-After headers
   - Gracefully handles errors to prevent blocking legitimate requests

### Database Rate Limiting

Implemented in `20250521000000_caching_support.sql`:

1. **Rate Limit Storage**:
   - `rate_limits` table tracks request counts by IP and/or user
   - Includes request counting and automatic window reset

2. **Rate Limit Function**:
   - `check_rate_limit()` function enforces limits based on configurable parameters
   - Tracks requests by IP address and/or user ID
   - Supports different endpoints and limit configurations

### Usage

To apply rate limiting to an API endpoint:

1. Add the edge function to the request path in Supabase Dashboard
2. Configure appropriate limits in the rate limiter constants
3. The function will automatically handle tracking and enforcing limits

### Local Testing

Test rate limiting locally using:
```
supabase functions serve rate-limiter
```

Then send test requests with varying frequencies to verify limits are enforced.
