# Azure Infrastructure Cost Optimization Plan

This document provides a detailed cost optimization plan for the Golf Tournament Organizer application's Azure infrastructure, with a focus on practical techniques to minimize costs while maintaining performance and reliability.

## Table of Contents
1. [Current Architecture Cost Analysis](#current-architecture-cost-analysis)
2. [Cost Comparison of Hosting Options](#cost-comparison-of-hosting-options)
3. [Recommended Cost Optimization Strategies](#recommended-cost-optimization-strategies)
4. [Seasonal Scaling Strategy](#seasonal-scaling-strategy) 
5. [Monitoring and Cost Management](#monitoring-and-cost-management)
6. [Implementation Roadmap](#implementation-roadmap)

## Current Architecture Cost Analysis

Our current architecture consists of the following Azure components:

| Service | Tier/SKU | Purpose | Estimated Monthly Cost (EUR) |
|---------|----------|---------|------------------------------|
| Azure Static Web App | Standard | Hosting React PWA frontend | €9 (Standard tier) |
| Azure Functions | Consumption | Backend API (.NET 8) | €0-50 (Depending on usage) |
| Azure CDN | Standard | Content delivery | €0-15 (Included with Static Web App) |
| Azure Key Vault | Standard | Secret management | €0-5 (€0.03/10,000 operations) |
| Azure Application Insights | Pay-as-you-go | Monitoring | €0-15 (Based on data volume) |
| Azure Blob Storage | Hot tier | Media and document storage | €0-20 (Based on storage amount) |
| Supabase | Free/Pro | Database, Auth, Realtime | €0 (Free tier) or €25 (Pro tier) |
| **Total Estimated Monthly Cost** | | | **€9-139** |

### Cost Breakdown Analysis

#### Azure Static Web App
- Standard tier: €9/month
- Includes:
  - 250 GB bandwidth
  - 500 GB storage
  - 2 staging environments
  - Custom domains with free SSL
  - Global CDN distribution

#### Azure Functions
- Consumption plan:
  - Free tier: 1 million executions, 400,000 GB-seconds compute
  - Beyond free tier: €0.169 per million executions
  - Memory consumption: €0.000014/GB-second
  - For a typical month outside tournament season: €0-10
  - During tournament months (higher traffic): €20-50

#### Azure CDN
- Included with Static Web App Standard tier
- Additional data transfer costs only in extreme cases (beyond 250 GB/month)

#### Azure Key Vault
- Standard tier: €0.03/10,000 operations
- For typical use patterns: €0-5/month

#### Application Insights
- Pay-as-you-go pricing: €2.30/GB ingested
- With appropriate sampling: €5-15/month

#### Azure Blob Storage
- Hot tier pricing:
  - Storage: €0.0184/GB/month
  - Operations: €0.0043/10,000 operations
  - For typical media storage: €5-20/month

#### Supabase
- Free tier (appropriate for development):
  - 500 MB database
  - 1 GB file storage
  - 2 GB bandwidth
  - 50,000 monthly active users
  - Limited daily email confirmations
- Pro tier (€25/month, recommended for production):
  - 8 GB database
  - 100 GB file storage
  - Unlimited daily email confirmations
  - Dedicated database instance

## Cost Comparison of Hosting Options

Let's compare different hosting options for our architecture components:

### Backend API Hosting Options

| Hosting Option | Pricing Tier | Monthly Cost (EUR) | Pros | Cons |
|----------------|--------------|---------|------|------|
| Azure Functions (Consumption) | Pay-per-use | €0-50 | Scale to zero, pay-per-use | Cold starts |
| Azure Functions (Premium) | EP1 | €150-300 | No cold starts, VNet integration | Higher base cost |
| App Service | Basic B1 | €13 | Simple deployment, no cold starts | Limited scaling |
| App Service | Standard S1 | €70 | Auto-scaling, staging slots | Higher base cost |
| Container Apps | Consumption | €0-50 | Container-based, scale to zero | More complex setup |
| Virtual Machines | B1s | €11 | Full control | Manual scaling, management overhead |
| Virtual Machines | B2s | €43 | More resources | Higher cost, management overhead |

#### Container Apps for Backend Consolidation

For startups looking for both cost efficiency and growth potential, Azure Container Apps deserves special attention:

| Container Apps Option | Configuration | Estimated Cost (EUR) | Best For |
|-----------------------|--------------|----------------------|----------|
| Consumption (0.5 CPU, 1GB) | Scale 0-1 | €0-25 | Initial stage with unpredictable traffic |
| Consumption (1 CPU, 2GB) | Scale 0-2 | €0-45 | Small tournaments with modest traffic |
| Consumption (2 CPU, 4GB) | Scale 0-3 | €0-120 | Medium tournaments with spiky traffic |

While not recommended for the immediate implementation due to the additional complexity, Container Apps offers an excellent future-proof option that combines:
- The cost benefits of scale-to-zero during quiet periods
- The performance benefits of containerization (reduced cold starts)
- The architectural benefit of consolidating multiple backend components

### Frontend Hosting Options

| Hosting Option | Pricing Tier | Monthly Cost (EUR) | Pros | Cons |
|----------------|--------------|---------|------|------|
| Azure Static Web Apps | Standard | €9 | Built-in CDN, GitHub integration | Limited customization |
| Azure Storage + CDN | Static website | €5-15 | Very low cost | Manual setup for CDN, auth |
| App Service | Free | €0 | Simple deployment | Limited scale, no custom domain |
| App Service | Basic B1 | €13 | Staging environments | Higher cost than Static Web Apps |
| Cloudflare Pages | Free | €0 | Free custom domains, CDN | Limited build minutes |

### Database Options

| Hosting Option | Pricing Tier | Monthly Cost (EUR) | Pros | Cons |
|----------------|--------------|---------|------|------|
| Supabase | Free | €0 | Integrated auth, realtime | Limited resources |
| Supabase | Pro | €25 | More resources, daily backups | Still limited for very large scale |
| Azure SQL | Basic | €5 | Native Azure integration | No built-in auth, realtime |
| Azure SQL | Standard S0 | €18 | Better performance | Higher cost |
| Azure PostgreSQL | Basic | €25 | Native PostgreSQL | No built-in auth, realtime |
| Azure Cosmos DB | Serverless | €0-40 | Global distribution | Higher cost for writes |

## Recommended Cost Optimization Strategies

Based on the cost analysis, here are specific optimization strategies:

### 1. Hybrid Function Plan Strategy

Implement a hybrid approach for Azure Functions:
- **Default**: Use Consumption plan (€0-50/month)
- **Tournament days**: Temporarily upgrade to Premium plan EP1 (prorated cost)

Implementation using Azure CLI:
```bash
# Script to upgrade to Premium plan before tournament
az functionapp plan create --name golfapp-premium-plan --resource-group golfapp-rg --location westeurope --sku EP1
az functionapp update --name golfapp-api --resource-group golfapp-rg --plan golfapp-premium-plan

# Script to downgrade back to Consumption plan after tournament
az functionapp plan create --name golfapp-consumption-plan --resource-group golfapp-rg --location westeurope --sku Y1
az functionapp update --name golfapp-api --resource-group golfapp-rg --plan golfapp-consumption-plan
```

**Cost impact**: Reduce Premium plan usage from full month (€150) to only tournament days (~€25-40 for 5-7 days), saving €110-125/month.

### 2. Tiered Storage Strategy

Implement storage lifecycle management:
- **Hot tier**: Active tournament media and documents
- **Cool tier**: Past tournaments (older than 30 days)
- **Archive tier**: Historical data (older than 1 year)

Configuration in Azure Portal or using Azure CLI:
```bash
# Create lifecycle management policy
az storage account management-policy create \
    --account-name golfappstorage \
    --resource-group golfapp-rg \
    --policy @lifecycle-policy.json
```

With this lifecycle policy:
```json
{
  "rules": [
    {
      "name": "MoveToCoolTier",
      "enabled": true,
      "type": "Lifecycle",
      "definition": {
        "filters": {
          "blobTypes": ["blockBlob"],
          "prefixMatch": ["tournaments/media/"]
        },
        "actions": {
          "baseBlob": {
            "tierToCool": {"daysAfterModificationGreaterThan": 30}
          }
        }
      }
    },
    {
      "name": "MoveToArchiveTier",
      "enabled": true,
      "type": "Lifecycle",
      "definition": {
        "filters": {
          "blobTypes": ["blockBlob"],
          "prefixMatch": ["tournaments/media/"]
        },
        "actions": {
          "baseBlob": {
            "tierToArchive": {"daysAfterModificationGreaterThan": 365}
          }
        }
      }
    }
  ]
}
```

**Cost impact**: Reduce storage costs by up to 60% for cool tier data and up to 90% for archive tier data.

### 3. Application Insights Sampling

Configure Application Insights sampling to reduce data ingestion costs:

In `host.json`:
```json
{
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "excludedTypes": "Request",
        "includedTypes": "PageView;Trace",
        "maxTelemetryItemsPerSecond": 5
      }
    }
  }
}
```

**Cost impact**: Reduce telemetry data volume by 60-80%, saving €5-10/month.

### 4. Supabase Optimization

Optimize Supabase usage:
- Start with Free tier for development
- Upgrade to Pro tier only for production
- Implement efficient database queries to minimize resource usage
- Cache frequently accessed data at the application level

**Cost impact**: €0 for development, €25 for production.

### 5. CDN Caching Optimization

Optimize CDN caching rules to maximize cache hit ratio:

```xml
<rule name="StaticContent">
  <match url="^(images|css|js|fonts)/(.*)" />
  <action type="Rewrite" url="{R:0}" />
  <conditions>
    <add input="{HTTP_METHOD}" pattern="^GET$" />
  </conditions>
  <serverVariables>
    <set name="HTTP_X_CACHE_CONTROL" value="public, max-age=604800" />
  </serverVariables>
</rule>
```

**Cost impact**: Reduce origin requests by up to 70%, minimizing bandwidth costs.

### 6. Azure Front Door Integration (Optional)

For larger tournaments with global audience, consider adding Azure Front Door:

- Standard tier: €32.38/month base fee
- Data processing: €0.014/GB
- Outbound data transfer: Region-specific rates

**Cost benefit analysis**:
- For small tournaments: Not cost-effective
- For large international tournaments: Can reduce backend load and improve performance

### 7. Reserved Instances for Predictable Workloads

For components with predictable usage:
- Consider 1-year reserved instances for savings of 20-40%
- Apply only to non-seasonal workloads (e.g., core infrastructure)

**Cost impact**: Potential savings of 20-40% on applicable resources.

## Seasonal Scaling Strategy

Given the seasonal nature of golf tournaments, we recommend a tailored scaling strategy:

### Off-Season Configuration (Most of the Year)

| Service | Tier/Configuration | Monthly Cost (EUR) |
|---------|-------------------|-------------------|
| Static Web App | Standard tier | €9 |
| Functions | Consumption plan | €0-20 |
| Supabase | Pro tier | €25 |
| Supporting services | Minimal configuration | €10-20 |
| **Total Off-Season Cost** | | **€44-74** |

### Tournament Season Configuration (5-7 Days per Tournament)

| Service | Tier/Configuration | Monthly Cost (EUR) |
|---------|-------------------|-------------------|
| Static Web App | Standard tier | €9 |
| Functions | Premium Plan EP1 (prorated) | €25-40 (for 5-7 days) |
| Supabase | Pro tier | €25 |
| Supporting services | Scaled up configuration | €20-30 |
| **Total Tournament Period Cost** | | **€79-104** |

### Automation Script Example

Create a PowerShell script to handle the scaling:

```powershell
param (
    [Parameter(Mandatory=$true)]
    [ValidateSet("TournamentStart", "TournamentEnd")]
    [string]$Action,
    
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup
)

$functionAppName = "golfapp-api"

if ($Action -eq "TournamentStart") {
    Write-Host "Scaling up resources for tournament..."
    
    # Create Premium plan if it doesn't exist
    $premiumPlanExists = az functionapp plan list --query "[?name=='golfapp-premium-plan']" --output tsv
    if (!$premiumPlanExists) {
        az functionapp plan create --name golfapp-premium-plan --resource-group $ResourceGroup --location westeurope --sku EP1
    }
    
    # Move function app to Premium plan
    az functionapp update --name $functionAppName --resource-group $ResourceGroup --plan golfapp-premium-plan
    
    # Scale up other resources as needed
    # ...
    
    Write-Host "Resources scaled up for tournament"
}
elseif ($Action -eq "TournamentEnd") {
    Write-Host "Scaling down resources after tournament..."
    
    # Create Consumption plan if it doesn't exist
    $consumptionPlanExists = az functionapp plan list --query "[?name=='golfapp-consumption-plan']" --output tsv
    if (!$consumptionPlanExists) {
        az functionapp plan create --name golfapp-consumption-plan --resource-group $ResourceGroup --location westeurope --sku Y1
    }
    
    # Move function app to Consumption plan
    az functionapp update --name $functionAppName --resource-group $ResourceGroup --plan golfapp-consumption-plan
    
    # Scale down other resources as needed
    # ...
    
    Write-Host "Resources scaled down after tournament"
}
```

Schedule this script to run before and after tournaments.

## Monitoring and Cost Management

Implement these monitoring tools and practices:

### 1. Azure Cost Management Dashboard

Create a custom Cost Management dashboard with these views:
- Monthly cost trend by service
- Cost by resource tag
- Forecast vs. actual spending
- Anomaly detection alerts

### 2. Custom Cost Alerting

Set up cost alerts:
- Budget alerts at 80% of monthly budget
- Anomaly detection for unexpected spikes
- Daily spending reports during tournaments

Example Azure CLI command:
```bash
az monitor action-group create --name "CostAlertGroup" --resource-group golfapp-rg --short-name "CostAlert" --email-receiver "CostAdmin" "admin@example.com"

az monitor metrics alert create --name "FunctionExecutionCostAlert" --resource-group golfapp-rg --scopes "/subscriptions/{subscription-id}/resourceGroups/golfapp-rg/providers/Microsoft.Web/sites/golfapp-api" --condition "total transactions > 800000 where ApiName includes Function" --description "Alert when function executions approach free tier limit" --evaluation-frequency 15m --window-size 15m --action "/subscriptions/{subscription-id}/resourceGroups/golfapp-rg/providers/Microsoft.Insights/actionGroups/CostAlertGroup"
```

### 3. Resource Tagging Strategy

Implement consistent resource tagging for cost tracking:
- Environment (prod, dev, test)
- Department/Cost center
- Tournament/Project
- Component (frontend, backend, data)

Example tag structure:
```json
{
  "Environment": "Production",
  "CostCenter": "TournamentOps",
  "Tournament": "DutchOpen2023",
  "Component": "Backend" 
}
```

### 4. Regular Cost Reviews

Schedule monthly cost review meetings:
- Review current spending vs. budget
- Identify cost anomalies
- Plan for upcoming tournaments
- Optimize underutilized resources

## Implementation Roadmap

Follow this implementation plan to optimize costs:

### Phase 1: Immediate Optimizations (1-2 Weeks)
- [ ] Configure Application Insights sampling
- [ ] Implement storage lifecycle management
- [ ] Optimize CDN caching rules
- [ ] Set up Azure Cost Management dashboard
- [ ] Implement resource tagging strategy

### Phase 2: Seasonal Strategy Implementation (2-4 Weeks)
- [ ] Develop automation scripts for scaling
- [ ] Test tournament scaling procedures
- [ ] Create documentation for operations team
- [ ] Set up cost alerts and monitoring
- [ ] Implement Function performance optimizations

### Phase 3: Advanced Optimizations (Ongoing)
- [ ] Evaluate reserved instances for stable components
- [ ] Explore Azure Front Door for international tournaments
- [ ] Optimize database queries and structure
- [ ] Implement advanced caching strategies
- [ ] Regular review and refinement
- [ ] Evaluate Azure Container Apps for backend consolidation

## Future Scaling with Azure Container Apps

As the Golf Tournament Organizer application grows beyond its initial user base, Azure Container Apps represents a promising evolution path that balances cost efficiency with operational simplicity.

### Container Apps Overview

Azure Container Apps is a serverless container service that enables:
- Running containerized applications without managing Kubernetes
- Automatic scaling based on HTTP traffic or events
- Scale-to-zero capability for cost optimization
- Built-in service discovery and ingress

### Benefits for Golf Tournament Organizer

| Benefit | Description | Impact |
|---------|-------------|--------|
| **Backend Consolidation** | Combine Node.js API Gateway and .NET Functions into a single deployment unit | Simplified operations, reduced management overhead |
| **Cost Optimization** | Scale to zero during quiet periods while maintaining fast activation | Lower costs outside tournament periods |
| **Cold Start Improvement** | Container-based activation is typically faster than Function cold starts | Better user experience during traffic spikes |
| **Simplified Deployment** | Single container image with all backend components | Streamlined CI/CD pipeline |
| **Easy Microservices Evolution** | Natural path to break out specialized services as needs evolve | Future-proof architecture |

### Cost Comparison

| Metric | Current Architecture | Container Apps | Savings Potential |
|--------|---------------------|----------------|-------------------|
| Base Infrastructure | €22/month (Static Web App + API Gateway) | €9/month (Static Web App only) | €13/month |
| Runtime Costs | €10-50/month (Functions) | €15-40/month (Container instances) | €0-10/month |
| Cold Start Performance | Variable (0.5-3s) | More consistent (0.3-1s) | User experience improvement |
| Development Complexity | Lower | Moderate (container expertise) | Initial investment required |

### Implementation Recommendation

We recommend maintaining the current architecture for the initial launch phase to prioritize simplicity and rapid deployment. As the application matures and usage patterns become established (typically after 6-12 months), a gradual migration to Container Apps can be evaluated.

The initial implementation can follow these steps:

1. **Containerize existing components**:
   ```Dockerfile
   # Example Dockerfile for consolidated backend
   FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
   WORKDIR /app
   
   # Build Function components
   COPY ./function-components/*.csproj ./function-components/
   RUN dotnet restore ./function-components/
   
   # Build API Gateway components
   # ...
   
   # Create runtime image
   FROM mcr.microsoft.com/dotnet/aspnet:8.0
   # Configure for container environment
   # ...
   ```

2. **Test in parallel with existing architecture**

3. **Gradually shift traffic to container-based backend**

This approach maintains stability while providing a path to the benefits of containerization.

## Conclusion

By implementing this cost optimization plan, we estimate potential annual savings of €600-1,200 compared to a static infrastructure approach, while maintaining high performance and reliability for the Golf Tournament Organizer application. The seasonal scaling strategy specifically addresses the unique usage patterns of golf tournaments, ensuring resources are available when needed but not generating costs when not in use.

The addition of Azure Container Apps to our future roadmap provides a clear evolution path as the application matures, offering further cost optimization and operational simplicity benefits when the organization is ready to make that transition.
