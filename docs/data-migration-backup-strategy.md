# Data Migration and Backup Strategy

This document outlines the backup, restore, and migration strategies for the Golf Tournament Organizer application. It covers Supabase PostgreSQL databases, storage, and authentication data.

## Backup Strategy

### Backup Overview

| Resource | Backup Frequency | Retention Policy | Storage Location |
|----------|------------------|-----------------|------------------|
| PostgreSQL Database | Daily (full) + 6-hour incrementals | 30 days daily, 3 months weekly, 1 year monthly | Primary: Supabase automated backups<br>Secondary: Azure Blob Storage |
| Supabase Storage | Daily | 30 days | Azure Blob Storage |
| Schema Definitions | After every schema change | Indefinite (version controlled) | GitHub repository |
| Authentication Data | Daily | 30 days | Encrypted backup in Azure Blob Storage |

### PostgreSQL Database Backups

#### Automated Supabase Backups

Supabase Pro tier includes automated daily backups with point-in-time recovery (PITR) for the past 7 days.

**Limitations:**
- Free tier does not include automated backups
- Supabase-managed backups cannot be directly downloaded

#### Custom Scheduled Backups

For additional protection and direct access to backup files, we implement custom scheduled backups:

```bash
#!/bin/bash
# backup-database.sh - Run from Azure Functions timer trigger

# Environment variables
DB_NAME=${DB_NAME}
DB_HOST=${DB_HOST}
DB_PORT=${DB_PORT}
DB_USER=${DB_USER}
DB_PASSWORD=${DB_PASSWORD}
BACKUP_BUCKET=${BACKUP_BUCKET}
AZURE_STORAGE_CONNECTION_STRING=${AZURE_STORAGE_CONNECTION_STRING}

# Timestamp for backup file
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="golfapp_backup_${TIMESTAMP}.sql.gz"

# Create backup
PGPASSWORD=$DB_PASSWORD pg_dump -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -F c | gzip > $BACKUP_FILE

# Upload to Azure Blob Storage
az storage blob upload --container-name $BACKUP_BUCKET --file $BACKUP_FILE --name "database/$BACKUP_FILE" --connection-string $AZURE_STORAGE_CONNECTION_STRING

# Delete local file
rm $BACKUP_FILE

# Keep only last 30 daily backups, 12 weekly backups, and 12 monthly backups
# ... retention logic ...
```

**Schedule:**
- **Full backups**: Daily at 01:00 UTC
- **Incremental backups**: Every 6 hours
- **Transaction log backups**: Continuous (PITR with Supabase Pro)

### Supabase Storage Backups

Storage objects (tournament images, documents, etc.) are backed up using the Supabase Storage API:

```typescript
// Example backup logic for Supabase Storage
async function backupStorageBucket(bucketName: string) {
  // List all objects in the bucket
  const { data: objects, error } = await supabase
    .storage
    .from(bucketName)
    .list();
    
  if (error) throw error;
  
  // Download and upload each object to Azure Blob Storage
  for (const object of objects) {
    if (object.name.endsWith('/')) continue; // Skip directories
    
    // Download from Supabase
    const { data, error: downloadError } = await supabase
      .storage
      .from(bucketName)
      .download(object.name);
      
    if (downloadError) throw downloadError;
    
    // Upload to Azure Blob Storage
    await blockBlobClient.upload(data, data.length);
  }
}
```

**Schedule:** Daily at 03:00 UTC

### Schema Version Control

Database schema is version-controlled in the repository:

1. **SQL migration files**: Located in `db/migrations/`
2. **Entity models**: C# models with annotations in `src/api/GolfApp.Api/Models/`

Each schema change requires:
1. A new migration script with both up and down migrations
2. Version number incrementing
3. Documentation of changes

### Authentication Data Backup

User authentication data is backed up through Supabase's APIs:

```typescript
async function backupAuthData() {
  // This requires a service role key with appropriate permissions
  const { data, error } = await supabaseAdmin
    .from('auth.users')
    .select('*');
    
  if (error) throw error;
  
  // Encrypt sensitive data before storing
  const encryptedData = encrypt(JSON.stringify(data));
  
  // Store in Azure Blob Storage
  await blockBlobClient.upload(encryptedData, encryptedData.length);
}
```

## Backup Testing and Verification

### Regular Restore Testing

Restore testing is performed on a monthly basis to verify backup integrity and restore procedures:

1. **Database restore testing**: Monthly, full restore to test environment
2. **Storage restore testing**: Monthly, random sampling of files
3. **Disaster recovery simulation**: Quarterly, full environment recovery

### Restore Testing Procedure

