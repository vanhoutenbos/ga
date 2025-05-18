# Comprehensive Architecture: Golf Tournament Organizer

This document provides a consolidated view of the Golf Tournament Organizer application's architecture, encompassing both technical implementation details and infrastructure configuration.

## Architecture Overview

Our architecture leverages Azure services for hosting and .NET 8 backend APIs while integrating with Supabase for database and authentication. This approach optimizes for cost, performance, and development efficiency.

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
                                  ┌───────────▼───────────────┐           ┌──────────────────────┐
                                  │                           │           │                      │
                                  │  Supabase                 │◄─────────►│  Azure Key Vault     │
                                  │  - Authentication         │           │  - Secrets           │
                                  │  - PostgreSQL Database    │           │  - Connection Strings│
                                  │  - Realtime Updates       │           │  - API Keys          │
                                  │  - Storage                │           │                      │
                                  └───────────────────────────┘           └──────────────────────┘
```

## Frontend Architecture

### Progressive Web Application (PWA)
- React-based SPA with TypeScript for type safety
- Full PWA implementation with offline capabilities
- Service workers for background sync and caching
- Web App Manifest for installability on devices
- Responsive design optimized for mobile, tablet, and desktop
- Modular component architecture for reusable UI elements
- Device API integration for camera (QR code scanning) and geolocation
- Internationalization (i18n) with react-i18next for multi-language support

### Single Application with Role-Based Modules
- Unified codebase for scoring, viewing, and administration
- Dynamic module loading for performance optimization
- Role-based navigation and permission system
- Shared core services across all modules
- Context-aware UI that adapts to user role and device

### Frontend Implementation Details
- Progressive Web App with offline capabilities
- IndexedDB for offline data storage
- Optimized bundle sizes with code splitting
- Internationalization with i18next (English/Dutch support)
- Static Web App hosting with GitHub Actions integration
- Global CDN distribution for performance
- Custom domains with SSL certificates

## Backend Architecture

### API Services
- Azure Functions (.NET 8) on consumption plan for cost optimization
- JWT validation middleware for Supabase tokens
- Automatic scaling based on demand
- HTTP-triggered functions for all API endpoints
- Future caching capabilities for performance optimization

### Serverless Functions
- Azure Functions on App Service Plan B1 (always on)
- .NET 8 backend APIs for business logic
- Isolated process model for improved performance
- Minimal API approach for simplified endpoints
- Dependency injection for service management
- Entity Framework Core for database access
- Middleware for authentication and authorization

### Supabase Integration
- Supabase Authentication
  - User registration and login
  - JWT token management
  - Role management for organizers vs players
  - Integration with frontend via Supabase JS client
- Supabase PostgreSQL Database
  - Tournament data storage
  - Player profiles and statistics
  - Row-Level Security for fine-grained access control
  - PostgreSQL functions for complex operations
- Supabase Realtime
  - Live leaderboard updates
  - Real-time scoring notifications
  - Presence detection for active users
- Supabase Storage
  - Tournament media storage
  - User-generated content

## Supporting Services

### Azure Key Vault
- Secure storage for Supabase connection secrets
- API keys management
- Certificate storage
- Managed identities for secure access

### Application Insights
- Real-time application monitoring
- User behavior analytics
- Performance tracking
- Error logging and diagnostics

### Azure Blob Storage
- Tournament media storage
- Document repository
- Backup storage
- Tiered storage strategy (Hot/Cool/Archive)

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

## Offline Functionality and Conflict Resolution

### Offline-First Approach

The application implements an offline-first architecture to ensure usability in environments with intermittent connectivity, such as golf courses with limited network coverage:

1. **Local Data Storage**
   - IndexedDB for client-side data persistence
   - Complete tournament data cached for offline access
   - Local-first operations with background synchronization

2. **Synchronization Strategy**
   - Background sync via Service Workers
   - Incremental sync to minimize bandwidth usage
   - Queue-based approach for pending changes
   - Conflict detection and resolution on reconnection

3. **Conflict Resolution**
   - Timestamp-based "last edit wins" approach for conflict resolution
   - Special handling for official scorers' updates (priority override)
   - Field-level merging for partial updates when possible
   - Conflict notification for significant data discrepancies

### Implementation Details

The conflict resolution strategy is particularly important given the application's offline capabilities:

1. **Metadata Tracking**
   - Each record includes `updated_at` timestamp
   - Client identifier stored with each change
   - User role considered in conflict resolution logic

2. **Resolution Process**
   ```
   ┌─────────────────┐         ┌─────────────────┐
   │                 │         │                 │
   │  Local Changes  │  Sync   │  Server Data    │
   │  with Timestamp ├────────►│  with Timestamp │
   │                 │         │                 │
   └────────┬────────┘         └────────┬────────┘
            │                           │
            ▼                           ▼
   ┌─────────────────────────────────────────────┐
   │                                             │
   │  Compare Timestamps for Same Record         │
   │                                             │
   └────────┬────────────────────────┬───────────┘
            │                        │
            ▼                        ▼
   ┌──────────────────┐     ┌──────────────────┐
   │                  │     │                  │
   │  Local is Newer  │     │ Server is Newer  │
   │                  │     │                  │
   └────────┬─────────┘     └────────┬─────────┘
            │                        │
            ▼                        ▼
   ┌──────────────────┐     ┌──────────────────┐
   │                  │     │                  │
   │  Apply Local     │     │  Apply Server    │
   │  to Server       │     │  to Local        │
   │                  │     │                  │
   └──────────────────┘     └──────────────────┘
   ```

3. **Edge Case Handling**
   - Special priority for tournament officials
   - Merge strategies for non-conflicting field updates
   - Notification system for significant conflicts

For more detailed information on our conflict resolution approach, refer to the following documents:

- [Conflict Resolution Strategy](conflict-resolution-strategy.md) - Core approach and implementation details
- [Conflict Resolution Edge Cases](conflict-resolution-edge-cases.md) - Specialized handling for complex scenarios
- [Conflict Resolution Testing](conflict-resolution-testing.md) - Comprehensive testing strategy for conflict scenarios

For complete details, see our [Conflict Resolution Strategy](conflict-resolution-strategy.md) document.

## Advanced Conflict Resolution Strategy

The application includes a sophisticated conflict resolution strategy to handle data synchronization challenges when multiple users work with the application in offline mode. This is particularly important for tournament environments where network connectivity may be limited.

### Core Conflict Resolution Approach

Our primary strategy follows a "last edit wins" approach, enhanced with special handling for important edge cases:

1. **Timestamp-Based Resolution**
   - Every record includes `updated_at` timestamp and client identifier
   - On synchronization, timestamps determine which version prevails
   - Special rules apply for official scorers and critical tournament data

2. **Edge Case Handling**
   ```
   ┌─────────────────────┐     ┌─────────────────────┐     ┌─────────────────────┐
   │                     │     │                     │     │                     │
   │  Special Priority   │     │  Field-Level        │     │  Tournament Status  │
   │  for Officials      │     │  Merging            │     │  Transition Rules   │
   │                     │     │                     │     │                     │
   └─────────────────────┘     └─────────────────────┘     └─────────────────────┘
   ```

3. **User Experience Considerations**
   - Conflict notifications for significant discrepancies
   - Transparent audit trail of changes
   - Guided conflict resolution for extensive offline edits

### Enhanced Resolution Mechanisms

For complex scenarios, we have implemented additional resolution strategies:

1. **Score Validation-Aware Resolution**
   - Validates scores against tournament rules during conflict resolution
   - Preserves valid scores over invalid ones regardless of timestamp
   - Computes nearest valid score when both versions are invalid

2. **Multi-Device Synchronization**
   - Special handling for the same user editing from multiple devices
   - Enhanced merging for different fields modified on different devices
   - User notifications when cross-device merges occur

3. **Connectivity-Aware Sync Process**
   - Reliable batch processing with retry mechanisms
   - Transaction logging for recovery from interrupted syncs
   - Bandwidth-efficient incremental synchronization

### Future Resolution Enhancements

As the application evolves, we are implementing more sophisticated conflict resolution capabilities:

1. **Interactive Conflict Resolution UI**
   - Manual conflict resolution interface for critical conflicts
   - Visual diff tools to help users understand changes
   - Guided resolution workflow for complex conflict scenarios

2. **Optimistic Concurrency Control**
   - Version-based concurrency for more precise conflict detection
   - Database triggers to enforce version consistency
   - Client-side version tracking for offline operations

3. **Advanced Merge Strategies**
   - Object-structure-aware deep merging for complex data
   - Array handling with add/remove/modify detection
   - Field-level timestamp tracking for granular conflict resolution

4. **Cryptographic Audit Trail**
   - Blockchain-inspired append-only change log
   - Cryptographic verification of change history integrity
   - Complete audit capabilities for tournament record verification

For complete implementation details and code examples, refer to our [Conflict Resolution Strategy](conflict-resolution-strategy.md) document.

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

## Future Architecture Evolution

As the application grows beyond its initial user base, evolution options include:

### Container Apps Approach
- Backend consolidation via Container Apps
- Scale-to-zero capability for cost optimization
- Reduced cold start times
- Simplified deployment through containers
- Gradual migration path from existing architecture

### Premium Microservices Architecture
For enterprise-scale deployment (10,000+ users), a full microservices architecture offers:

```
┌─────────────────────┐           ┌───────────────────────────┐           ┌─────────────────────┐
│                     │           │                           │           │                     │
│  React PWA          │◄─────────►│  Azure Static Web App     │◄─────────►│  Azure Front Door   │
│  - Recorder App     │           │  - Hosting                │           │  - Global Routing   │
│  - Leaderboard      │           │  - Authentication Proxy   │           │  - WAF Protection   │
│  - Info Website     │           │  - Static Content         │           │  - Load Balancing   │
└─────────────────────┘           └───────────┬───────────────┘           └─────────────────────┘
                                              │
                                              ▼
