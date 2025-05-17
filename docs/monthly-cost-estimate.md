# Monthly Cost Estimate: Golf Tournament Organizer

This document provides a comprehensive cost estimate for the Golf Tournament Organizer application during its starting period, with projections for future growth phases.

## Executive Summary

Based on our current architecture using Azure services for hosting, .NET 8 backend APIs integrated with Supabase, and a Node.js Express API gateway, we estimate the following monthly costs:

| Deployment Stage | Monthly Cost Range (EUR) | User Base |
|------------------|-------------------------|-----------|
| **Initial Launch** | **€51-94** | 1,000 users (100 organizers, 900 players) |
| **Early Growth** | €100-150 | 5,000+ users |
| **Established** | €150-250 | 10,000+ users |

The cost model is highly efficient during the starting period due to our serverless architecture and strategic use of free tiers. Costs scale proportionally with usage, ensuring you only pay for what you use.

## Detailed Cost Breakdown - Initial Launch Phase

### Core Infrastructure Components

| Service | Tier/Configuration | Monthly Cost (EUR) | Notes |
|---------|-------------------|-------------------|-------|
| **Azure Static Web App** | Standard | €9 | Includes CDN, custom domains, SSL |
| **Azure Functions (.NET 8)** | Consumption Plan | €10-20 | For custom business logic and complex operations |
| **Azure App Service (Node.js)** | Basic B1 | €13 | For API Gateway/middleware layer |
| **Supabase** | Free Tier | €0 | Sufficient for initial 1,000 users (100 organizers + 900 players) |
| **Azure Key Vault** | Standard | €0-5 | Pay-per-operation model |
| **Azure Application Insights** | Pay-as-you-go | €5-10 | With sampling enabled |
| **Azure Blob Storage** | Hot tier (minimal usage) | €3-8 | For tournament media and documents |
| **Azure CDN** | Included with Static Web App | €0 | No additional cost |
| **Total Monthly Core Infrastructure** | | **€40-65** | |

> **Note on Supabase Free Tier**: As the application will initially support a maximum of 1,000 users (100 organizers and 900 players), the Supabase Free tier with limits of 50,000 MAU, 500MB database, and 1GB storage is more than sufficient. This allows us to maintain €0 cost for database services during the initial launch phase. We would only need to upgrade to the Pro tier (€25/month) when approaching 25,000 users or 450MB database size, which is significantly beyond our immediate target scope.

### Additional Costs During Tournament Periods

During active tournament days (assuming 1-2 tournaments in the first month):

| Service | Tier/Configuration | Additional Cost (EUR) | Notes |
|---------|-------------------|-------------------|-------|
| **Azure Functions** | Premium Plan EP1 (2 days) | €10-15 | Temporary upgrade for tournament days only |
| **Increased Storage & Bandwidth** | - | €5-10 | Media uploads, leaderboard traffic |
| **Additional Monitoring** | - | €2-5 | Enhanced logging during tournaments |
| **Total Additional Tournament Costs** | | **€17-30** | |

### Development & Testing Environment

| Service | Tier/Configuration | Monthly Cost (EUR) | Notes |
|---------|-------------------|-------------------|-------|
| **Azure Static Web App** | Free tier | €0 | Development environment |
| **Azure Functions** | Consumption (minimal usage) | €0 | Within free tier limits |
| **Supabase** | Free Tier (shared with prod) | €0 | Single project for initial development |
| **Total Development Environment** | | **€0** | |

### Total Estimated Monthly Bill

| Cost Category | Monthly Cost (EUR) | Notes |
|---------------|-------------------|-------|
| **Core Infrastructure** | €27-52 | Base production environment |
| **Tournament Period Premium** | €17-30 | Additional costs during tournament days |
| **Development Environment** | €0 | Free tier services |
| **Contingency (15%)** | €9-14 | Buffer for unexpected usage |
| **TOTAL ESTIMATED MONTHLY BILL** | **€66-109** | Expected range for starting period |

## Cost Analysis by User Type