```bash
#!/bin/bash
# restore-test.sh

# Set variables
BACKUP_FILE="golfapp_backup_20230515_010000.sql.gz"
TEST_DB_HOST="test-db-host"
TEST_DB_PORT="5432"
TEST_DB_NAME="test_restore_db"
TEST_DB_USER="test_user"
TEST_DB_PASSWORD="test_password"

# Download backup from Azure Blob Storage
az storage blob download --container-name $BACKUP_BUCKET --name "database/$BACKUP_FILE" --file $BACKUP_FILE --connection-string $AZURE_STORAGE_CONNECTION_STRING

# Create test database
PGPASSWORD=$TEST_DB_PASSWORD psql -h $TEST_DB_HOST -p $TEST_DB_PORT -U $TEST_DB_USER -c "CREATE DATABASE $TEST_DB_NAME;"

# Restore backup
gunzip -c $BACKUP_FILE | PGPASSWORD=$TEST_DB_PASSWORD pg_restore -h $TEST_DB_HOST -p $TEST_DB_PORT -U $TEST_DB_USER -d $TEST_DB_NAME

# Run verification queries
PGPASSWORD=$TEST_DB_PASSWORD psql -h $TEST_DB_HOST -p $TEST_DB_PORT -U $TEST_DB_USER -d $TEST_DB_NAME -c "SELECT COUNT(*) FROM tournaments;"
PGPASSWORD=$TEST_DB_PASSWORD psql -h $TEST_DB_HOST -p $TEST_DB_PORT -U $TEST_DB_USER -d $TEST_DB_NAME -c "SELECT COUNT(*) FROM scores;"

# Clean up
PGPASSWORD=$TEST_DB_PASSWORD psql -h $TEST_DB_HOST -p $TEST_DB_PORT -U $TEST_DB_USER -c "DROP DATABASE $TEST_DB_NAME;"
rm $BACKUP_FILE
```

### Backup Monitoring

Backup processes are monitored through:

1. **Azure Function App logs**: Verify successful execution
2. **Application Insights**: Track backup duration and size
3. **Email notifications**: Success and failure alerts
4. **Azure Monitor alerts**: Trigger on backup failures

## Data Migration Strategy

### Schema Migrations

Database schema migrations are managed through:

1. **Migration scripts**: SQL scripts for each schema change
2. **Version tracking**: Database version table to track applied migrations
3. **Automated application**: Migrations applied through CI/CD pipeline

#### Migration Script Example

```sql
-- Migration: 0001_initial_schema
-- Description: Initial schema for tournaments and players

-- Up Migration
CREATE TABLE tournaments (
  id UUID PRIMARY KEY,
  tenant_id UUID NOT NULL,
  name TEXT NOT NULL,
  -- other columns
);

CREATE TABLE players (
  id UUID PRIMARY KEY,
  email TEXT UNIQUE,
  -- other columns
);

-- Down Migration
DROP TABLE players;
DROP TABLE tournaments;
```

### Application Deployment With Migrations

Database migrations are integrated into the CI/CD pipeline:

1. **Pre-deployment check**: Verify schema compatibility
2. **Migration application**: Apply pending migrations
3. **Rollback capability**: Automatic rollback on failure
4. **Validation**: Run tests against new schema

```yaml
# Example GitHub Actions workflow step
- name: Apply database migrations
  run: |
    cd db/migrations
    dotnet run --project ../../src/tools/Migrator/Migrator.csproj \
      --connection "${{ secrets.SUPABASE_CONNECTION_STRING }}" \
      --apply-migrations
```

### Data Export and Portability

Data can be exported for portability using:

1. **CSV export**: For tabular data
2. **JSON export**: For structured data with relationships
3. **Full database dump**: For complete migration

#### CSV Export Example

```csharp
public async Task<string> ExportTournamentToCSV(string tournamentId)
{
    var tournament = await _supabaseClient
        .From<Tournament>()
        .Where(t => t.Id == tournamentId)
        .Get();
        
    var scores = await _supabaseClient
        .From<Score>()
        .Where(s => s.TournamentId == tournamentId)
        .Get();
        
    var csv = new StringBuilder();
    csv.AppendLine("player_id,player_name,hole_number,strokes,putts");
    
    foreach (var score in scores)
    {
        csv.AppendLine($"{score.PlayerId},{score.PlayerName},{score.HoleNumber},{score.Strokes},{score.Putts}");
    }
    
    return csv.ToString();
}
```

## Growth and Scaling Considerations

### Free Tier to Pro Tier Migration

When outgrowing the Supabase free tier, the migration process will be:

1. **Subscription upgrade**: Upgrade to Supabase Pro tier
2. **Performance tuning**: Apply optimizations suitable for Pro tier
3. **Backup review**: Adapt backup strategy for Pro tier capabilities

### Self-Hosting Migration

For customers requiring self-hosted deployment, the migration path is:

1. **Export full database**: `pg_dump` of the entire database
2. **Export storage**: Sync all storage objects to local storage
3. **Configure self-hosted Supabase**: Deploy using Docker Compose
4. **Import data**: Restore database and storage
5. **Update connection strings**: Point application to new instance
6. **Verification**: Run full test suite against new instance

## Conclusion

This comprehensive backup and migration strategy ensures data integrity, disaster recovery capabilities, and smooth scaling paths for the Golf Tournament Organizer application. It addresses both automated and manual backup procedures, regular testing, and clear migration paths for different growth scenarios.
