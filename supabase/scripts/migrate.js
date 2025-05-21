// Local development migration script for Supabase
const { execSync } = require('child_process');
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
 * Check if Supabase is running locally
 */
function checkSupabaseRunning() {
  try {
    execSync('supabase status', { stdio: 'ignore' });
    console.log('✓ Supabase is running locally');
    return true;
  } catch (error) {
    console.warn('! Supabase is not running locally');
    return false;
  }
}

/**
 * Apply migrations to local database
 */
function applyMigrations() {
  console.log('Applying database migrations...');
  runCommand('supabase db reset', { cwd: path.join(__dirname, '..', '..') });
  console.log('✓ Migrations applied successfully');
}

/**
 * Create a new migration file
 * @param {string} name - The name of the migration
 */
function createMigration(name) {
  if (!name) {
    console.error('× Migration name is required');
    console.error('Usage: node migration-script.js create <migration-name>');
    process.exit(1);
  }
  
  console.log(`Creating new migration: ${name}`);
  runCommand(`supabase migration new ${name}`, { cwd: path.join(__dirname, '..', '..') });
  console.log('✓ Migration created successfully');
  console.log(`Don't forget to edit the migration file with your changes!`);
}

/**
 * Reset local database and apply all migrations
 */
function resetDatabase() {
  console.log('Resetting local database and applying all migrations...');
  runCommand('supabase db reset', { cwd: path.join(__dirname, '..', '..') });
  console.log('✓ Database reset successfully');
}

/**
 * Main execution function
 */
function main() {
  const command = process.argv[2];
  const arg = process.argv[3];
  
  console.log('=== Supabase Database Migration Tool ===');
  
  // Start Supabase if not running
  const isRunning = checkSupabaseRunning();
  if (!isRunning) {
    console.log('Starting Supabase...');
    runCommand('supabase start', { cwd: path.join(__dirname, '..', '..') });
  }
  
  switch (command) {
    case 'apply':
      applyMigrations();
      break;
    case 'create':
      createMigration(arg);
      break;
    case 'reset':
      resetDatabase();
      break;
    default:
      console.log('Available commands:');
      console.log('  apply  - Apply pending migrations to the local database');
      console.log('  create <name> - Create a new migration file');
      console.log('  reset  - Reset local database and apply all migrations');
      break;
  }
}

main();
