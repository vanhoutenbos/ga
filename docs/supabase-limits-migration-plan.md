# Supabase Limits & Migration Plan

## Overview

This document outlines our strategy for handling Supabase service limits and the migration plan for when we outgrow the free or pro tiers. The Golf Tournament Organizer application currently uses Supabase for authentication, database, and real-time functionality, but we need to plan for scaling beyond these tiers as our user base grows.

## Current Supabase Tier Limits

### Free Tier Limitations
- Database: 500MB storage
- Auth: 50,000 MAU (Monthly Active Users)
- Storage: 1GB storage, 2GB bandwidth
- Real-time: 200 concurrent connections
- Edge Functions: 500K invocations/month
- Database Branches: 2 branches

### Pro Tier Limitations ($25/month)
- Database: 8GB storage
- Auth: 100,000 MAU
- Storage: 100GB storage, 200GB bandwidth
- Real-time: 500 concurrent connections
- Edge Functions: 2M invocations/month
- Database Branches: 4 branches

### Team Tier Limitations ($599/month)
- Database: 16GB storage (customizable)
- Auth: Unlimited MAUs
- Storage: 1TB storage, 2TB bandwidth
- Real-time: 3,000 concurrent connections
- Edge Functions: 5M invocations/month
- Database Branches: 10 branches

## Migration Triggers

We will consider migration from our current tier when approaching any of these thresholds:

1. **Database Storage**: When reaching 80% of the storage limit
2. **Monthly Active Users**: When reaching 75% of the MAU limit
3. **Real-time Connections**: When regularly exceeding 70% of concurrent connection limit during tournaments
4. **Edge Function Invocations**: When consistently reaching 80% of the monthly limit
5. **Cost Efficiency**: When the next tier up would be more cost-effective than current architecture

## Migration Options

### Option 1: Upgrade to Higher Supabase Tier

**When to Choose:**
- When only 1-2 limits are being approached
- When the next tier provides sufficient headroom for 12+ months of projected growth
- When the rest of the Supabase services meet our needs

**Implementation Steps:**
1. Run a cost-benefit analysis comparing current tier vs. next tier
2. Schedule the upgrade during a low-usage window
3. Monitor performance post-upgrade to validate improvements
4. Update cost projections based on new tier pricing

### Option 2: Hybrid Architecture

**When to Choose:**
- When specific services (e.g., real-time or storage) are hitting limits, but not others
- When certain workloads would benefit from specialized services

**Implementation Plan:**
1. **Database Overflow Strategy**
   - Keep primary/recent data in Supabase
   - Move historical or archival data to Azure SQL Database
   - Implement a data access layer that abstracts the physical data location
   - Use Azure Functions to bridge between data sources when necessary

2. **Real-time Scale Out**
   - Supplement Supabase real-time with Azure SignalR Service for high-concurrency tournaments
   - Implement a tournament size threshold that determines which real-time service to use
   - Maintain a consistent API interface regardless of the underlying service

3. **Storage Offloading**
   - Move large media files to Azure Blob Storage
   - Use short-lived signed URLs for secure access
   - Implement a consistent storage interface abstraction

### Option 3: Full Migration to Self-Hosted PostgreSQL and Services

**When to Choose:**
- When multiple Supabase limits are approaching simultaneously
- When control over infrastructure is needed for compliance or performance reasons
- When cost analysis shows significant savings with self-hosting

**Implementation Steps:**

1. **Preparation Phase (1-2 months before migration)**
   - Set up PostgreSQL cluster on Azure Database for PostgreSQL or Azure Kubernetes Service
   - Create migration scripts for schema and data
   - Set up PostgREST for API compatibility
   - Configure real-time functionality using Postgres triggers and web sockets
   - Implement authentication services using Azure Active Directory B2C or similar

2. **Testing Phase (2-4 weeks)**
   - Create a parallel test environment with the new infrastructure
   - Run full data migration tests with production-sized datasets
   - Validate performance under load
   - Test authorization and RLS policy equivalents
   - Validate all application features against the new backend

3. **Migration Phase (1-2 days)**
   - Schedule downtime window (ideally < 8 hours)
   - Freeze writes to Supabase
   - Run final data migration
   - Update application configuration to point to new services
   - Run verification tests
   - Route traffic to new infrastructure

4. **Post-Migration Phase (1-2 weeks)**
   - Monitor system performance and error rates closely
   - Keep Supabase as read-only backup for quick rollback if needed
   - Validate all integrations are functioning correctly
   - Address any issues discovered in production

## Feature-Specific Migration Considerations

### Authentication
- Export user data from Supabase Auth
- Import to Azure AD B2C or a custom authentication service
- Handle password resets for all users (one-time token for password setting)
- Update frontend authentication flows

### Database
- Maintain PostgreSQL compatibility for easiest migration
- Export schema and data using pg_dump
- Validate RLS policies and convert to equivalent permissions
- Update connection strings in application configuration

### Real-time
- Replace Supabase real-time with Postgres triggers and WebSockets/SignalR
- Update client subscription patterns
- Test concurrency and latency under tournament load

### Storage
- Migrate files to Azure Blob Storage
- Update file access patterns in the application
- Generate equivalent access policies

## Cost Comparison

| Service | Supabase Pro Tier | Self-Hosted (Azure) | Notes |
|---------|------------------|---------------------|-------|
| Database | $25/month | ~$50-200/month | Depends on size, performance tier |
| Auth | Included | ~$0.05/MAU | Azure AD B2C pricing |
| Storage | Included | ~$0.02/GB/month | Azure Blob Storage |
| Real-time | Included | ~$50-100/month | Azure SignalR Service |
| Functions | Included | Pay per execution | Azure Functions consumption plan |
| Total | $25/month | $100-350/month | Self-hosting has higher base cost but better scaling economics |

## Risk Mitigation

1. **Data Integrity**
   - Run multiple test migrations before the actual migration
   - Implement validation scripts to verify data consistency
   - Maintain a read-only copy of Supabase for post-migration verification

2. **Downtime Minimization**
   - Use a blue-green deployment approach where possible
   - Schedule migration during lowest usage periods
   - Prepare rollback scripts and procedures

3. **Performance Regression**
   - Benchmark key operations before and after migration
   - Create load tests that simulate tournament traffic
   - Have optimization strategies ready for common bottlenecks

4. **Migration Failure**
   - Maintain the ability to roll back to Supabase for 30 days post-migration
   - Create detailed procedure documents for both migration and rollback

## Monitoring and Validation

Post-migration monitoring will focus on:

1. **Performance Metrics**
   - API response times compared to pre-migration baseline
   - Database query performance
   - Real-time message delivery latency

2. **Error Rates**
   - Application errors
   - API failures
   - Authentication issues

3. **Cost Tracking**
   - Compare actual costs to projections
   - Identify optimization opportunities

## Implementation Timeline

1. **Preparation and Planning**: 1-2 months
2. **Development of Migration Tools**: 2-4 weeks
3. **Testing and Validation**: 2-4 weeks
4. **Migration Execution**: 1-2 days
5. **Post-Migration Monitoring**: 2-4 weeks

## Conclusion

This migration plan provides a structured approach to handling the growth of the Golf Tournament Organizer application beyond Supabase's tier limits. By identifying migration triggers, preparing multiple migration options, and establishing clear validation procedures, we can ensure a smooth transition as our user base grows.

The hybrid architecture approach offers the most flexibility, allowing us to scale individual components as needed while minimizing disruption. However, all options should be re-evaluated when migration triggers are reached to ensure we select the most appropriate approach based on our current needs and the available services at that time.
