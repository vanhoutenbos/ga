#!/usr/bin/env node

/**
 * Supabase Project Setup Script
 * 
 * This script automates setting up a Supabase project for the Golf Tournament Organizer.
 * It creates a new project, configures settings, and applies the schema.
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

// Configuration
const CONFIG = {
  projectName: 'golf-tournament-organizer',
  organization: '',  // Will be set by user input
  region: 'us-east-1', // Default region
  plan: 'free',  // Default plan
  configPath: path.join(__dirname, '..', 'config.toml'),
  migrationsDir: path.join(__dirname, '..', 'migrations'),
  seedFile: path.join(__dirname, '..', 'seed', 'development.sql'),
};

/**
 * Helper function to run shell commands
 */
async function runCommand(command, options = {}) {
  try {
    console.log(`> ${command}`);
    const { stdout, stderr } = await exec(command, options);
    console.log(stdout);
    if (stderr) console.error(stderr);
    return stdout;
  } catch (error) {
    console.error(`Error executing command: ${command}`);
    console.error(error.stderr || error.message);
    throw error;
  }
}

/**
 * Check if Supabase CLI is installed
 */
async function checkSupabaseCLI() {
  try {
    await exec('supabase --version');
    console.log('✓ Supabase CLI is installed');
  } catch (error) {
    console.error('× Supabase CLI is not installed. Installing now...');
    await runCommand('npm install -g supabase');
    console.log('✓ Supabase CLI installed successfully');
  }
}

/**
 * Check if the user is logged in to Supabase
 */
async function checkLoginStatus() {
  try {
    const { stdout } = await exec('supabase projects list');
    if (stdout.includes('not logged in')) {
      throw new Error('Not logged in');
    }
    console.log('✓ Already logged in to Supabase');
    return true;
  } catch (error) {
    console.log('× Not logged in to Supabase');
    return false;
  }
}

/**
 * Login to Supabase
 */
async function login() {
  console.log('Please login to Supabase:');
  await runCommand('supabase login');
  console.log('✓ Logged in successfully');
}

/**
 * Get list of organizations
 */
async function getOrganizations() {
  try {
    const { stdout } = await exec('supabase orgs list --json');
    return JSON.parse(stdout);
  } catch (error) {
    console.error('Error getting organizations:', error.message);
    return [];
  }
}

/**
 * Create a new Supabase project
 */
async function createProject(projectName, orgId, region, dbPassword, plan) {
  try {
    console.log(`Creating Supabase project '${projectName}'...`);
    
    const createCommand = `supabase projects create "${projectName}" \
      --org-id "${orgId}" \
      --region "${region}" \
      --db-password "${dbPassword}" \
      ${plan ? `--plan "${plan}"` : ''}`;
      
    const { stdout } = await exec(createCommand);
    
    // Extract project ID from output
    const match = stdout.match(/Created project: ([a-zA-Z0-9-]+)/);
    if (match && match[1]) {
      const projectId = match[1];
      console.log(`✓ Project created successfully with ID: ${projectId}`);
      return projectId;
    } else {
      console.error('× Could not extract project ID from output');
      return null;
    }
  } catch (error) {
    console.error('Error creating project:', error.stderr || error.message);
    return null;
  }
}

/**
 * Update the config.toml file with the project ID
 */
function updateConfig(projectId) {
  try {
    let configContent = fs.readFileSync(CONFIG.configPath, 'utf8');
    configContent = configContent.replace(/project_id = ".*"/, `project_id = "${projectId}"`);
    fs.writeFileSync(CONFIG.configPath, configContent);
    console.log('✓ Updated config.toml with project ID');
  } catch (error) {
    console.error('× Error updating config.toml:', error.message);
  }
}

/**
 * Link local project to the Supabase project
 */
async function linkProject(projectId, dbPassword) {
  try {
    await runCommand(`supabase link --project-ref ${projectId} --password "${dbPassword}"`);
    console.log('✓ Project linked successfully');
    return true;
  } catch (error) {
    console.error('× Error linking project:', error.message);
    return false;
  }
}

/**
 * Apply migrations to the project
 */
async function applyMigrations() {
  try {
    await runCommand('supabase db push');
    console.log('✓ Migrations applied successfully');
    return true;
  } catch (error) {
    console.error('× Error applying migrations:', error.message);
    return false;
  }
}

/**
 * Apply seed data to the project
 */
async function applySeedData() {
  try {
    await runCommand('supabase db reset');
    console.log('✓ Seed data applied successfully');
    return true;
  } catch (error) {
    console.error('× Error applying seed data:', error.message);
    return false;
  }
}

/**
 * Configure auth settings
 */
async function configureAuthSettings(projectId) {
  try {
    // Get current site URL and add to allowed redirect URLs
    const siteUrl = await question('Enter the site URL for auth redirects (e.g. http://localhost:3000, https://your-domain.com): ');
    
    await runCommand(`supabase config set auth.site_url="${siteUrl}" --project-ref ${projectId}`);
    await runCommand(`supabase config set auth.additional_redirect_urls=["${siteUrl}"] --project-ref ${projectId}`);
    
    console.log('✓ Auth settings configured successfully');
    return true;
  } catch (error) {
    console.error('× Error configuring auth settings:', error.message);
    return false;
  }
}

/**
 * Enable realtime
 */