┌─────────────────────┐           ┌───────────────────────────┐           ┌─────────────────────┐
│                     │           │                           │           │                     │
│  Azure API          │◄─────────►│  Azure Container Apps     │◄─────────►│  Azure Redis Cache  │
│  Management         │           │  - API Services           │           │  - Response Caching │
│  - API Documentation│           │  - Authentication         │           │  - Session Storage  │
│  - Developer Portal │           │  - Rate Limiting          │           │  - Distributed Cache│
│  - Subscription Mgmt│           │  - Request Routing        │           │                     │
└─────────────────────┘           └─────────────┬─────────────┘           └─────────────────────┘
                                                │
                                                ▼
┌──────────────────────────────────────────────────────────────────────────────────────────────┐
│                                                                                              │
│                                  Microservices Layer                                         │
│                                                                                              │
├─────────────────────┬─────────────────────┬─────────────────────┬─────────────────────┐     │
│                     │                     │                     │                     │     │
│  Tournament Service │  Scoring Service    │  Leaderboard Service│  User Profile       │     │
│  (Container App)    │  (Container App)    │  (Container App)    │  Service            │     │
│  - Tournament CRUD  │  - Score Recording  │  - Real-time Updates│  (Container App)    │     │
│  - Player Management│  - Score Validation │  - Statistics       │  - User Preferences │     │
│  - Course Management│  - History          │  - Rankings         │  - Auth Integration │     │
│                     │                     │                     │                     │     │
└─────────────────────┴─────────────────────┴─────────────────────┴─────────────────────┘     │
│                                                                                              │
├─────────────────────┬─────────────────────┬─────────────────────┬─────────────────────┐     │
│                     │                     │                     │                     │     │
│  Notification       │  Analytics Service  │  Content Management │  Payment Service    │     │
│  Service            │  (Container App)    │  Service            │  (Container App)    │     │
│  (Container App)    │  - User Behavior    │  - Media Management │  - Subscription     │     │
│  - Push Notifications│ - Business Intel   │  - Templates        │  - Tournament Fees  │     │
│  - Email            │  - Reporting        │  - Localization     │  - Payment Processor│     │
│  - SMS              │  - ML Insights      │  - Localization     │  - Invoicing        │     │
│                     │                     │                     │                     │     │
└─────────────────────┴─────────────────────┴─────────────────────┴─────────────────────┘     │
│                                                                                              │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
```

This premium architecture includes:
- Specialized microservices for each business function
- Advanced API management
- Enhanced security with Web Application Firewall
- Distributed caching
- Event-driven integration between services
- Multi-database strategy (relational and NoSQL)
- Advanced analytics capabilities

## Benefits of This Architecture

1. **Cost Efficiency**
   - Fixed-cost approach for predictable billing (€53/month)
   - Optimized resource utilization
   - Efficient scaling during tournaments

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
   - Clear evolution path as the application grows
   - Support for initial limited user base
   - Pathway to enterprise-scale deployments
   - Growth without architectural re-engineering

5. **Security**
   - Managed authentication services
   - Row-Level Security in database
   - Secret management in Key Vault
   - JWT-based authorization

## .NET 8 API Best Practices

This section outlines best practices for building robust, maintainable, and high-performance .NET 8 APIs. These principles should guide the development of the backend services for the Golf Tournament Organizer application.

### Clean Architecture Implementation

```
┌───────────────────────────────────────────────────────────────────┐
│                        Presentation Layer                          │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │                      API Controllers                         │  │
│  │  - Minimal API Endpoints                                     │  │
│  │  - Request/Response Models                                   │  │
│  │  - Input Validation                                          │  │
│  └─────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────┬───────────────────────────────┘
                                    │
                                    ▼
