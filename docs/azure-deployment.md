# Azure Deployment Architecture

Building on the lessons learned from previous versions of the application and optimizing for cost-efficiency, the following Azure deployment architecture is recommended:

## Updated Architecture for Recorder App / Leaderboard / Informative Website

Our new architecture leverages Azure services for hosting and .NET 8 backend APIs while integrating with Supabase for database and authentication. This approach optimizes for cost, performance, and development efficiency.

### Architecture Overview

```
┌─────────────────────┐           ┌───────────────────────────┐           ┌─────────────────────┐
│                     │           │                           │           │                     │
│  React PWA          │◄─────────►│  Azure Static Web App     │◄─────────►│  Azure CDN          │
│  - Recorder App     │           │  - Hosting                │           │  - Global Content   │
│  - Leaderboard      │           │  - Authentication Proxy   │           │  - Distribution     │
│  - Info Website     │           │  - API Routing            │           │                     │
└─────────────────────┘           └───────────┬───────────────┘           └─────────────────────┘
                                              │
                                              │
                                  ┌───────────▼───────────────┐           ┌─────────────────────┐
                                  │                           │           │                     │
                                  │  Azure Function App       │◄─────────►│  Azure App Insights │
                                  │  (.NET 8)                 │           │  - Monitoring       │
                                  │  - Tournament API         │           │  - Logging          │
                                  │  - Leaderboard API        │           │  - Analytics        │
                                  │  - User Profile API       │           │                     │
                                  └───────────┬───────────────┘           └─────────────────────┘
                                              │
                                              │
                                  ┌───────────▼───────────────┐           ┌─────────────────────┐
                                  │                           │           │                     │
                                  │  Supabase                 │◄─────────►│  Azure Key Vault    │
                                  │  - Authentication         │           │  - Secrets          │
                                  │  - PostgreSQL Database    │           │  - Connection Strings│
                                  │  - Realtime Updates       │           │  - API Keys         │
                                  │  - Storage                │           │                     │
                                  └───────────────────────────┘           └─────────────────────┘
```

## Core Infrastructure Components

1. **Frontend Hosting**
   - **Azure Static Web Apps**: For hosting the React PWA
     - Standard tier for production environment
     - Built-in GitHub Actions integration for CI/CD
     - Global CDN distribution for performance
     - Managed SSL certificates included
     - Support for custom authentication providers
     - API routing capabilities for backend integration
     - PWA capabilities (manifest, service workers)

2. **API Services**
   - **Azure Functions (.NET 8 Isolated Process Model)**
     - HTTP-triggered functions for all API endpoints
     - Consumption plan for cost optimization
     - Automatic scaling based on demand
     - .NET 8 for improved performance and reduced memory usage
     - Integration with Supabase services via SDK
     - Role-based access control
     - API versioning support

3. **Supabase Integration**
   - **Supabase Authentication**
     - User registration and login
     - JWT token management
     - Role management for organizers vs players
     - Integration with frontend via Supabase JS client
   - **Supabase PostgreSQL Database**
     - Tournament data storage
     - Player profiles and statistics
     - Row-Level Security for fine-grained access control
     - PostgreSQL functions for complex operations
   - **Supabase Realtime**
     - Live leaderboard updates
     - Real-time scoring notifications
     - Presence detection for active users

4. **Supporting Services**
   - **Azure Key Vault**
     - Secure storage for Supabase connection secrets
     - API keys management
     - Certificate storage
     - Managed identities for secure access
   - **Azure Application Insights**
     - Real-time application monitoring
     - User behavior analytics
     - Performance tracking
     - Error logging and diagnostics
   - **Azure Blob Storage**
     - Static assets for web application
     - Tournament media and document storage
     - Backup storage for exports

## Technical Implementation Details

### React PWA Implementation
- Progressive Web App with offline capabilities
- Responsive design for mobile, tablet, and desktop
- React framework with TypeScript
- Service workers for caching and background sync
- IndexedDB for offline data storage
- Optimized bundle sizes with code splitting
- Internationalization with i18next (English/Dutch support)

