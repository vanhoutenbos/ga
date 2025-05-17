# Fixed-Cost Infrastructure Plan

This document outlines options for transitioning from a partially usage-dependent cost model to a fully predictable fixed monthly cost structure for the Golf Tournament Organizer application.

## Current Usage-Dependent Cost Components

In our existing architecture, several components have costs that fluctuate based on usage:

| Component | Current Plan | Cost Variability Factor | Monthly Cost Range (EUR) |
|-----------|--------------|------------------------|--------------------------|
| Azure Functions | Consumption | Function execution count and duration | €10-20 |
| Azure Blob Storage | Pay-as-you-go | Storage volume and transaction count | €3-8 |
| Application Insights | Pay-as-you-go | Telemetry data volume | €5-10 |
| Additional bandwidth | Pay-as-you-go | Data transfer during tournaments | €5-10 |
| **Total Variable Components** | | | **€23-48** |

## Fixed-Cost Alternative Architecture

### Option 1: All-Inclusive Fixed Pricing (Recommended)

This option replaces all variable components with fixed-price alternatives:

| Component | Current Plan | Proposed Plan | Fixed Monthly Cost (EUR) |
|-----------|--------------|--------------|--------------------------|
| Azure Functions | Consumption | App Service Plan B1 (always on) | €13 |
| Azure Static Web App | Standard | Standard | €9 |
| App Service (Node.js) | Basic B1 | Basic B1 | €13 |
| Azure Blob Storage | Pay-as-you-go | 100GB with reserved capacity | €5 |
| Application Insights | Pay-as-you-go | Fixed capacity with data cap | €8 |
| Azure Key Vault | Standard | Standard | €5 |
| Supabase | Free | Free (within limits of 1,000 users) | €0 |
| **Total Fixed Monthly Cost** | | | **€53** |

> **Important Note on Supabase**: We recommend maintaining Supabase on the Free tier for the immediate future. With a projected user base well below 25,000 users and modest database size requirements, there's no immediate need to upgrade to the Pro tier. This provides significant cost savings of €25/month while still meeting all current requirements. See the "Supabase Free Tier Sustainability" section later in this document for more details.

Benefits:
- 100% predictable monthly billing
- No surprises regardless of usage patterns
- Simplified budget planning
- No need to monitor usage metrics for cost control

Drawbacks:
- Potentially paying for unused capacity
- Slightly higher base cost than the minimum variable estimate
- Less automatic scaling during tournaments (would require manual intervention)

### Option 2: Hybrid Model with Reserved Capacity

This approach uses reserved capacity for the most variable components but keeps some pay-as-you-go elements:

| Component | Current Plan | Proposed Plan | Fixed Monthly Cost (EUR) |
|-----------|--------------|--------------|--------------------------|
| Azure Functions | Consumption | Premium EP1 with Reserved Instances | €130 |
| Azure Static Web App | Standard | Standard | €9 |
| App Service (Node.js) | Basic B1 | Basic B1 | €13 |
| Azure Blob Storage | Pay-as-you-go | 100GB with reserved capacity | €5 |
| Application Insights | Pay-as-you-go | Pay-as-you-go (capped) | €5-10 |
| Azure Key Vault | Standard | Standard | €5 |
| Supabase | Free | Free (within limits of 1,000 users) | €0 |
| **Total Monthly Cost** | | | **€167-172** |

Benefits:
- Premium performance for all Functions
- No cold starts ever
- Virtual Network integration capability
- Maximum performance during tournaments without manual intervention
- Reserved capacity for guaranteed availability

Drawbacks:
- Significantly higher cost than current estimate
- Some minor variability remains with Application Insights
- Substantial over-provisioning during quiet periods

### Option 3: Annual Commitment Plan

This option leverages 1-year Azure Reserved Instances for significant discounts:

| Component | Current Plan | Proposed Plan | Monthly Cost with 1-Year Commitment (EUR) |
|-----------|--------------|--------------|------------------------------------------|
| Azure Functions | Consumption | App Service Plan B1 (1-year) | €10.40 (-20%) |
| Azure Static Web App | Standard | Standard | €9 |
| App Service (Node.js) | Basic B1 | Basic B1 (1-year) | €10.40 (-20%) |
| Azure Blob Storage | Pay-as-you-go | 100GB reserved (1-year) | €4 (-20%) |
| Application Insights | Pay-as-you-go | Fixed capacity with data cap | €8 |
| Azure Key Vault | Standard | Standard | €5 |
| Supabase | Free | Free (within limits of 1,000 users) | €0 |
| **Total Fixed Monthly Cost** | | | **€46.80** |

> **Note on Supabase Annual Plan**: While Supabase offers a Pro tier with annual payment at a discount (€22/month vs €25/month), we recommend deferring this expense while the application remains well within free tier limits. This saves €264 annually with no functional impact.

Benefits:
- Lower monthly cost than Option 1
- Still fully predictable billing
- No variability based on usage

Drawbacks:
- Requires upfront annual commitment
- Less flexibility to change architecture
- Potential financial lock-in

## Implementation Considerations

### Migration Path for Option 1 (Recommended)

