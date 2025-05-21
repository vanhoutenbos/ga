# DevOps Environment Separation

This document outlines our approach to environment separation for the Golf Tournament Organizer application, with particular focus on how we manage development, staging, and production environments across both our custom code and Supabase resources.

## Environment Architecture Overview

Our application uses a complete separation of environments to ensure quality, stability, and security:

| Environment | Purpose | Access | Data Sensitivity |
|-------------|---------|--------|-----------------|
| **Development** | Active development, feature work | All developers | Synthetic data only |
| **Staging** | Pre-production testing, UAT | Development team, QA, Product | Anonymized data |
| **Production** | Live application | Limited operations team | Full user data |

## Supabase Resources Separation

### 1. Independent Supabase Projects

Each environment has its own dedicated Supabase project to ensure complete isolation:

| Environment | Supabase Project | Database | Storage |
|-------------|-----------------|----------|---------|
| Development | golfapp-dev | Dedicated | Dedicated |
| Staging | golfapp-staging | Dedicated | Dedicated |
| Production | golfapp-prod | Dedicated with enhanced backup | Dedicated with replication |

### 2. Supabase Configuration Management

We manage Supabase configuration across environments using infrastructure as code:

```typescript
// scripts/supabase/setup-environment.ts
import { createClient } from '@supabase/supabase-js'
import { readFileSync } from 'fs'
import { parse } from 'yaml'

async function configureEnvironment(environment: 'dev' | 'staging' | 'prod') {
  // Load environment-specific configuration
  const config = parse(readFileSync(`./config/${environment}.yaml`, 'utf8'))
  
  // Initialize Supabase admin client
  const supabase = createClient(
    config.supabaseUrl,
    config.serviceRoleKey,
    { auth: { persistSession: false } }
  )
  
  // Configure authentication settings
  await supabase.auth.admin.updateConfig({
    site_url: config.siteUrl,
    additional_redirect_urls: config.redirectUrls,
    jwt_expiry: config.jwtExpiry,
    email_template_forgot_password: config.emailTemplates.forgotPassword,
    email_template_magic_link: config.emailTemplates.magicLink
  })
  
  // Set storage configuration
  await configureStorage(supabase, config.storage)
  
  // Set custom email service if specified
  if (config.emailService) {
    await supabase.functions.invoke('setup-email-provider', {
      body: {
        provider: config.emailService.provider,
        config: config.emailService.config
      }
    })
  }
  
  console.log(`✅ Environment ${environment} configured successfully`)
}

// Run the configuration
const environment = process.argv[2] as 'dev' | 'staging' | 'prod'
configureEnvironment(environment)
  .catch(console.error)
```

### 3. Database Schema Management

Database schemas are version controlled and applied to each environment:

```bash
# scripts/db/migrate.sh
#!/bin/bash

ENV=$1
CONFIG_FILE="./config/${ENV}.yaml"

# Read configuration
SUPABASE_URL=$(yq eval '.supabaseUrl' $CONFIG_FILE)
SERVICE_KEY=$(yq eval '.serviceRoleKey' $CONFIG_FILE)

# Run migrations using Supabase CLI
echo "Running migrations for ${ENV} environment..."
supabase db push \
  --db-url "postgresql://postgres:${SERVICE_KEY}@db.${SUPABASE_URL}:5432/postgres" \
  --migrations-dir "./migrations" \
  --debug
```

## Frontend Environment Configuration

### 1. Environment-Specific Build Configuration

We use environment variables and build configurations to target the correct backends:

```typescript
// src/lib/config.ts
export const getEnvironmentConfig = () => {
  switch (process.env.REACT_APP_ENV) {
    case 'production':
      return {
        supabaseUrl: process.env.REACT_APP_SUPABASE_URL,
        supabaseAnonKey: process.env.REACT_APP_SUPABASE_ANON_KEY,
        apiBaseUrl: process.env.REACT_APP_API_BASE_URL,
        sentryDsn: process.env.REACT_APP_SENTRY_DSN,
        features: {
          socialAuth: true,
          paymentProcessing: true,
          analytics: true
        }
      };
    case 'staging':
      return {
        supabaseUrl: process.env.REACT_APP_SUPABASE_URL,
        supabaseAnonKey: process.env.REACT_APP_SUPABASE_ANON_KEY,
        apiBaseUrl: process.env.REACT_APP_API_BASE_URL,
        sentryDsn: process.env.REACT_APP_SENTRY_DSN,
        features: {
          socialAuth: true,
          paymentProcessing: true,
          analytics: false
        }
      };
    default:
      // Development environment defaults
      return {
        supabaseUrl: 'https://abcdefghijklmnopqrst.supabase.co',
        supabaseAnonKey: 'public-anon-key',
        apiBaseUrl: 'http://localhost:7071',
        sentryDsn: null,
        features: {
          socialAuth: false,
          paymentProcessing: false,
          analytics: false
        }
      };
  }
};
```

### 2. Environment Indicators

Visual indicators for non-production environments to prevent confusion:

```tsx
// src/components/layout/EnvironmentBanner.tsx
const EnvironmentBanner: React.FC = () => {
  const env = process.env.REACT_APP_ENV;
  
  if (env === 'production') return null;
  
  const bannerStyles = {
    backgroundColor: env === 'staging' ? '#f59e0b' : '#3b82f6',
    color: 'white',
    padding: '0.25rem',
    textAlign: 'center' as const,
    fontWeight: 'bold' as const,
    fontSize: '0.75rem',
  };
  
  return (
    <div style={bannerStyles}>
      {env === 'staging' ? 'STAGING ENVIRONMENT' : 'DEVELOPMENT ENVIRONMENT'}
    </div>
  );
};
```

## Azure Functions Environment Configuration

### 1. Separate Function Apps by Environment

Each environment has a dedicated Azure Function App:

```
- golfapp-functions-dev
- golfapp-functions-staging
- golfapp-functions-prod
```

### 2. Environment-Specific Configuration

Environment variables managed through Azure App Configuration:

```csharp
// GolfApp.Api/Startup.cs
public override void Configure(IFunctionsHostBuilder builder)
{
    var configuration = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .AddAzureAppConfiguration(Environment.GetEnvironmentVariable("AppConfigConnectionString"))
        .Build();
        
    builder.Services.AddSingleton<IConfiguration>(configuration);
    
    // Register environment-specific services
    var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (environmentName == "Development")
    {
        builder.Services.AddDevSpecificServices();
    }
    else if (environmentName == "Staging")
    {
        builder.Services.AddStagingSpecificServices();
    }
    else
    {
        builder.Services.AddProdSpecificServices();
    }
    
    // Register common services
    builder.Services.AddCoreServices(configuration);
}
```

### 3. Supabase Connection Configuration

Environment-specific Supabase connections in Azure Functions:

```csharp
// GolfApp.Api/Services/SupabaseConnectionFactory.cs
public class SupabaseConnectionFactory : ISupabaseConnectionFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SupabaseConnectionFactory> _logger;
    
    public SupabaseConnectionFactory(IConfiguration configuration, ILogger<SupabaseConnectionFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }
    
    public Task<Supabase.Client> CreateClientAsync()
    {
        var url = _configuration["Supabase:Url"];
        var key = _configuration["Supabase:ServiceKey"];
        
        _logger.LogInformation("Creating Supabase client for {Environment}", 
            _configuration["ASPNETCORE_ENVIRONMENT"]);
        
        var options = new Supabase.SupabaseOptions
        {
            AutoConnectRealtime = false
        };
        
        return Supabase.Client.InitializeAsync(url, key, options);
    }
}
```

## CI/CD Pipeline Environment Strategy

### 1. Separate Deployment Workflows

Each environment has a dedicated deployment workflow:

```yaml
# .github/workflows/deploy-dev.yml
name: Deploy to Development

on:
  push:
    branches: [develop]

jobs:
  deploy-frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      # Configuration and build steps
      - name: Build frontend
        run: npm run build:dev
      - name: Deploy to Dev Storage
        uses: azure/cli@v1
        with:
          inlineScript: |
            az storage blob upload-batch -s ./build -d '$web' --account-name golfappdevstg

  deploy-functions:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      # Build and deployment steps
      - name: Deploy Functions
        uses: Azure/functions-action@v1
        with:
          app-name: 'golfapp-functions-dev'
          package: './api/publish'
```

### 2. Database Migration Safety

Environment-specific migration protections:

```yaml
# .github/workflows/db-migrations.yml
name: Database Migrations

on:
  push:
    branches:
      - develop
      - staging
      - main
    paths:
      - 'migrations/**'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Setup Supabase CLI
        run: npm install -g supabase@latest
        
      - name: Determine environment
        id: env
        run: |
          if [[ $GITHUB_REF == 'refs/heads/main' ]]; then
            echo "::set-output name=name::production"
            echo "::set-output name=requires_approval::true"
          elif [[ $GITHUB_REF == 'refs/heads/staging' ]]; then
            echo "::set-output name=name::staging"
            echo "::set-output name=requires_approval::true"
          else
            echo "::set-output name=name::development"
            echo "::set-output name=requires_approval::false"
          fi
          
      # Validate migrations using dry-run
      - name: Validate migrations
        run: ./scripts/db/validate-migrations.sh ${{ steps.env.outputs.name }}
        
  deploy:
    needs: validate
    runs-on: ubuntu-latest
    environment: ${{ steps.env.outputs.name }}
    steps:
      # Apply migrations based on environment
      - name: Apply migrations
        if: ${{ steps.env.outputs.requires_approval == 'false' || github.event.inputs.confirmed == 'true' }}
        run: ./scripts/db/migrate.sh ${{ steps.env.outputs.name }}
```

## Environment Data Management

### 1. Data Isolation

Complete data isolation between environments:

```typescript
// scripts/db/seed-environment.ts
async function seedEnvironment(environment: string) {
  const config = loadConfig(environment);
  const supabase = createClient(config.supabaseUrl, config.serviceRoleKey);
  
  console.log(`Seeding ${environment} environment...`);
  
  // Clear existing seed data
  await clearSeedData(supabase);
  
  // Load appropriate dataset
  const dataset = environment === 'development' 
    ? require('./data/dev-seed-data.json')
    : require('./data/staging-seed-data.json');
    
  // Insert seed data
  await insertSeedData(supabase, dataset);
  
  console.log(`✅ Successfully seeded ${environment} environment`);
}
```

### 2. Production Data Protection

For staging environment, we anonymize production data:

```typescript
// scripts/db/anonymize-data.ts
async function anonymizeProductionData() {
  const prodConfig = loadConfig('production');
  const stagingConfig = loadConfig('staging');
  
  // Create database dump from production
  console.log('Creating production database dump...');
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  const dumpFile = `./tmp/prod-dump-${timestamp}.sql`;
  
  execSync(`pg_dump "${prodConfig.databaseUrl}" > ${dumpFile}`);
  
  // Anonymize sensitive data
  console.log('Anonymizing sensitive data...');
  let dumpContent = readFileSync(dumpFile, 'utf8');
  
  // Replace emails with anonymized versions
  dumpContent = dumpContent.replace(
    /([a-zA-Z0-9._%+-]+)@([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})/g, 
    (match, username, domain) => `anon_${hashText(match)}@example.com`
  );
  
  // Anonymize names
  dumpContent = dumpContent.replace(
    /'(first_name|last_name)',(.*?):'([^']+)'/g,
    (match, field, separator, name) => 
      `'${field}',${separator}:'${field === 'first_name' ? 'User' : hashText(name).substr(0, 8)}'`
  );
  
  // Anonymize phone numbers
  dumpContent = dumpContent.replace(
    /'phone_number',(.*?):'(\+?[0-9\s-]+)'/g,
    (match, separator) => `'phone_number',${separator}:'+1555${Math.floor(1000000 + Math.random() * 9000000)}'`
  );
  
  // Write anonymized dump
  const anonymizedDumpFile = `./tmp/anonymized-dump-${timestamp}.sql`;
  writeFileSync(anonymizedDumpFile, dumpContent);
  
  // Import to staging
  console.log('Importing anonymized data to staging...');
  execSync(`psql "${stagingConfig.databaseUrl}" < ${anonymizedDumpFile}`);
  
  console.log('✅ Successfully imported anonymized production data to staging');
}
```