┌───────────────────────────────────────────────────────────────────┐
│                        Application Layer                           │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │                      Command/Query Handlers                  │  │
│  │  - Business Logic                                           │  │
│  │  - Validation                                               │  │
│  │  - Use Case Orchestration                                   │  │
│  └─────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────┬───────────────────────────────┘
                                    │
                                    ▼
┌───────────────────────────────────────────────────────────────────┐
│                          Domain Layer                              │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │                     Domain Models & Logic                    │  │
│  │  - Entities                                                  │  │
│  │  - Value Objects                                             │  │
│  │  - Domain Events                                             │  │
│  │  - Domain Services                                           │  │
│  └─────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────┬───────────────────────────────┘
                                    │
                                    ▼
┌───────────────────────────────────────────────────────────────────┐
│                      Infrastructure Layer                          │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │                     External Services                        │  │
│  │  - Data Persistence                                          │  │
│  │  - External APIs                                             │  │
│  │  - Authentication                                            │  │
│  │  - Caching                                                   │  │
│  │  - Messaging                                                 │  │
│  └─────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────┘
```

#### Key Packages and Technologies

1. **Core Architecture Packages**
   - **MediatR**: For implementing the CQRS pattern and handling domain events
   - **FluentValidation**: For robust request validation
   - **AutoMapper**: For object-to-object mapping between layers
   - **Ardalis.GuardClauses**: For defensive programming and input validation
   - **Ardalis.Result**: For standardized result objects
   - **Ardalis.Specification**: For encapsulating query specifications

2. **Project Structure**
   ```
   src/
   ├── GolfApp.Domain/                # Core domain models and logic
   │   ├── Entities/                  # Domain entities
   │   ├── Events/                    # Domain events
   │   ├── Exceptions/                # Domain-specific exceptions
   │   ├── Interfaces/                # Domain interfaces
   │   └── ValueObjects/              # Domain value objects
   ├── GolfApp.Application/           # Application business logic
   │   ├── Common/                    # Shared application components
   │   ├── Features/                  # Organized by feature
   │   │   ├── Tournaments/           
   │   │   │   ├── Commands/          # Write operations
   │   │   │   └── Queries/           # Read operations
   │   │   ├── Scoring/
   │   │   └── Players/
   │   ├── Interfaces/                # Application service interfaces
   │   └── Behaviors/                 # Cross-cutting behaviors (logging, validation)
   ├── GolfApp.Infrastructure/        # External concerns implementation
   │   ├── Data/                      # Data access
   │   ├── Services/                  # External service integrations
   │   ├── Identity/                  # Authentication/authorization
   │   └── Messaging/                 # Event/message handling
   └── GolfApp.Api/                   # API endpoints and configuration
       ├── Controllers/               # API controllers (if not using minimal APIs)
       ├── Endpoints/                 # Minimal API endpoint definitions
       ├── Models/                    # API-specific models
       └── Filters/                   # API filters
   ```

### Testing Strategy

### Supabase Testing Strategy

Supabase integration testing requires special consideration, especially when using the free tier and testing real-time features. Our approach focuses on resilience, fallbacks, and comprehensive validation of Row-Level Security (RLS) policies.

#### Testing RLS Policies

1. **Local Policy Validation**
   - PostgreSQL scripts that replicate our RLS policies for local testing
   - Unit tests against these local policies with different user contexts
   - Automated policy verification in CI/CD pipelines

2. **Integration Testing Against Development Instance**
   - Tests against a development Supabase instance
   - Validation of access patterns for different user roles
   - Negative test cases to confirm proper data isolation

#### Real-Time Feature Testing

1. **Mock-Based Testing**
   - Mock implementations of Supabase real-time clients
   - Unit tests for subscription setup, handling, and teardown
   - Validation of application behavior with simulated events

2. **Resilience Testing**
   - Tests for fallback mechanisms when real-time connections fail
   - Verification of reconnection strategies
   - Validation of data synchronization after connection recovery

3. **End-to-End Testing**
   - Dedicated Supabase project for testing
   - Automated tests for real-time subscriptions and event delivery
   - Performance baseline establishment for the free tier

4. **Free Tier Considerations**
   - Usage tracking relative to free tier limits
   - Implementation of graceful degradation when limits are approached
   - Planned upgrade paths when usage grows

For comprehensive details, see our [Supabase Testing Strategy](supabase-testing-strategy.md) document.

### Frontend Testing Strategy

1. **Component Testing**
   - Unit tests for React components using React Testing Library
   - Snapshot testing for UI stability
   - Interaction testing for user flows

2. **State Management Testing**
   - Unit tests for state management logic
   - Validation of state transitions
   - Testing of selectors and reducers

3. **End-to-End Testing**
   - Cypress for full application flows
   - Cross-browser compatibility testing
   - Accessibility testing with axe-core

### Backend Testing Strategy

1. **Unit Testing**
   - Tests for individual components (commands, queries, validators)
   - Mock-based testing for external dependencies
   - High coverage for business logic

2. **Integration Testing**
   - API endpoint testing with test clients
   - Database interaction testing
   - Authentication and authorization testing

3. **Performance Testing**
   - Load testing for API endpoints
   - Benchmark tests for critical operations
   - Memory usage and leak detection

### CQRS with MediatR Implementation

#### Commands (Write Operations)

```csharp
// Command definition
public class CreateTournamentCommand : IRequest<Result<TournamentDto>>
{
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public IEnumerable<PlayerEntry> Players { get; set; }
    // Other tournament properties
}

