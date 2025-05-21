#!/usr/bin/env node

/**
 * Supabase Environment Synchronization Script
 * 
 * This script helps synchronize settings and configurations between
 * different Supabase environments (development, staging, production)
 */

const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const readline = require('readline');
const { promisify } = require('util');
const exec = promisify(require('child_process').exec);

// Create readline interface for user input
const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout
});

const question = (query) => new Promise((resolve) => rl.question(query, resolve));

/**
 * Helper function to run shell commands
 */
async function runCommand(command, options = {}) {
  try {
    console.log(`> ${command}`);
    const { stdout, stderr } = await exec(command, options);
    if (stdout) console.log(stdout);
    if (stderr) console.error(stderr);
    return stdout;
  } catch (error) {
    console.error(`Error executing command: ${command}`);
    console.error(error.stderr || error.message);
    throw error;
  }
}

/**
 * Get list of linked Supabase projects
 */
async function getLinkedProjects() {
  try {
    const { stdout } = await exec('supabase projects list');
    const lines = stdout.split('\n');
    const projects = [];
    
    // Skip the header lines
    let inProjectList = false;
    for (const line of lines) {
      if (line.trim() === '') continue;
      if (line.includes('Ref ID')) {
        inProjectList = true;
        continue;
      }
      if (inProjectList) {
        // Extract project name and ID
        const parts = line.trim().split(/\s+/);
        if (parts.length >= 2) {
          projects.push({
            id: parts[0],
            name: parts.slice(1).join(' ')
          });
        }
      }
    }
    
    return projects;
  } catch (error) {
    console.error('Error getting projects:', error.message);
    return [];
  }
}

/**
 * Export database to a dump file
 */
async function exportDatabase(projectId, filename) {
  try {
    const command = `supabase db dump --db-url "postgresql://postgres:PASSWORD@db.${projectId}.supabase.co:5432/postgres" -f ${filename}`;
    console.log('Please enter the database password:');
    const { stdout } = await exec(command, { stdio: 'inherit' });
    console.log(`✓ Database exported to ${filename}`);
    return true;
  } catch (error) {
    console.error(`× Error exporting database:`, error.message);
    return false;
  }
}

/**
 * Import database from a dump file
 */
async function importDatabase(projectId, filename) {
  try {
    const command = `supabase db push -f ${filename} --project-ref ${projectId}`;
    await runCommand(command);
    console.log('✓ Database imported successfully');
    return true;
  } catch (error) {
    console.error('× Error importing database:', error.message);
    return false;
  }
}

/**
 * Sync settings between Supabase projects
 */
async function syncSettings(sourceId, targetId) {
  try {
    // Get settings from source project
    const { stdout: sourceSettings } = await exec(`supabase config dump --project-ref ${sourceId}`);
    const settings = JSON.parse(sourceSettings);
    
    // Apply settings to target project
    for (const [category, configs] of Object.entries(settings)) {
      if (typeof configs === 'object') {
        for (const [key, value] of Object.entries(configs)) {
          if (value !== null && value !== undefined) {
            const valueString = typeof value === 'string' ? `"${value}"` : value;
            await runCommand(`supabase config set ${category}.${key}=${valueString} --project-ref ${targetId}`);
          }
        }
      }
    }
    
    console.log('✓ Settings synchronized successfully');
    return true;
  } catch (error) {
    console.error('× Error synchronizing settings:', error.message);
    return false;
  }
}

/**
 * Deploy edge functions to a project
 */
async function deployEdgeFunctions(projectId) {
  try {
    const functionsDir = path.join(__dirname, '..', 'functions');
    const functionFolders = fs.readdirSync(functionsDir);
    
    for (const folder of functionFolders) {
      const functionPath = path.join(functionsDir, folder);
      if (fs.statSync(functionPath).isDirectory()) {
        console.log(`Deploying edge function: ${folder}`);
        await runCommand(`supabase functions deploy ${folder} --project-ref ${projectId}`);
      }
    }
    
    console.log('✓ Edge functions deployed successfully');
    return true;
  } catch (error) {
    console.error('× Error deploying edge functions:', error.message);
    return false;
  }
}

/**
 * Apply a SQL file to a project
 */