## Monitoring and Alerting

### 1. Environment-Specific Monitoring

Each environment has dedicated monitoring resources:

```typescript
// src/lib/monitoring.ts
export const initializeMonitoring = () => {
  const config = getEnvironmentConfig();
  
  // Initialize Application Insights
  if (config.applicationInsightsKey) {
    const appInsights = new ApplicationInsights({
      config: {
        instrumentationKey: config.applicationInsightsKey,
        enableAutoRouteTracking: true,
        enableRequestTrackingTelemetryModule: true,
      }
    });
    
    appInsights.loadAppInsights();
    appInsights.trackPageView();
    
    // Add environment tag to all telemetry
    appInsights.addTelemetryInitializer((envelope) => {
      envelope.tags = envelope.tags || {};
      envelope.tags["ai.cloud.role"] = `golf-app-frontend-${process.env.REACT_APP_ENV}`;
    });
  }
  
  // Initialize Sentry for error reporting
  if (config.sentryDsn) {
    Sentry.init({
      dsn: config.sentryDsn,
      environment: process.env.REACT_APP_ENV,
      tracesSampleRate: process.env.REACT_APP_ENV === 'production' ? 0.1 : 1.0,
    });
  }
};
```

### 2. Alert Sensitivity by Environment

Different alert thresholds by environment:

```typescript
// infra/monitoring/alert-config.ts
export const getAlertConfig = (environment: string) => {
  switch (environment) {
    case 'production':
      return {
        responseTimeThresholdMs: 1000,
        errorRateThreshold: 0.01, // 1%
        availabilityThreshold: 0.995, // 99.5%
        notificationChannels: ['email', 'sms', 'teams'],
        throttlingEnabled: false,
      };
    case 'staging':
      return {
        responseTimeThresholdMs: 2000,
        errorRateThreshold: 0.05, // 5%
        availabilityThreshold: 0.98, // 98%
        notificationChannels: ['email', 'teams'],
        throttlingEnabled: true,
      };
    default: // Development
      return {
        responseTimeThresholdMs: 3000,
        errorRateThreshold: 0.1, // 10%
        availabilityThreshold: 0.95, // 95%
        notificationChannels: ['teams'],
        throttlingEnabled: true,
      };
  }
};
```

## Security Configuration By Environment

### 1. Authentication Restrictions

Stricter security in production:

```typescript
// config/security-settings.ts
export const getSecuritySettings = (environment: string) => {
  const base = {
    passwordMinLength: 8,
    passwordRequireSpecialChar: true,
    passwordRequireNumber: true,
    sessionTimeoutMinutes: 60,
  };
  
  if (environment === 'production') {
    return {
      ...base,
      mfaEnabled: true,
      mfaRequiredForAdmins: true,
      passwordMinLength: 12,
      sessionTimeoutMinutes: 30,
      securityHeaders: {
        strictTransportSecurity: {
          maxAge: 63072000, // 2 years
          includeSubdomains: true,
          preload: true
        },
        contentSecurityPolicy: "default-src 'self'; script-src 'self'; object-src 'none';",
        xFrameOptions: 'DENY',
        xContentTypeOptions: 'nosniff',
      }
    };
  }
  
  if (environment === 'staging') {
    return {
      ...base,
      mfaEnabled: true,
      mfaRequiredForAdmins: false,
      securityHeaders: {
        strictTransportSecurity: {
          maxAge: 31536000, // 1 year
          includeSubdomains: true,
          preload: false
        },
        contentSecurityPolicy: "default-src 'self'; script-src 'self' 'unsafe-inline';",
        xFrameOptions: 'SAMEORIGIN',
        xContentTypeOptions: 'nosniff',
      }
    };
  }
  
  // Development - more relaxed for easier testing
  return {
    ...base,
    mfaEnabled: false,
    mfaRequiredForAdmins: false,
    securityHeaders: {
      strictTransportSecurity: null, // No HSTS in dev
      contentSecurityPolicy: null, // No CSP in dev
      xFrameOptions: 'SAMEORIGIN',
      xContentTypeOptions: 'nosniff',
    }
  };
};
```

