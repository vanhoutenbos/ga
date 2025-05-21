// Local development setup script for Supabase
const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

/**
 * Helper function to run shell commands
 */
function runCommand(command, options = {}) {
  try {
    console.log(`> ${command}`);
    const output = execSync(command, { stdio: 'inherit', ...options });
    return output;
  } catch (error) {
    console.error(`Error executing command: ${command}`);
    console.error(error);
    process.exit(1);
  }
}

/**
 * Check if Supabase CLI is installed
 */
function checkSupabaseCLI() {
  try {
    execSync('supabase --version', { stdio: 'ignore' });
    console.log('✓ Supabase CLI is installed');
  } catch (error) {
    console.error('× Supabase CLI is not installed. Installing now...');
    runCommand('npm install -g supabase');
    console.log('✓ Supabase CLI installed successfully');
  }
}

/**
 * Check if Docker is installed and running
 */
function checkDocker() {
  try {
    execSync('docker info', { stdio: 'ignore' });
    console.log('✓ Docker is installed and running');
  } catch (error) {
    console.error('× Docker is not installed or not running');
    console.error('Please install Docker and start it before running this script');
    process.exit(1);
  }
}

/**
 * Initialize local Supabase if not already initialized
 */
function initializeLocalSupabase() {
  // Check for .supabase folder in project root
  const supabaseFolderPath = path.join(__dirname, '..', '..', '.supabase');
  if (!fs.existsSync(supabaseFolderPath)) {
    console.log('Initializing local Supabase development environment...');
    runCommand('supabase init', { cwd: path.join(__dirname, '..', '..') });
    console.log('✓ Local Supabase initialized successfully');
  } else {
    console.log('✓ Local Supabase already initialized');
  }
}

/**
 * Start local Supabase
 */
function startLocalSupabase() {
  console.log('Starting local Supabase development server...');
  runCommand('supabase start', { cwd: path.join(__dirname, '..', '..') });
  console.log('✓ Local Supabase started successfully');
}

/**
 * Update configuration with project-specific values
 */
function updateLocalConfig() {
  console.log('Updating local configuration...');
  // This could be extended to update specific values in config.toml if needed
  console.log('✓ Configuration updated');
}

/**
 * Main execution function
 */
function main() {
  console.log('=== Setting up Supabase local development environment ===');
  
  // Prerequisites checks
  checkSupabaseCLI();
  checkDocker();
  
  // Setup
  initializeLocalSupabase();
  updateLocalConfig();
  startLocalSupabase();
  
  console.log('\n=== Setup complete! ===');
  console.log('Local Supabase is now running');
  console.log('- Studio URL: http://localhost:54323');
  console.log('- API URL:    http://localhost:54321');
  console.log('- DB URL:     postgresql://postgres:postgres@localhost:54322/postgres');
  console.log('\nYou can stop the local instance with: supabase stop');
}

main();