async function applySqlFile(projectId, filePath) {
  try {
    await runCommand(`supabase db execute --project-ref ${projectId} --file ${filePath}`);
    console.log(`✓ SQL file ${filePath} applied successfully`);
    return true;
  } catch (error) {
    console.error(`× Error applying SQL file ${filePath}:`, error.message);
    return false;
  }
}

/**
 * Enable rate limiting in a project
 */
async function enableRateLimiting(projectId) {
  // Create temporary SQL file for rate limiting setup
  const rateLimitSqlPath = path.join(__dirname, 'temp_rate_limit.sql');
  const rateLimitSql = `
  -- Apply rate limiting settings
  BEGIN;
    -- Enable the rate-limiter function for all API routes
    INSERT INTO supabase_functions.hooks (hook_table_id, hook_name, hook_function_name)
    VALUES ('http_request_pre', 'rate_limiter_hook', 'rate-limiter')
    ON CONFLICT (hook_table_id, hook_name) DO NOTHING;
    
    -- Ensure the rate-limiter function has the necessary permissions
    GRANT USAGE ON SCHEMA public TO supabase_functions_admin;
    GRANT SELECT, INSERT, UPDATE ON public.rate_limits TO supabase_functions_admin;
  COMMIT;
  `;
  
  fs.writeFileSync(rateLimitSqlPath, rateLimitSql);
  
  try {
    await applySqlFile(projectId, rateLimitSqlPath);
    fs.unlinkSync(rateLimitSqlPath);
    return true;
  } catch (error) {
    fs.unlinkSync(rateLimitSqlPath);
    return false;
  }
}

/**
 * Enable caching in a project
 */
async function enableCaching(projectId) {
  // Create temporary SQL file for caching setup
  const cachingSqlPath = path.join(__dirname, 'temp_caching.sql');
  const cachingSql = `
  -- Apply caching settings
  BEGIN;
    -- Create cache policy triggers if they don't exist
    DO $$ BEGIN
      IF NOT EXISTS (
        SELECT 1 FROM pg_trigger 
        WHERE tgname = 'tournament_cache_invalidation'
      ) THEN
        CREATE TRIGGER tournament_cache_invalidation
          AFTER INSERT OR UPDATE ON tournaments
          FOR EACH ROW EXECUTE FUNCTION invalidate_tournament_cache();
      END IF;
      
      IF NOT EXISTS (
        SELECT 1 FROM pg_trigger 
        WHERE tgname = 'score_cache_invalidation'
      ) THEN
        CREATE TRIGGER score_cache_invalidation
          AFTER INSERT OR UPDATE ON scores
          FOR EACH ROW EXECUTE FUNCTION invalidate_score_cache();
      END IF;
    END $$;
    
    -- Ensure all tables needed for caching are enabled for real-time
    ALTER PUBLICATION supabase_realtime ADD TABLE cache_invalidation_events;
  COMMIT;
  `;
  
  fs.writeFileSync(cachingSqlPath, cachingSql);
  
  try {
    await applySqlFile(projectId, cachingSqlPath);
    fs.unlinkSync(cachingSqlPath);
    return true;
  } catch (error) {
    fs.unlinkSync(cachingSqlPath);
    return false;
  }
}

/**
 * Main function
 */