### 2. Rate Limiting Configuration

```csharp
// GolfApp.Api/Middleware/RateLimitingMiddleware.cs
public class RateLimitingConfigProvider : IRateLimitingConfigProvider
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    
    public RateLimitingConfigProvider(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }
    
    public RateLimitingConfig GetConfig()
    {
        var config = new RateLimitingConfig();
        
        switch (_environment.EnvironmentName)
        {
            case "Production":
                config.RequestsPerMinute = 60;
                config.RequestsPerHour = 1000;
                config.IpWhitelistEnabled = true;
                break;
            
            case "Staging":
                config.RequestsPerMinute = 120;
                config.RequestsPerHour = 2000;
                config.IpWhitelistEnabled = true;
                break;
            
            default: // Development
                config.RequestsPerMinute = 300;
                config.RequestsPerHour = 5000;
                config.IpWhitelistEnabled = false;
                break;
        }
        
        return config;
    }
}
```

## Environment-Specific Feature Flags

We use feature flags to control feature availability across environments:

```typescript
// src/lib/features.ts
type FeatureFlag = {
  name: string;
  enabledIn: Array<'development' | 'staging' | 'production'>;
  description: string;
};

const FEATURE_FLAGS: FeatureFlag[] = [
  {
    name: 'enhanced-scoring',
    enabledIn: ['development', 'staging'],
    description: 'Enhanced scoring with detailed statistics',
  },
  {
    name: 'team-tournaments',
    enabledIn: ['development'],
    description: 'Support for team-based tournaments',
  },
  {
    name: 'multi-round-tournaments',
    enabledIn: ['development', 'staging', 'production'],
    description: 'Support for tournaments with multiple rounds',
  },
  {
    name: 'mobile-scoring-app',
    enabledIn: ['development', 'staging'],
    description: 'Mobile app for score entry',
  }
];

export const isFeatureEnabled = (featureName: string): boolean => {
  const currentEnv = process.env.REACT_APP_ENV || 'development';
  const feature = FEATURE_FLAGS.find(f => f.name === featureName);
  
  if (!feature) {
    console.warn(`Feature "${featureName}" is not defined in feature flags`);
    return false;
  }
  
  return feature.enabledIn.includes(currentEnv as any);
};
```

## Access Control by Environment

Different access control for different environments:

```typescript
// scripts/supabase/configure-access-control.ts
async function configureAccessControl(environment: string) {
  const config = loadConfig(environment);
  const supabase = createClient(config.supabaseUrl, config.serviceRoleKey);
  
  // Base policies that apply to all environments
  const basePolicies = [
    // ... common policies
  ];
  
  // Environment specific policies
  let environmentPolicies = [];
  
  if (environment === 'development') {
    // More permissive policies for development
    environmentPolicies = [
      {
        name: 'dev_allow_all_select',
        table: 'tournaments',
        definition: 'true',
        operation: 'SELECT',
        check: null,
      },
      // More development-specific policies
    ];
  } else if (environment === 'staging') {
    // Moderate policies for staging
    environmentPolicies = [
      // Staging-specific policies
    ];
  } else {
    // Strict policies for production
    environmentPolicies = [
      // Production-specific policies
    ];
  }
  
  // Apply all policies
  const allPolicies = [...basePolicies, ...environmentPolicies];
  
  for (const policy of allPolicies) {
    // Apply policy using Supabase API or SQL
  }
  
  console.log(`✅ Access control configured for ${environment}`);
}
```