// Command validator
public class CreateTournamentCommandValidator : AbstractValidator<CreateTournamentCommand>
{
    public CreateTournamentCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tournament name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");
            
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required")
            .Must(BeInFuture).WithMessage("Tournament must start in the future");
            
        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required")
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be after or equal to start date");
            
        RuleFor(x => x.Players)
            .NotEmpty().WithMessage("Tournament must have at least one player");
    }
    
    private bool BeInFuture(DateTime date)
    {
        return date > DateTime.UtcNow;
    }
}

// Command handler
public class CreateTournamentCommandHandler : 
    IRequestHandler<CreateTournamentCommand, Result<TournamentDto>>
{
    private readonly ITournamentRepository _repository;
    private readonly IMapper _mapper;
    
    public CreateTournamentCommandHandler(
        ITournamentRepository repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }
    
    public async Task<Result<TournamentDto>> Handle(
        CreateTournamentCommand request, 
        CancellationToken cancellationToken)
    {
        // Map command to domain entity
        var tournament = new Tournament(
            request.Name,
            request.Description,
            request.StartDate,
            request.EndDate);
            
        // Add players to tournament
        foreach (var player in request.Players)
        {
            tournament.AddPlayer(player.PlayerId, player.HandicapIndex);
        }
        
        // Save to repository
        await _repository.AddAsync(tournament, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        
        // Return mapped result
        var tournamentDto = _mapper.Map<TournamentDto>(tournament);
        return Result<TournamentDto>.Success(tournamentDto);
    }
}
```

#### Queries (Read Operations)

```csharp
// Query definition
public class GetTournamentLeaderboardQuery : IRequest<Result<LeaderboardDto>>
{
    public Guid TournamentId { get; set; }
}

// Query handler
public class GetTournamentLeaderboardQueryHandler : 
    IRequestHandler<GetTournamentLeaderboardQuery, Result<LeaderboardDto>>
{
    private readonly ITournamentRepository _repository;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;
    
    public GetTournamentLeaderboardQueryHandler(
        ITournamentRepository repository,
        IMapper mapper,
        IMemoryCache cache)
    {
        _repository = repository;
        _mapper = mapper;
        _cache = cache;
    }
    
    public async Task<Result<LeaderboardDto>> Handle(
        GetTournamentLeaderboardQuery request, 
        CancellationToken cancellationToken)
    {
        // Check cache first
        string cacheKey = $"leaderboard_{request.TournamentId}";
        if (_cache.TryGetValue(cacheKey, out LeaderboardDto cachedLeaderboard))
        {
            return Result<LeaderboardDto>.Success(cachedLeaderboard);
        }
        
        // Get tournament data
        var tournament = await _repository.GetByIdWithPlayersAndScoresAsync(
            request.TournamentId, 
            cancellationToken);
            
        if (tournament == null)
        {
            return Result<LeaderboardDto>.NotFound();
        }
        
        // Build leaderboard
        var leaderboard = new LeaderboardDto
        {
            TournamentId = tournament.Id,
            TournamentName = tournament.Name,
            LastUpdated = DateTime.UtcNow,
            Players = tournament.GetLeaderboard()
        };
        
        // Cache result (sliding expiration of 1 minute)
        _cache.Set(cacheKey, leaderboard, TimeSpan.FromMinutes(1));
        
        return Result<LeaderboardDto>.Success(leaderboard);
    }
}
```

#### API Endpoint Registration with Minimal APIs

```csharp
public static class TournamentEndpoints
{
    public static void MapTournamentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tournaments", GetAllTournaments)
            .WithName("GetTournaments")
            .WithOpenApi()
            .Produces<List<TournamentDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
            
        app.MapGet("/api/tournaments/{id}", GetTournamentById)
            .WithName("GetTournamentById")
            .WithOpenApi()
            .Produces<TournamentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
            
        app.MapPost("/api/tournaments", CreateTournament)
            .WithName("CreateTournament")
            .WithOpenApi()
            .Produces<TournamentDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
            
        app.MapGet("/api/tournaments/{id}/leaderboard", GetTournamentLeaderboard)
            .WithName("GetTournamentLeaderboard")
            .WithOpenApi()
            .Produces<LeaderboardDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
    
    private static async Task<IResult> GetAllTournaments(IMediator mediator)
    {
        var query = new GetAllTournamentsQuery();
        var result = await mediator.Send(query);
        
        return result.IsSuccess 
            ? Results.Ok(result.Value) 
            : Results.Problem("Failed to retrieve tournaments");
    }
    
    private static async Task<IResult> GetTournamentById(Guid id, IMediator mediator)
    {
        var query = new GetTournamentByIdQuery { TournamentId = id };
        var result = await mediator.Send(query);
        
        return result.IsSuccess 
            ? Results.Ok(result.Value) 
            : result.Status == ResultStatus.NotFound 
                ? Results.NotFound() 
                : Results.Problem("Failed to retrieve tournament");
    }
    
    private static async Task<IResult> CreateTournament(
        CreateTournamentCommand command, 
        IMediator mediator)
    {
        var result = await mediator.Send(command);
        
        return result.IsSuccess 
            ? Results.Created($"/api/tournaments/{result.Value.Id}", result.Value) 
            : result.Status == ResultStatus.Invalid 
                ? Results.ValidationProblem(result.ValidationErrors) 
                : Results.Problem("Failed to create tournament");
    }
    
    private static async Task<IResult> GetTournamentLeaderboard(
        Guid id, 
        IMediator mediator)
    {
        var query = new GetTournamentLeaderboardQuery { TournamentId = id };
        var result = await mediator.Send(query);
        
        return result.IsSuccess 
            ? Results.Ok(result.Value) 
            : result.Status == ResultStatus.NotFound 
                ? Results.NotFound() 
                : Results.Problem("Failed to retrieve leaderboard");
    }
}
```

### Validation Best Practices

#### Input Validation

1. **Use FluentValidation for All Requests**
   ```csharp
   public class ScoreCardValidator : AbstractValidator<ScoreCardDto>
   {
       public ScoreCardValidator()
       {
           RuleFor(x => x.PlayerId)
               .NotEmpty().WithMessage("Player ID is required");
               
           RuleFor(x => x.TournamentId)
               .NotEmpty().WithMessage("Tournament ID is required");
               
           RuleFor(x => x.Scores)
               .NotEmpty().WithMessage("Scores are required");
               
           RuleForEach(x => x.Scores)
               .SetValidator(new ScoreValidator());
       }
   }
   
   public class ScoreValidator : AbstractValidator<ScoreDto>
   {
       public ScoreValidator()
       {
           RuleFor(x => x.HoleNumber)
               .InclusiveBetween(1, 18).WithMessage("Hole number must be between 1 and 18");
               
           RuleFor(x => x.Strokes)
               .GreaterThan(0).WithMessage("Strokes must be greater than 0")
               .LessThan(20).WithMessage("Strokes must be less than 20");
       }
   }
   ```

2. **Automatic Validation with MediatR Pipeline**
   ```csharp
   public class ValidationBehavior<TRequest, TResponse> : 
       IPipelineBehavior<TRequest, TResponse>
       where TRequest : IRequest<TResponse>
       where TResponse : IResult
   {
       private readonly IEnumerable<IValidator<TRequest>> _validators;
       
       public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
       {
           _validators = validators;
       }
       
       public async Task<TResponse> Handle(
           TRequest request, 
           RequestHandlerDelegate<TResponse> next, 
           CancellationToken cancellationToken)
       {
           if (!_validators.Any())
           {
               return await next();
           }
           
           var context = new ValidationContext<TRequest>(request);
           var validationResults = await Task.WhenAll(
               _validators.Select(v => v.ValidateAsync(context, cancellationToken)));
               
           var failures = validationResults
               .SelectMany(r => r.Errors)
               .Where(f => f != null)
               .ToList();
               
           if (failures.Count == 0)
           {
               return await next();
           }
           
           // Convert validation failures to a dictionary for ProblemDetails
           var errors = failures
               .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
               .ToDictionary(
                   failureGroup => failureGroup.Key,
                   failureGroup => failureGroup.ToArray());
                   
           // Return validation problem
           return (TResponse)Results.ValidationProblem(errors);
       }
   }
   ```

#### Domain Validation with Guard Clauses

```csharp
public class Tournament : AggregateRoot
{
    private readonly List<Player> _players = new();
    private readonly List<Round> _rounds = new();
    
    public Tournament(string name, string description, DateTime startDate, DateTime endDate)
    {
        // Guard clauses to enforce invariants
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.OutOfRange(startDate, nameof(startDate), DateTime.UtcNow, DateTime.UtcNow.AddYears(2));
        Guard.Against.OutOfRange(endDate, nameof(endDate), startDate, startDate.AddDays(14));
        
        Name = name;
        Description = description;
        StartDate = startDate;
        EndDate = endDate;
        
        // Raise domain event
        AddDomainEvent(new TournamentCreatedEvent(this));
    }
    
    public void AddPlayer(Guid playerId, decimal handicapIndex)
    {
        // Guard clauses
        Guard.Against.Default(playerId, nameof(playerId));
        Guard.Against.OutOfRange(handicapIndex, nameof(handicapIndex), -10, 54);
        
        // Domain logic
        if (_players.Any(p => p.Id == playerId))
        {
            throw new DomainException("Player already exists in tournament");
        }
        
        var player = new Player(playerId, handicapIndex);
        _players.Add(player);
        
        // Raise domain event
        AddDomainEvent(new PlayerAddedToTournamentEvent(this, player));
    }
    
    // Other methods...
}
```

### Error Handling and Resilience Patterns

#### Global Exception Handling

```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }
    
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred");
        
        var problemDetails = exception switch
        {
            DomainException domainException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Domain Rule Violation",
                Detail = domainException.Message,
                Type = "https://example.com/errors/domain-rule-violation"
            },
            ApplicationException appException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Application Error",
                Detail = appException.Message,
                Type = "https://example.com/errors/application-error"
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred",
                Detail = "An unexpected error occurred",
                Type = "https://example.com/errors/internal-server-error"
            }
        };
        
        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        
        return true;
    }
}
```

#### Retry Policies with Polly

```csharp
public static class HttpClientExtensions
{
    public static IServiceCollection AddResilientHttpClient(
        this IServiceCollection services,
        string name,
        Uri baseAddress)
    {
        services
            .AddHttpClient(name, client =>
            {
                client.BaseAddress = baseAddress;
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());
            
        return services;
    }
    
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var serviceProvider = context.GetServiceProvider();
                    var logger = serviceProvider.GetService<ILogger<object>>();
                    logger?.LogWarning("Retrying after {Timespan}s due to {StatusCode}", 
                        timespan.TotalSeconds, 
                        outcome.Result?.StatusCode);
                });
    }
    
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                5,
                TimeSpan.FromMinutes(1),
                onBreak: (outcome, timespan, context) =>
                {
                    var serviceProvider = context.GetServiceProvider();
                    var logger = serviceProvider.GetService<ILogger<object>>();
                    logger?.LogWarning("Circuit breaker opened for {Timespan}s", 
                        timespan.TotalSeconds);
                },
                onReset: (context) =>
                {
                    var serviceProvider = context.GetServiceProvider();
                    var logger = serviceProvider.GetService<ILogger<object>>();
                    logger?.LogInformation("Circuit breaker reset");
                });
    }
}
```

### Caching and Performance Optimization

#### Memory Cache Strategy

```csharp
public class CachedTournamentRepository : ITournamentRepository
{
    private readonly ITournamentRepository _decorated;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedTournamentRepository> _logger;
    
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    public CachedTournamentRepository(
        ITournamentRepository decorated,
        IMemoryCache cache,
        ILogger<CachedTournamentRepository> logger)
    {
        _decorated = decorated;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<Tournament> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        string cacheKey = $"tournament_{id}";
        
        if (_cache.TryGetValue(cacheKey, out Tournament tournament))
        {
            _logger.LogInformation("Tournament {Id} retrieved from cache", id);
            return tournament;
        }
        
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            // Double-check after acquiring semaphore
            if (_cache.TryGetValue(cacheKey, out tournament))
            {
                return tournament;
            }
            
            tournament = await _decorated.GetByIdAsync(id, cancellationToken);
            
            if (tournament != null)
            {
                // Cache with sliding expiration
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1));
                    
