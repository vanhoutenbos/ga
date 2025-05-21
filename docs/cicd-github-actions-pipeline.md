# CI/CD GitHub Actions Pipeline

This document outlines the CI/CD strategy for the Golf Tournament Organizer application using GitHub Actions. The pipeline automates testing, building, and deployment of the application, as well as manages database schema creation and migrations for Supabase using Infrastructure as Code (IaC).

## CI/CD Principles

1. **Automation First**: Minimize manual intervention by automating all repeatable processes
2. **Environment Parity**: Ensure development, staging, and production environments are as similar as possible
3. **Infrastructure as Code**: Define all infrastructure components, including database schema, in version-controlled code
4. **Shift Left**: Catch issues early through automated testing and quality checks
5. **Progressive Deployment**: Deploy changes progressively through environments (dev → staging → production)
6. **Observability**: Integrate monitoring and alerting throughout the pipeline

## Workflow Architecture

### Main Workflows

1. **CI Workflow** (on PR to develop/main)
   - Code linting and style checking
   - Unit and integration tests
   - Static code analysis
   - Build verification
   - Automated accessibility checks
   - Database schema validation

2. **Deployment Workflow** (on push to develop/main)
   - Environment-specific builds
   - Database migrations
   - Frontend deployment
   - Azure Functions deployment
   - Post-deployment tests
   - Status notifications

3. **Supabase Schema Management Workflow**
   - Database schema creation
   - Schema migration execution
   - Data seeding for development/testing
   - Schema documentation generation

## Supabase Database Schema Automation

We'll use the Supabase CLI with GitHub Actions to automate the database schema management process. This approach ensures:

1. The schema is version controlled
2. Changes are applied consistently across environments
3. The database schema can be recreated from scratch if needed
4. Migrations are applied in the correct order

### Folder Structure for Supabase IaC

```
supabase/
├── migrations/                 # Database migrations
│   ├── 20250501000000_initial_schema.sql
│   └── 20250515000000_add_player_handicap.sql
├── functions/                  # Database functions
├── triggers/                   # Database triggers
├── seed/                       # Seed data
│   ├── development.sql
│   └── test.sql
├── policies/                   # RLS policies
│   ├── tournaments.sql
│   └── scores.sql
└── config.toml                 # Supabase project configuration
```

### Workflow Steps for Supabase Management

1. **Setup**: Install Supabase CLI and authenticate with Supabase
2. **Validation**: Validate SQL migrations for syntax errors
3. **Local Testing** (PR): Apply migrations to a local/test database
4. **Migration Execution**: Apply migrations to the target environment
5. **Seeding** (Non-production): Apply seed data for development/testing
6. **Verification**: Verify the database schema is correct

### Security Considerations

1. **Credentials Management**: Store Supabase credentials securely in GitHub Secrets
2. **Environment Isolation**: Use different Supabase projects for different environments
3. **Access Control**: Limit workflow permissions to only what's necessary

## GitHub Actions Workflow Implementation

The workflow files are stored in the `.github/workflows/` directory:

1. **ci.yml**: Runs on pull requests to validate code and database changes
2. **deploy-dev.yml**: Deploys to development environment on push to develop
3. **deploy-staging.yml**: Deploys to staging environment on push to release branch
4. **deploy-prod.yml**: Deploys to production environment on push to main
5. **supabase-db-migrations.yml**: Manages Supabase database migrations

## Environment Configuration

Each environment (development, staging, production) has its own configuration stored in GitHub Secrets:

1. **Development Environment**:
   - SUPABASE_URL_DEV
   - SUPABASE_ANON_KEY_DEV
   - SUPABASE_SERVICE_ROLE_KEY_DEV
   - AZURE_FUNCTIONS_DEPLOYMENT_TOKEN_DEV

2. **Staging Environment**:
   - SUPABASE_URL_STAGING
   - SUPABASE_ANON_KEY_STAGING
   - SUPABASE_SERVICE_ROLE_KEY_STAGING
   - AZURE_FUNCTIONS_DEPLOYMENT_TOKEN_STAGING

3. **Production Environment**:
   - SUPABASE_URL_PROD
   - SUPABASE_ANON_KEY_PROD
   - SUPABASE_SERVICE_ROLE_KEY_PROD
   - AZURE_FUNCTIONS_DEPLOYMENT_TOKEN_PROD

## Workflow Triggers

1. **CI Workflow**: Triggered on pull requests to develop and main branches
2. **Development Deployment**: Triggered on push to develop branch
3. **Staging Deployment**: Triggered on push to release/* branches
4. **Production Deployment**: Triggered on push to main branch
5. **Database Migrations**: 
   - Validation: Triggered on pull requests with changes to supabase/migrations
   - Execution: Triggered as part of deployment workflows

## Rollback Strategy

In case of issues with a deployment or database migration:

1. **Deploy Previous Version**: Trigger a workflow to deploy the previous working version
2. **Database Rollback**: Apply down migrations using Supabase CLI
3. **Emergency Rollback**: Manual intervention procedure documented for critical issues

## Monitoring and Notifications

The CI/CD pipeline integrates with monitoring and notification systems:

1. **Slack Notifications**: Send deployment status updates to team Slack channel
2. **Email Alerts**: Send failure notifications to development team
3. **GitHub Status Checks**: Display workflow status in GitHub PR interface
4. **Application Insights**: Track deployment-related metrics

## Scheduled Maintenance and Cleanup

1. **Dependency Updates**: Weekly workflow to update dependencies and create PRs
2. **Cache Cleanup**: Monthly cleanup of GitHub Actions caches
3. **Database Maintenance**: Weekly schema statistics update and vacuum

## Getting Started

To use this CI/CD pipeline:

1. Ensure you have set up all required GitHub Secrets for your environments
2. Organize your Supabase migrations in the supabase/migrations directory
3. Create feature branches from develop for new features
4. Submit pull requests to develop for CI validation
5. Merge to develop to deploy to development environment
6. Create release branches from develop for staging deployment
7. Merge to main for production deployment

## Conclusion

This CI/CD pipeline ensures a streamlined, automated workflow for the Golf Tournament Organizer application, with a particular focus on managing Supabase database schema using Infrastructure as Code principles. By following this approach, we maintain consistency across environments and reduce the risk of manual errors.