## Environment Testing Strategy

1. **Integration Testing**: Environment-specific integration test suites:

```typescript
// tests/integration/test-runner.ts
async function runIntegrationTests() {
  const environment = process.env.TEST_ENVIRONMENT || 'development';
  const config = loadConfig(environment);
  
  console.log(`Running integration tests against ${environment} environment`);
  
  // Configure test client
  const supabase = createClient(config.supabaseUrl, config.apiKey);
  
  // Determine which tests to run
  const testSuites = ['common', environment];
  
  // Run all applicable test suites
  for (const suite of testSuites) {
    console.log(`Running test suite: ${suite}`);
    await import(`./suites/${suite}.test.ts`).then(module => {
      module.default(supabase, config);
    });
  }
}
```

2. **End-to-End Testing**: Environment routing for Cypress tests:

```javascript
// cypress/support/commands.js
Cypress.Commands.add('selectEnvironment', (environment = 'development') => {
  const envUrls = {
    development: 'http://localhost:3000',
    staging: 'https://staging.golfapp.example.com',
    production: 'https://golfapp.example.com'
  };
  
  const url = envUrls[environment] || envUrls.development;
  cy.visit(url);
  
  // Set environment in local storage for test reference
  cy.window().then(win => {
    win.localStorage.setItem('cypress:environment', environment);
  });
});
```

## Disaster Recovery by Environment

Different backup and recovery strategies by environment:

```typescript
// scripts/backup/configure-backup.ts
async function configureBackupStrategy(environment: string) {
  const config = loadConfig(environment);
  
  if (environment === 'production') {
    // Production - most robust backup strategy
    return {
      fullBackupFrequency: 'daily',
      incrementalBackupFrequency: 'hourly',
      pointInTimeRecovery: true,
      retentionPeriod: 30, // days
      offSiteBackup: true,
      automaticFailover: true
    };
  } else if (environment === 'staging') {
    // Staging - moderate backup strategy
    return {
      fullBackupFrequency: 'daily',
      incrementalBackupFrequency: '12hours',
      pointInTimeRecovery: true,
      retentionPeriod: 7, // days
      offSiteBackup: false,
      automaticFailover: false
    };
  } else {
    // Development - basic backup strategy
    return {
      fullBackupFrequency: 'weekly',
      incrementalBackupFrequency: 'daily',
      pointInTimeRecovery: false,
      retentionPeriod: 3, // days
      offSiteBackup: false,
      automaticFailover: false
    };
  }
}
```

## Cost Management by Environment

```typescript
// scripts/monitoring/cost-alerts.ts
async function configureCostAlerts(environment: string) {
  const config = loadConfig(environment);
  
  // Set spending limits based on environment
  let monthlyBudget = 0;
  let alertThresholds = [];
  
  switch (environment) {
    case 'production':
      monthlyBudget = 500; // $500/month
      alertThresholds = [0.5, 0.8, 0.9, 1.0]; // 50%, 80%, 90%, 100%
      break;
    case 'staging':
      monthlyBudget = 200; // $200/month
      alertThresholds = [0.5, 0.9, 1.0]; // 50%, 90%, 100%
      break;
    default: // development
      monthlyBudget = 50; // $50/month
      alertThresholds = [0.8, 1.0]; // 80%, 100%
      break;
  }
  
  // Configure cost alerts through infrastructure
  // This is cloud provider specific
}
```

## Conclusion

Our environment separation strategy ensures that development, testing, and production workloads remain isolated while following a consistent deployment pipeline. This approach minimizes risk during development and deployment cycles while maximizing productivity across different development phases.

By maintaining separate Supabase projects for each environment, we ensure complete data isolation and can tailor security, performance, and feature availability to the specific needs of each environment. This comprehensive separation strategy provides a solid foundation for our development practices and ensures we can deliver quality features safely to production.