                _cache.Set(cacheKey, tournament, cacheOptions);
                _logger.LogInformation("Tournament {Id} added to cache", id);
            }
            
            return tournament;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    // Implement other repository methods with similar caching pattern
}
```

#### Output Caching

```csharp
// Program.cs
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("LeaderboardCache", builder =>
    {
        builder
            .Expire(TimeSpan.FromMinutes(1))
            .SetVaryByQuery("tournamentId")
            .Tag("leaderboards");
    });
});

// Endpoint registration
app.MapGet("/api/tournaments/{id}/leaderboard", GetTournamentLeaderboard)
    .WithName("GetTournamentLeaderboard")
    .WithOpenApi()
    .CacheOutput("LeaderboardCache");
    
// Cache invalidation (in command handler)
public class SubmitScoreCommandHandler : IRequestHandler<SubmitScoreCommand, Result<Unit>>
{
    private readonly ITournamentRepository _repository;
    private readonly IOutputCacheStore _outputCacheStore;
    
    public SubmitScoreCommandHandler(
        ITournamentRepository repository,
        IOutputCacheStore outputCacheStore)
    {
        _repository = repository;
        _outputCacheStore = outputCacheStore;
    }
    
    public async Task<Result<Unit>> Handle(
        SubmitScoreCommand request,
        CancellationToken cancellationToken)
    {
        // Process the score submission
        
        // Invalidate leaderboard cache
        await _outputCacheStore.EvictByTagAsync("leaderboards", cancellationToken);
        
        return Result<Unit>.Success(Unit.Value);
    }
}
```

### API Versioning and Documentation

```csharp
// Program.cs
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-API-Version"),
        new QueryStringApiVersionReader("api-version"));
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Golf Tournament API",
        Version = "v1",
        Description = "API for managing golf tournaments and scores",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@golftournamentapp.com"
        }
    });
    
    // Add XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
    
    // Add authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Versioned minimal API endpoints