| User Type | Count | Monthly Cost per User (EUR) |
|-----------|-------|--------------------------|
| Organizers | 100 | €0.66-1.09 |
| Players with Accounts | 900 | €0.07-0.12 |
| Participants without Accounts | Unlimited | Negligible incremental cost |
| Spectators | Unlimited | Negligible incremental cost |

Our architecture efficiently supports unlimited participants and spectators with minimal additional cost, while providing premium features to account holders.

## Multi-API Architecture Cost Analysis

Our architecture includes multiple API components - specifically a Node.js Express API Gateway and .NET 8 Azure Functions. This section addresses the cost implications of this design:

### Cost Impact of Multi-API Architecture

| Component | Purpose | Cost Impact | Mitigation Strategy |
|-----------|---------|------------|---------------------|
| **Node.js Express API Gateway** | API routing, JWT validation, caching, rate limiting | Additional €13/month for App Service Basic B1 tier | Centralizes auth/routing logic, reducing redundant code in Function endpoints |
| **Azure Functions (.NET 8)** | Complex business logic, tournament operations, custom calculations | Potentially higher execution counts due to API layering | Functions focus on complex operations that benefit from .NET performance |
| **Cross-Service Communication** | Internal calls between API Gateway and Functions | Additional bandwidth costs and latency | Intelligent caching to minimize redundant calls |

### Cost-Benefit Analysis

While a multi-API architecture increases monthly costs by approximately €13-15 compared to a single API approach, it provides several benefits that justify this investment:

1. **Technology Specialization**:
   - Node.js excels at lightweight API handling and middleware
   - .NET provides superior performance for complex business logic

2. **Operational Efficiency**:
   - Simplified authentication at a single entry point
   - Centralized rate limiting and protection
   - Focused development teams can work independently

3. **Cost Optimization Opportunities**:
   - Caching common responses at API Gateway level reduces Function invocations
   - Batching multiple client requests into single Function calls
   - Traffic shaping to prevent cost spikes

4. **Future Scalability**:
   - Gateway architecture facilitates adding specialized microservices
   - Easier to integrate new backend technologies without disrupting clients

### Alternative Approaches

| Alternative | Monthly Cost (EUR) | Pros | Cons |
|-------------|------------------|------|------|
| **Single API (.NET only)** | €55-95 | Simplified architecture, lower direct costs | Less optimized for API gateway duties, potentially higher code complexity |
| **Single API (Node.js only)** | €55-95 | Good fit for API gateway, lower direct costs | Less performant for complex calculations, possible higher resource needs |
| **Current Multi-API** | €66-109 | Best tool for each job, better separation of concerns | Higher direct costs, additional communication overhead |

The multi-API approach represents an investment of approximately 15-20% higher monthly infrastructure costs for improved development efficiency, system flexibility, and long-term scalability.

## Cost Optimization Strategies Already Implemented

1. **Serverless-First Approach**:
   - Functions on Consumption plan to scale to zero when not in use
   - Pay only for actual compute time used

2. **Hybrid Function Plan Strategy**:
   - Using Premium plan only for tournament days
   - Temporary scaling for predictable high-traffic periods

3. **Free Tier Maximization**:
   - Utilizing Supabase free tier initially (up to 1,000 users)
   - Leveraging Azure free grants (Functions, Key Vault operations)

4. **Efficient Storage Usage**:
   - Optimized media handling and storage
   - Appropriate caching at multiple levels

5. **Cold Start Optimizations**:
   - .NET 8 performance improvements
   - Static initialization patterns
   - Memory management techniques

6. **API Architecture Considerations**:
   - Strategic separation of API concerns between Node.js middleware and .NET Functions
   - Caching layer in API Gateway to reduce redundant Function calls
   - Rate limiting to prevent excessive costs from high-volume requests
   - Lightweight API Gateway design focused on routing and authentication

## Cost Management Recommendations

1. **Monitor Function Execution Metrics**:
   - Track execution counts and duration
   - Optimize high-cost functions

2. **Implement Caching Strategically**:
   - Cache tournament data during active events
   - Implement client-side caching for spectators

3. **Storage Lifecycle Management**:
   - Move older tournament media to cool storage
   - Archive historical data