async function enableRealtime(projectId) {
  try {
    // Enable Realtime for all tables
    await runCommand(`supabase config set realtime.enabled=true --project-ref ${projectId}`);
    
    // Execute SQL to enable realtime on specific tables
    const realtimeSQL = `
    BEGIN;
      -- Enable replication for these tables
      ALTER PUBLICATION supabase_realtime ADD TABLE tournaments;
      ALTER PUBLICATION supabase_realtime ADD TABLE tournament_players;
      ALTER PUBLICATION supabase_realtime ADD TABLE flights;
      ALTER PUBLICATION supabase_realtime ADD TABLE scores;
      
      -- Enable replication for leaderboard views
      ALTER PUBLICATION supabase_realtime ADD TABLE stroke_leaderboard;
      ALTER PUBLICATION supabase_realtime ADD TABLE stableford_leaderboard;
      ALTER PUBLICATION supabase_realtime ADD TABLE match_leaderboard;
    COMMIT;
    `;
    
    // Write the SQL to a temporary file
    const tempFile = path.join(__dirname, 'temp_realtime.sql');
    fs.writeFileSync(tempFile, realtimeSQL);
    
    // Execute the SQL
    await runCommand(`supabase db execute --file=${tempFile}`);
    
    // Remove the temporary file
    fs.unlinkSync(tempFile);
    
    console.log('✓ Realtime enabled successfully');
    return true;
  } catch (error) {
    console.error('× Error enabling realtime:', error.message);
    return false;
  }
}

/**
 * Deploy edge functions
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
 * Display project information and credentials
 */
async function showProjectInfo(projectId) {
  try {
    const { stdout } = await exec(`supabase status --project-ref ${projectId}`);
    console.log('\n===== Project Information =====');
    console.log(stdout);
    
    const { stdout: apiKeys } = await exec(`supabase secrets list --project-ref ${projectId}`);
    console.log('\n===== API Keys =====');
    console.log(apiKeys);
    
    return true;
  } catch (error) {
    console.error('× Error getting project information:', error.message);
    return false;
  }
}

/**
 * Main function
 */
async function main() {
  try {
    console.log('====== Golf Tournament Organizer - Supabase Setup ======');
    
    // Check prerequisites
    await checkSupabaseCLI();
    
    // Check login status
    const isLoggedIn = await checkLoginStatus();
    if (!isLoggedIn) {
      await login();
    }
    
    // Get organizations
    const orgs = await getOrganizations();
    if (orgs.length === 0) {
      console.error('No organizations found. Please create an organization on Supabase first.');
      process.exit(1);
    }
    
    // Select organization
    console.log('\nAvailable organizations:');
    orgs.forEach((org, i) => {
      console.log(`${i + 1}. ${org.name} (${org.id})`);
    });
    
    const orgIndex = await question('Select an organization (number): ');
    const selectedOrg = orgs[parseInt(orgIndex) - 1];
    if (!selectedOrg) {
      console.error('Invalid selection');
      process.exit(1);
    }
    
    CONFIG.organization = selectedOrg.id;
    console.log(`Selected organization: ${selectedOrg.name}`);
    
    // Get project name
    const projectName = await question(`Enter project name [${CONFIG.projectName}]: `) || CONFIG.projectName;
    
    // Get region
    const region = await question(`Enter region [${CONFIG.region}]: `) || CONFIG.region;
    
    // Get database password
    const dbPassword = await question('Enter database password (min 8 chars): ');
    if (dbPassword.length < 8) {
      console.error('Password must be at least 8 characters');
      process.exit(1);
    }
    
    // Create project
    const projectId = await createProject(projectName, selectedOrg.id, region, dbPassword, CONFIG.plan);
    if (!projectId) {
      console.error('Failed to create project');
      process.exit(1);
    }
    
    // Update config.toml
    updateConfig(projectId);
    
    // Link project
    const linked = await linkProject(projectId, dbPassword);
    if (!linked) {
      console.error('Failed to link project');
      process.exit(1);
    }
    
    // Apply migrations
    const migrationsApplied = await applyMigrations();
    if (!migrationsApplied) {
      console.error('Failed to apply migrations');
      // Continue anyway as this might be recoverable
    }
    
    // Apply seed data
    const seedApplied = await applySeedData();
    if (!seedApplied) {
      console.error('Failed to apply seed data');
      // Continue anyway as this might be recoverable
    }
    
    // Configure auth settings
    const authConfigured = await configureAuthSettings(projectId);
    if (!authConfigured) {
      console.error('Failed to configure auth settings');
      // Continue anyway as this might be recoverable
    }
    
    // Enable realtime
    const realtimeEnabled = await enableRealtime(projectId);
    if (!realtimeEnabled) {
      console.error('Failed to enable realtime');
      // Continue anyway as this might be recoverable
    }
    
    // Deploy edge functions
    const functionsDeployed = await deployEdgeFunctions(projectId);
    if (!functionsDeployed) {
      console.error('Failed to deploy edge functions');
      // Continue anyway as this might be recoverable
    }
    
    // Show project information
    await showProjectInfo(projectId);
    
    console.log('\n====== Setup Complete ======');
    console.log(`Project URL: https://${projectId}.supabase.co`);
    console.log(`Studio URL: https://app.supabase.com/project/${projectId}`);
    
    rl.close();
  } catch (error) {
    console.error('An error occurred during setup:', error);
    process.exit(1);
  }
}

// Run the main function
main();