app.MapGroup("/api/v{version:apiVersion}/tournaments")
    .MapTournamentEndpointsV1()
    .WithApiVersionSet(apiVersionSet)
    .HasApiVersion(new ApiVersion(1, 0));
    
app.MapGroup("/api/v{version:apiVersion}/tournaments")
    .MapTournamentEndpointsV2()
    .WithApiVersionSet(apiVersionSet)
    .HasApiVersion(new ApiVersion(2, 0));
```

### Memory Management and Performance

#### Efficient Object Pooling

```csharp
public class ScoreCalculationService
{
    private readonly ObjectPool<ScoreCalculator> _calculatorPool;
    
    public ScoreCalculationService(ObjectPoolProvider poolProvider)
    {
        _calculatorPool = poolProvider.Create(new ScoreCalculatorPolicy());
    }
    
    public async Task<ScoreResult> CalculateScores(ScoreCard scoreCard)
    {
        var calculator = _calculatorPool.Get();
        try
        {
            return await calculator.CalculateAsync(scoreCard);
        }
        finally
        {
            _calculatorPool.Return(calculator);
        }
    }
    
    private class ScoreCalculatorPolicy : IPooledObjectPolicy<ScoreCalculator>
    {
        public ScoreCalculator Create()
        {
            return new ScoreCalculator();
        }
        