async function main() {
  try {
    console.log('====== Supabase Environment Synchronization Tool ======');
    
    // Get linked projects
    const projects = await getLinkedProjects();
    if (projects.length === 0) {
      console.error('No linked Supabase projects found. Please link at least one project first.');
      process.exit(1);
    }
    
    // List projects
    console.log('\nLinked Supabase projects:');
    projects.forEach((project, i) => {
      console.log(`${i + 1}. ${project.name} (${project.id})`);
    });
    
    console.log('\nSelect operation:');
    console.log('1. Sync database schema from source to target');
    console.log('2. Sync settings from source to target');
    console.log('3. Deploy edge functions to a project');
    console.log('4. Enable rate limiting for a project');
    console.log('5. Enable caching for a project');
    console.log('6. Exit');
    
    const operation = await question('Enter operation number: ');
    
    switch (operation) {
      case '1': {
        // Sync database schema
        const sourceIndex = await question('Select source project (number): ');
        const targetIndex = await question('Select target project (number): ');
        
        const sourceProject = projects[parseInt(sourceIndex) - 1];
        const targetProject = projects[parseInt(targetIndex) - 1];
        
        if (!sourceProject || !targetProject) {
          console.error('Invalid project selection');
          process.exit(1);
        }
        
        console.log(`\nSyncing database from ${sourceProject.name} to ${targetProject.name}`);
        const confirmation = await question('This will OVERWRITE the target database. Continue? (yes/no): ');
        
        if (confirmation.toLowerCase() !== 'yes') {
          console.log('Operation cancelled');
          process.exit(0);
        }
        
        // Export source database
        const dumpFile = path.join(__dirname, `${sourceProject.id}_dump.sql`);
        const exported = await exportDatabase(sourceProject.id, dumpFile);
        
        if (exported) {
          // Import to target
          const imported = await importDatabase(targetProject.id, dumpFile);
          
          // Clean up
          if (fs.existsSync(dumpFile)) {
            fs.unlinkSync(dumpFile);
          }
          
          if (imported) {
            console.log('✓ Database sync completed successfully');
          } else {
            console.error('× Database sync failed during import');
          }
        } else {
          console.error('× Database sync failed during export');
        }
        break;
      }
      
      case '2': {
        // Sync settings
        const sourceIndex = await question('Select source project (number): ');
        const targetIndex = await question('Select target project (number): ');
        
        const sourceProject = projects[parseInt(sourceIndex) - 1];
        const targetProject = projects[parseInt(targetIndex) - 1];
        
        if (!sourceProject || !targetProject) {
          console.error('Invalid project selection');
          process.exit(1);
        }
        
        console.log(`\nSyncing settings from ${sourceProject.name} to ${targetProject.name}`);
        const confirmation = await question('This will overwrite settings in the target project. Continue? (yes/no): ');
        
        if (confirmation.toLowerCase() !== 'yes') {
          console.log('Operation cancelled');
          process.exit(0);
        }
        
        const synced = await syncSettings(sourceProject.id, targetProject.id);
        if (synced) {
          console.log('✓ Settings sync completed successfully');
        } else {
          console.error('× Settings sync failed');
        }
        break;
      }
      
      case '3': {
        // Deploy edge functions
        const projectIndex = await question('Select target project (number): ');
        const project = projects[parseInt(projectIndex) - 1];
        
        if (!project) {
          console.error('Invalid project selection');
          process.exit(1);
        }
        
        console.log(`\nDeploying edge functions to ${project.name}`);
        const deployed = await deployEdgeFunctions(project.id);
        
        if (deployed) {
          console.log('✓ Edge functions deployed successfully');
        } else {
          console.error('× Edge function deployment failed');
        }
        break;
      }
      
      case '4': {
        // Enable rate limiting
        const projectIndex = await question('Select target project (number): ');
        const project = projects[parseInt(projectIndex) - 1];
        
        if (!project) {
          console.error('Invalid project selection');
          process.exit(1);
        }
        
        console.log(`\nEnabling rate limiting for ${project.name}`);
        const confirmation = await question('This will modify database functions. Continue? (yes/no): ');
        
        if (confirmation.toLowerCase() !== 'yes') {
          console.log('Operation cancelled');
          process.exit(0);
        }
        
        const enabled = await enableRateLimiting(project.id);
        if (enabled) {
          console.log('✓ Rate limiting enabled successfully');
          console.log('Make sure the rate-limiter edge function is deployed!');
        } else {
          console.error('× Failed to enable rate limiting');
        }
        break;
      }
      
      case '5': {
        // Enable caching
        const projectIndex = await question('Select target project (number): ');
        const project = projects[parseInt(projectIndex) - 1];
        
        if (!project) {
          console.error('Invalid project selection');
          process.exit(1);
        }
        
        console.log(`\nEnabling caching for ${project.name}`);
        const confirmation = await question('This will modify database triggers. Continue? (yes/no): ');
        
        if (confirmation.toLowerCase() !== 'yes') {
          console.log('Operation cancelled');
          process.exit(0);
        }
        
        const enabled = await enableCaching(project.id);
        if (enabled) {
          console.log('✓ Caching enabled successfully');
        } else {
          console.error('× Failed to enable caching');
        }
        break;
      }
      
      case '6':
        console.log('Exiting...');
        break;
        
      default:
        console.error('Invalid operation selected');
        break;
    }
    
    rl.close();
  } catch (error) {
    console.error('An error occurred:', error);
    process.exit(1);
  }
}

// Run the main function
main();