1. **Azure Functions Migration**:
   - Create new App Service Plan (B1 tier)
   - Deploy Functions to App Service Plan instead of Consumption plan
   - Configure "Always On" setting to prevent idle shutdown
   - Update CI/CD pipelines for the new deployment target

2. **Storage Reconfiguration**:
   - Move from pay-as-you-go to reserved capacity Blob Storage
   - Implement lifecycle management to optimize storage usage within fixed capacity

3. **Monitoring Adjustments**:
   - Configure data caps in Application Insights
   - Implement sampling to ensure staying within fixed allocation
   - Set up alerts for approaching capacity limits

4. **Supabase Upgrade**:
   - Upgrade from Free tier to Pro tier
   - Migrate database and configure settings
   - Test authentication flows with the new instance

## Supabase Free Tier Sustainability

Maintaining Supabase on the free tier represents a significant cost optimization opportunity while providing all necessary functionality for the immediate future.

### Free Tier Limits Analysis

| Resource | Free Tier Limit | Projected Usage (Year 1) | Capacity Utilization |
|----------|----------------|-------------------------|----------------------|
| Monthly Active Users | 50,000 | 1,000 | 2% |
| Database Size | 500MB | ~50-100MB | 10-20% |
| Storage | 1GB | ~200-300MB | 20-30% |
| Daily Database Egress | 2GB | ~50-100MB/day | 2.5-5% |
| Auth Users | Unlimited | 1,000 | N/A |

### Recommended Monitoring Metrics

To ensure we stay within free tier limits:
1. **Database size growth rate**: Monitor monthly to predict when we'll approach the 500MB limit
2. **Active user count**: Track monthly active users vs. the 50,000 limit
3. **Storage utilization**: Implement cleanup policies for temporary files
4. **Database egress**: Watch for unexpected spikes that could approach daily limits

### Upgrade Triggers

We recommend upgrading to Supabase Pro tier (€25/month) only when:
- Reaching 25,000 monthly active users (50% of free tier limit)
- Database approaching 450MB (90% of free tier limit)
- Storage nearing 900MB (90% of free tier limit)

Based on our current growth projections, we expect to remain comfortably within free tier limits for at least 12-18 months, representing a €300-450 cost savings over that period.

## Cost Comparison Summary

| Cost Structure | Monthly Cost (EUR) | Cost Certainty | Performance | Flexibility |
|----------------|-------------------|----------------|-------------|-------------|
| Current Variable Model | €66-109 | Low | Good (with spikes) | High |
| Option 1: All-Inclusive Fixed (with Supabase Free) | €53 | Very High | Good | Medium |
| Option 1: All-Inclusive Fixed (with Supabase Pro) | €78 | Very High | Good | Medium |
| Option 2: Premium Reserved | €167-172 | High | Excellent | Low |
| Option 3: Annual Commitment | €46.80 | Very High | Good | Low |

## Recommendation

We recommend **Option 1: All-Inclusive Fixed Pricing with Supabase Free Tier** as it provides:

1. Complete cost predictability at €53/month
2. Sufficient performance for the application needs
3. Optimal balance between cost and capabilities
4. No long-term commitments that might limit future architecture changes
5. Maximum utilization of free tier resources

For your specific use case with 100 organizers and 900 players with accounts plus unlimited participants without accounts, this fixed-cost model eliminates all uncertainty while keeping costs reasonable at approximately €0.53 per organizer per month.

### Implementation Priority

1. **First Phase** (Immediate):
   - Transition Azure Functions to App Service Plan B1
   - Move to reserved capacity for storage
   - Implement Application Insights data caps
   - Continue with Supabase Free tier

2. **Second Phase** (When needed):
   - Monitor Supabase usage metrics monthly
   - Upgrade to Supabase Pro tier only when approaching free tier limits
   - Consider annual commitment options when usage patterns stabilize

3. **Future Scaling Options** (Growth Phase):
   - Evaluate Azure Container Apps as a consolidated backend solution
   - Consider containerizing both API Gateway and Function logic in a single deployment
   - Benefit from both scale-to-zero cost savings and reduced cold starts
   - Maintain deployment simplicity with integrated CI/CD pipelines

### Azure Container Apps as a Future Alternative

While our immediate recommendation prioritizes simplicity and stability with App Service and Functions, Azure Container Apps (ACA) represents a compelling future option:

| Consideration | Current Approach | Container Apps Approach |
|---------------|-----------------|------------------------|
| Deployment    | Multiple services | Single container image |
| Cold Starts   | Managed via Always On | Reduced with container readiness |
| Pricing Model | Fixed cost | Consumption with scale to zero |
| Operations    | Simple service management | Container orchestration |
| Development   | Standard Azure services | Container expertise required |
| Cost Range    | €53/month fixed | €30-50/month (usage-based) |

Container Apps would allow consolidating the Node.js API Gateway and .NET Functions into a unified deployment model while maintaining the ability to scale to zero during quiet periods. This approach becomes more attractive as the application grows and deployment expertise matures.

If absolute cost minimization is more important than complete predictability, you could remain with the current model which may be cheaper during very low usage periods but carries the risk of higher costs during peak usage.