        public bool Return(ScoreCalculator obj)
        {
            obj.Reset();
            return true;
        }
    }
}
```

#### Optimized Entity Framework Core Usage

```csharp
public class TournamentRepository : ITournamentRepository
{
    private readonly AppDbContext _dbContext;
    
    public TournamentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<Tournament> GetByIdWithPlayersAndScoresAsync(
        Guid id, 
        CancellationToken cancellationToken)
    {
        // Use compiled query for performance
        return await CompileQueries.GetTournamentWithRelatedData(
            _dbContext, 
            id, 
            cancellationToken);
    }
    
    // Other repository methods...
}

// Static compiled queries for better performance
public static class CompileQueries
{
    public static readonly Func<AppDbContext, Guid, CancellationToken, Task<Tournament>> 
        GetTournamentWithRelatedData = EF.CompileAsyncQuery(
            (AppDbContext context, Guid id, CancellationToken ct) => 
                context.Tournaments
                    .AsNoTracking()
                    .Include(t => t.Players)
                    .Include(t => t.Rounds)
                        .ThenInclude(r => r.Scores)
                    .FirstOrDefault(t => t.Id == id));
}
```

### Additional Best Practices

1. **HTTP Status Codes and Responses**
   - Use appropriate HTTP status codes
   - Consistent error response structure
   - Problem Details (RFC 7807) for errors
   - Hypermedia links (HATEOAS) where appropriate
   - Consistent response envelopes

2. **Resource-Based Design**
   - Organize APIs around resources, not actions
   - Use appropriate HTTP methods (GET, POST, PUT, DELETE)
   - Implement proper collection endpoints
   - Use query parameters for filtering, sorting, and pagination
   - Include metadata for paginated responses

3. **Code Quality Standards**
   - Automated tests with high coverage
   - Code analysis with analyzers and rulesets
   - XML documentation for public APIs
   - Consistent naming conventions
   - Domain-Driven Design principles

These best practices provide a solid foundation for building robust, maintainable, and high-performance .NET 8 APIs for the Golf Tournament Organizer application. By following these patterns and practices, the development team can ensure that the backend services meet the application's requirements for reliability, performance, and scalability.