### Azure Static Web App Configuration
- Custom routing rules for SPA navigation
- API proxy configuration for backend services
- Authentication provider configuration
- CDN configuration for global distribution
- Custom domain configuration
- GitHub Actions workflow for CI/CD

### .NET 8 Function App Implementation
- Isolated process model for improved performance
- Minimal API approach for simplified endpoints
- Dependency injection for service management
- Entity Framework Core for database access
- Middleware for authentication and authorization
- Supabase .NET SDK integration
- Azure Functions bindings for integration with other Azure services

### Supabase Integration Details
- User authentication flow with Supabase Auth
- Database schema design optimized for golf tournaments
- Row-Level Security policies for data protection
- Real-time subscriptions for live updates
- Supabase Storage for media management
- JavaScript client in frontend, .NET SDK in backend

## Authentication Flow

1. **User Registration/Login**
   - User authenticates through Supabase Auth in the React frontend
   - Supabase issues JWT tokens for the authenticated session
   - Frontend stores tokens securely for subsequent API calls

2. **API Authorization**
   - Frontend includes JWT token in requests to Azure Functions
   - Function App validates token and extracts user claims
   - Roles and permissions checked for authorization
   - Access granted or denied based on user context

3. **Database Access Control**
   - Row-Level Security in Supabase ensures users can only access permitted data
   - .NET backend respects same permission boundaries
   - Additional business logic validation in Function App layer

## Cost Optimization Strategies

1. **Serverless-First Approach**
   - Azure Functions Consumption plan for cost-effective scaling
   - Scale to zero during inactive periods
   - Pay only for actual compute usage

2. **Static Web App Efficiency**
   - Free tier for development
   - Standard tier for production with reasonable included bandwidth
   - Built-in CDN reduces data transfer costs

3. **Supabase Free Tier Maximization**
   - Limited initial user accounts strategy (100 organizers, 900 players)
   - Optimization of database queries to minimize load
   - Efficient use of realtime connections

4. **Caching Strategy**
   - Client-side caching for static assets
   - CDN caching for frequent content
   - API response caching where appropriate
   - Service worker caching for offline support

## Deployment and CI/CD Pipeline

1. **GitHub Actions Workflows**
   - Automated build, test, and deployment
   - Environment-specific configurations
   - Frontend and backend deployment coordination
   - Preview environments for pull requests
   - Security scanning integration

2. **Infrastructure as Code**
   - Azure Bicep templates for resource provisioning
   - Environment parameterization
   - Idempotent deployments
   - Configuration validation

3. **Monitoring and Operations**
   - Application Insights dashboard
   - Custom alerts for critical metrics
   - Log Analytics for error investigation
   - Usage analytics for feature optimization

## Benefits of This Architecture

1. **Cost Efficiency**
   - Serverless architecture minimizes infrastructure costs
   - Pay-as-you-go model for all components
   - Free tier usage where possible
   - Scale-to-zero capability during quiet periods

2. **Development Efficiency**
   - Familiar React frontend for web developers
   - Modern .NET 8 backend for API developers
   - Clear separation of concerns
   - Reduced operational overhead with managed services

3. **Performance**
   - Global CDN distribution for frontend
   - .NET 8 performance optimizations
   - Supabase real-time capabilities for live updates
   - Optimized caching at multiple levels

4. **Scalability**
   - Automatic scaling for all components
   - Designed to handle tournament traffic spikes
   - Path to growth beyond initial account limitations

5. **Security**
   - Managed authentication services
   - Row-Level Security in database
   - Secret management in Key Vault
   - JWT-based authorization

This architecture provides a balance between cost optimization and performance, with a focus on modern web technologies and cloud-native patterns. It leverages the strengths of both Azure (hosting, functions, monitoring) and Supabase (auth, database, realtime) to create a comprehensive solution for the Golf Tournament Organizer application.