4. **Schedule Cost Reviews**:
   - Monthly review of actual vs. estimated costs
   - Quarterly optimization assessment

5. **API Architecture Optimization**:
   - Evaluate API Gateway caching effectiveness regularly
   - Monitor cross-service communication patterns
   - Analyze Function invocation patterns to identify consolidation opportunities
   - Consider serverless options for API Gateway if usage patterns support it

## Conclusion

The Golf Tournament Organizer application is designed with cost efficiency as a core architectural principle. The serverless approach ensures costs scale proportionally with usage, making it particularly well-suited for the seasonal nature of golf tournaments.

With a starting monthly cost of €66-109, the application offers an exceptional value proposition for the exclusive 100 organizers and 900 players in the initial launch, while supporting unlimited participation from competitors and spectators at minimal incremental cost.

As the platform grows, costs will increase but remain manageable and predictable, with multiple optimization opportunities available at each stage.

## Supabase Scaling Considerations

Supabase's free tier provides an excellent starting point for our application with generous limits:

| Metric | Free Tier Limit | Our Initial Usage | Upgrade Threshold |
|--------|----------------|------------------|-------------------|
| Monthly Active Users | 50,000 | 1,000 | ~25,000 users (50%) |
| Database Size | 500MB | ~50-100MB initially | ~450MB (90%) |
| Storage | 1GB | ~200-300MB initially | ~900MB (90%) |
| Database Connections | 10 concurrent | 2-5 peak | 8+ sustained |

### Scaling Timeline

Based on our projected growth, we anticipate remaining on the Supabase free tier for:

1. **Initial Launch (6-12 months)**: Well under the free tier limits with 1,000 users
2. **Early Growth (12-24 months)**: Potentially reaching upgrade thresholds depending on user acquisition rate
3. **Established Phase (24+ months)**: Likely requiring Pro tier upgrade (€25/month)

### Upgrade Criteria

We should consider upgrading to Supabase Pro tier (€25/month) when:
- Reaching 25,000 monthly active users (50% of free tier limit)
- Database approaching 450MB (90% of free tier limit)
- Storage nearing 900MB (90% of free tier limit)
- Experiencing sustained periods of high database connection usage

This conservative approach ensures we maintain service quality before hitting hard limits while maximizing the free tier benefits.

## Future Architecture Evolution

As the Golf Tournament Organizer grows beyond its initial user base, we've identified Azure Container Apps as a promising evolution path for the backend architecture.

### Azure Container Apps as a Future Option

While our current architecture prioritizes simplicity and proven technologies, Azure Container Apps offers compelling benefits for a maturing application:

| Growth Stage | Monthly Active Users | Recommended Architecture | Monthly Cost Estimate (EUR) |
|--------------|---------------------|-------------------------|----------------------------|
| Initial Launch | 1,000 | Current architecture (Functions + API Gateway) | €51-94 |
| Growth Phase | 5,000+ | Container Apps for backend consolidation | €75-125 |
| Mature Application | 10,000+ | Specialized Container Apps microservices | €150-200 |

### Benefits of Container Apps for Future Growth

1. **Backend Consolidation**
   - Combine Node.js API Gateway and .NET Function logic in a single container
   - Eliminate the need for separate App Service and Function App resources
   - Potential cost savings of €10-15/month through consolidation

2. **Superior Scaling Characteristics**
   - Faster cold start performance compared to pure Functions
   - Scale to zero capability for cost optimization
   - Granular scaling based on actual demand

3. **Operational Simplicity**
   - Simplified deployment through container images
   - Consistent runtime environment
   - Easier local development and testing

4. **Implementation Timing**
   - Recommend evaluation after 6-12 months of production operation
   - Wait until usage patterns are established and understood
   - Initial focus should remain on product-market fit rather than infrastructure optimization

This future architecture option balances maintaining the application's cost efficiency while providing a clear growth path as usage increases.

---

*This estimate is based on Azure and Supabase pricing as of May 2025 and may be subject to change based on actual usage patterns and vendor pricing updates.*
