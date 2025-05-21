# API Documentation

This document provides comprehensive documentation for the Golf Tournament Organizer application's APIs, both for internal use and for potential third-party integrations.

## API Overview

The Golf Tournament Organizer exposes two primary sets of APIs:

1. **Azure Function APIs**: Core application functionality for tournament management, scoring, and administrative operations
2. **Supabase Edge Function APIs**: Real-time and lightweight operations closely tied to the database

All APIs follow RESTful principles with consistent error handling and authentication requirements.

## Authentication

### Authentication Methods

All APIs require authentication using one of these methods:

1. **Bearer Token**: JWT token obtained through Supabase Auth
2. **API Key**: For third-party integrations (limited access)

Example:

```http
GET /api/tournaments
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM...
```

### Obtaining Authentication Tokens

```typescript
// Using the Supabase client
const { data: { session }, error } = await supabase.auth.signInWithPassword({
  email: 'user@example.com',
  password: 'secure-password'
});

const token = session?.access_token;
```

## Common HTTP Status Codes

| Code | Description |
|------|-------------|
| 200 | Success - The request was successful |
| 201 | Created - Resource was successfully created |
| 400 | Bad Request - Invalid parameters or request body |
| 401 | Unauthorized - Missing or invalid authentication |
| 403 | Forbidden - Authenticated but lacking permission |
| 404 | Not Found - Resource doesn't exist |
| 422 | Unprocessable Entity - Validation error |
| 429 | Too Many Requests - Rate limit exceeded |
| 500 | Internal Server Error - Server failure |

## Error Response Format

All APIs use a consistent error response format following RFC 7807:

```json
{
  "type": "https://golfapp.example.com/errors/invalid-input",
  "title": "Invalid Input",
  "status": 400,
  "detail": "The provided tournament date is in the past",
  "instance": "/api/tournaments/create",
  "errors": {
    "tournamentDate": "Must be a future date"
  }
}
```

## API Rate Limits

| API Type | Authenticated | Unauthenticated |
|----------|---------------|-----------------|
| Standard Endpoints | 120 req/min | 30 req/min |
| Write Operations | 60 req/min | Not allowed |
| Tournament Creation | 10 req/min | Not allowed |
| Score Submission | 120 req/min | Not allowed |

Rate limit headers are included in all responses:

```http
X-RateLimit-Limit: 120
X-RateLimit-Remaining: 117
X-RateLimit-Reset: 1628776932
```

## Core APIs (Azure Functions)

### Tournament Management

#### Get All Tournaments

Retrieves a list of tournaments the user has access to.

**Endpoint:** `GET /api/tournaments`

**Query Parameters:**

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| page | integer | Page number for pagination | 1 |
| pageSize | integer | Number of items per page | 20 |
| status | string | Filter by status (`upcoming`, `active`, `completed`) | All |
| sortBy | string | Sort field (`date`, `name`, `playerCount`) | `date` |
| sortDirection | string | Sort direction (`asc`, `desc`) | `asc` |

**Response:**

```json
{
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Annual Club Championship",
      "description": "The premier event of the season",
      "status": "upcoming",
      "startDate": "2023-08-15T08:00:00Z",
      "endDate": "2023-08-16T18:00:00Z",
      "location": "Main Course",
      "format": "stroke",
      "playerCount": 48,
      "organizerId": "7f4df40d-8541-429a-8731-7d177e965d99",
      "createdAt": "2023-07-01T10:15:30Z",
      "updatedAt": "2023-07-05T14:20:15Z"
    },
    // Additional tournaments...
  ],
  "metadata": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 156,
    "totalPages": 8
  }
}
```

#### Get Tournament Details

Retrieves detailed information about a specific tournament.

**Endpoint:** `GET /api/tournaments/{tournamentId}`

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| tournamentId | string (UUID) | Unique identifier of the tournament |

**Response:**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Annual Club Championship",
  "description": "The premier event of the season",
  "status": "upcoming",
  "startDate": "2023-08-15T08:00:00Z",
  "endDate": "2023-08-16T18:00:00Z",
  "location": "Main Course",
  "format": "stroke",
  "handicapAdjustment": true,
  "maxPlayers": 60,
  "currentPlayers": 48,
  "registrationDeadline": "2023-08-10T23:59:59Z",
  "entryFee": {
    "amount": 75.00,
    "currency": "USD",
    "paymentMethods": ["credit_card", "club_account"]
  },
  "prizes": [
    {
      "position": 1,
      "description": "First Place",
      "amount": 500.00,
      "currency": "USD"
    },
    {
      "position": 2,
      "description": "Second Place",
      "amount": 300.00,
      "currency": "USD"
    }
  ],
  "rounds": [
    {
      "id": "a71c7b6a-8f4b-4e61-9c37-920a040c5611",
      "name": "Round 1",
      "date": "2023-08-15T08:00:00Z",
      "courseId": "c712b89e-3890-47ba-9297-caaa9561cd23",
      "courseName": "Championship Course",
      "teeTime": true
    }
  ],
  "organizerId": "7f4df40d-8541-429a-8731-7d177e965d99",
  "organizerName": "John Smith",
  "createdAt": "2023-07-01T10:15:30Z",
  "updatedAt": "2023-07-05T14:20:15Z",
  "rules": "Standard club rules apply. USGA rules govern all play.",
  "visibility": "public",
  "featured": false
}
```

#### Create Tournament

Creates a new tournament.

**Endpoint:** `POST /api/tournaments`

**Request Body:**

```json
{
  "name": "Fall Member-Guest Tournament",
  "description": "Annual fall tournament for members and guests",
  "startDate": "2023-09-22T08:00:00Z",
  "endDate": "2023-09-23T18:00:00Z",
  "location": "South Course",
  "format": "stroke",
  "handicapAdjustment": true,
  "maxPlayers": 40,
  "registrationDeadline": "2023-09-15T23:59:59Z",
  "entryFee": {
    "amount": 90.00,
    "currency": "USD",
    "paymentMethods": ["credit_card", "club_account"]
  },
  "rounds": [
    {
      "name": "Round 1",
      "date": "2023-09-22T08:00:00Z",
      "courseId": "c712b89e-3890-47ba-9297-caaa9561cd23",
      "teeTime": true
    },
    {
      "name": "Round 2",
      "date": "2023-09-23T08:00:00Z", 
      "courseId": "c712b89e-3890-47ba-9297-caaa9561cd23",
      "teeTime": true
    }
  ],
  "rules": "Standard club rules apply. USGA rules govern all play.",
  "visibility": "public",
  "featured": false
}
```

**Response:**

```json
{
  "id": "9802e37c-3e51-4cc9-88a7-5a53e721bc8f",
  "name": "Fall Member-Guest Tournament",
  "...": "... other tournament fields ...",
  "createdAt": "2023-08-03T15:42:18Z"
}
```

#### Update Tournament

Updates an existing tournament.

**Endpoint:** `PUT /api/tournaments/{tournamentId}`

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| tournamentId | string (UUID) | Unique identifier of the tournament |

**Request Body:**

```json
{
  "name": "Fall Member-Guest Tournament (Updated)",
  "description": "Annual fall tournament for members and guests",
  "startDate": "2023-09-23T08:00:00Z", // Date changed
  "...": "... other fields to update ..."
}
```

**Response:**

```json
{
  "id": "9802e37c-3e51-4cc9-88a7-5a53e721bc8f",
  "name": "Fall Member-Guest Tournament (Updated)",
  "...": "... other tournament fields ...",
  "updatedAt": "2023-08-03T16:20:45Z"
}
```

#### Delete Tournament

Deletes a tournament.

**Endpoint:** `DELETE /api/tournaments/{tournamentId}`

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| tournamentId | string (UUID) | Unique identifier of the tournament |

**Response:**

```json
{
  "success": true,
  "message": "Tournament deleted successfully"
}
```

### Tournament Participants

#### List Tournament Participants

Retrieves all participants for a tournament.

**Endpoint:** `GET /api/tournaments/{tournamentId}/participants`

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| tournamentId | string (UUID) | Unique identifier of the tournament |

**Query Parameters:**

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| page | integer | Page number for pagination | 1 |
| pageSize | integer | Number of items per page | 50 |
| sortBy | string | Sort field (`name`, `handicap`, `registrationDate`) | `name` |
| sortDirection | string | Sort direction (`asc`, `desc`) | `asc` |

**Response:**

```json
{
  "data": [
    {
      "id": "a08e8f67-4b15-432b-b85c-6598f9dd9c8c",
      "userId": "ce44b918-3ce5-42ba-91c3-99d2cf856143",
      "name": "Alice Johnson",
      "email": "alice@example.com",
      "handicapIndex": 12.3,
      "status": "confirmed",
      "registrationDate": "2023-07-15T10:30:45Z",
      "paymentStatus": "completed",
      "role": "player",
      "flightNumber": 3,
      "startingHole": 1
    },
    // Additional participants...
  ],
  "metadata": {
    "page": 1,
    "pageSize": 50,
    "totalCount": 48,
    "totalPages": 1
  }
}
```

#### Add Tournament Participant

Adds a participant to a tournament.

**Endpoint:** `POST /api/tournaments/{tournamentId}/participants`

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| tournamentId | string (UUID) | Unique identifier of the tournament |

**Request Body:**

```json
{
  "userId": "ce44b918-3ce5-42ba-91c3-99d2cf856143",
  "email": "bob@example.com", // Optional, for new users
  "name": "Bob Smith", // Optional, for new users
  "handicapIndex": 18.2,
  "role": "player",
  "flightNumber": 4,
  "startingHole": 1,
  "notes": "Prefers early tee time"
}
```

**Response:**

```json
{
  "id": "e7dbc6ff-7178-4232-b181-305d9f42f7d4",
  "userId": "ce44b918-3ce5-42ba-91c3-99d2cf856143",
  "name": "Bob Smith",
  "email": "bob@example.com",
  "handicapIndex": 18.2,
  "status": "invited",
  "registrationDate": "2023-08-03T16:45:22Z",
  "paymentStatus": "pending",
  "role": "player",
  "flightNumber": 4,
  "startingHole": 1,
  "notes": "Prefers early tee time"
}
```

### Scoring APIs

#### Submit Score

Submits a score for a player in a tournament round.

**Endpoint:** `POST /api/tournaments/{tournamentId}/rounds/{roundId}/scores`

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| tournamentId | string (UUID) | Unique identifier of the tournament |
| roundId | string (UUID) | Unique identifier of the tournament round |

**Request Body:**

```json
{
  "playerId": "ce44b918-3ce5-42ba-91c3-99d2cf856143",
  "scores": [
    {
      "holeNumber": 1,
      "strokes": 4,
      "putts": 2,
      "fairwayHit": true,
      "greenInRegulation": true,
      "penalties": 0
    },
    {
      "holeNumber": 2,
      "strokes": 5,
      "putts": 3,
      "fairwayHit": false,
      "greenInRegulation": false,
      "penalties": 1
    },
    // Additional holes...
  ],
  "totalScore": 76,
  "verified": false,
  "submittedBy": "7f4df40d-8541-429a-8731-7d177e965d99",
  "notes": "Player had equipment issue on hole 7"
}
```

**Response:**

```json
{
  "id": "6604e390-dc2a-4a6c-bef1-7d8d12c8e7b1",
  "playerId": "ce44b918-3ce5-42ba-91c3-99d2cf856143",
  "tournamentId": "550e8400-e29b-41d4-a716-446655440000",
  "roundId": "a71c7b6a-8f4b-4e61-9c37-920a040c5611",
  "scores": [
    // Array of hole scores as submitted
  ],
  "totalScore": 76,
  "netScore": 64,
  "verified": false,
  "submittedBy": "7f4df40d-8541-429a-8731-7d177e965d99",
  "submittedAt": "2023-08-15T14:32:18Z",
  "updatedAt": "2023-08-15T14:32:18Z",
  "notes": "Player had equipment issue on hole 7"
}
```

#### Get Tournament Leaderboard

Retrieves the current leaderboard for a tournament.

**Endpoint:** `GET /api/tournaments/{tournamentId}/leaderboard`

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| tournamentId | string (UUID) | Unique identifier of the tournament |

**Query Parameters:**

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| type | string | Leaderboard type (`gross`, `net`) | `gross` |
| roundId | string | Filter by specific round | All rounds |
| format | string | Response format (`full`, `summary`) | `full` |

**Response:**

```json
{
  "tournamentId": "550e8400-e29b-41d4-a716-446655440000",
  "tournamentName": "Annual Club Championship",
  "leaderboardType": "gross",
  "lastUpdated": "2023-08-15T16:45:22Z",
  "round": "all",
  "players": [
    {
      "position": 1,
      "playerId": "ce44b918-3ce5-42ba-91c3-99d2cf856143",
      "playerName": "Alice Johnson",
      "handicapIndex": 12.3,
      "totalScore": 145,
      "roundScores": [72, 73],
      "totalToPar": 1,
      "thruHole": 18,
      "today": 73
    },
    {
      "position": 2,
      "playerId": "d8f31b27-79e2-4723-9b42-c8395a189467",
      "playerName": "Carlos Rodriguez",
      "handicapIndex": 8.1,
      "totalScore": 147,
      "roundScores": [75, 72],
      "totalToPar": 3,
      "thruHole": 18,
      "today": 72
    },
    // Additional players...
  ]
}
```

### User Management

#### Get User Profile

Retrieves the profile of a user.

**Endpoint:** `GET /api/users/{userId}`

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| userId | string (UUID) | Unique identifier of the user |

**Response:**

```json
{
  "id": "ce44b918-3ce5-42ba-91c3-99d2cf856143",
  "email": "alice@example.com",
  "displayName": "Alice Johnson",
  "firstName": "Alice",
  "lastName": "Johnson",
  "profilePicture": "https://storage.example.com/profiles/alice.jpg",
  "handicapIndex": 12.3,
  "handicapTrend": "decreasing",
  "memberSince": "2022-03-15T10:00:00Z",
  "preferences": {
    "notifications": {
      "email": true,
      "push": true,
      "sms": false
    },
    "privacy": {
      "showHandicap": true,
      "showScoreHistory": true
    }
  },
  "stats": {
    "tournamentsPlayed": 8,
    "tournamentsWon": 1,
    "averageScore": 85.2,
    "bestScore": 72
  }
}
```

#### Update User Profile

Updates the profile of a user.

**Endpoint:** `PUT /api/users/{userId}`

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| userId | string (UUID) | Unique identifier of the user |

**Request Body:**

```json
{
  "displayName": "Alice J. Smith",
  "firstName": "Alice",
  "lastName": "Smith",
  "preferences": {
    "notifications": {
      "email": true,
      "push": false,
      "sms": true
    }
  }
}
```

**Response:**

```json
{
  "id": "ce44b918-3ce5-42ba-91c3-99d2cf856143",
  "email": "alice@example.com",
  "displayName": "Alice J. Smith",
  "firstName": "Alice",
  "lastName": "Smith",
  "...": "... other profile fields ...",
  "updatedAt": "2023-08-03T17:12:45Z"
}
```

## Edge Function APIs (Supabase)

### Real-Time Scoring Notifications

#### Register For Score Updates

Registers a client for real-time score update notifications.

**Endpoint:** `POST /functions/v1/register-score-updates`

**Request Body:**

```json
{
  "tournamentId": "550e8400-e29b-41d4-a716-446655440000",
  "deviceId": "a1b2c3d4-e5f6-7890-abcd-1234567890ab",
  "playerIds": ["ce44b918-3ce5-42ba-91c3-99d2cf856143", "d8f31b27-79e2-4723-9b42-c8395a189467"],
  "updateFrequency": "immediate"
}
```

**Response:**

```json
{
  "success": true,
  "subscriptionId": "score-updates-5f9a2d87",
  "message": "Successfully registered for score updates"
}
```

### Lightweight Data Access

#### Get Tournament Summary

Gets a lightweight summary of tournament data for display on public pages.

**Endpoint:** `GET /functions/v1/tournament-summary?id=550e8400-e29b-41d4-a716-446655440000`

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| id | string (UUID) | Unique identifier of the tournament |

**Response:**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Annual Club Championship",
  "startDate": "2023-08-15T08:00:00Z",
  "endDate": "2023-08-16T18:00:00Z",
  "status": "active",
  "playerCount": 48,
  "topPlayers": [
    {
      "name": "Alice Johnson",
      "position": 1,
      "score": 72
    },
    {
      "name": "Carlos Rodriguez",
      "position": 2,
      "score": 75
    },
    {
      "name": "Emma Davis",
      "position": 3,
      "score": 76
    }
  ]
}
```

## Webhooks

The system supports webhooks for integration with external systems.

### Webhook Events

| Event | Description | Payload Example |
|-------|-------------|-----------------|
| `tournament.created` | New tournament created | `{"id": "550e8400-e29b-41d4-a716-446655440000", "name": "Annual Club Championship", ...}` |
| `tournament.updated` | Tournament details updated | `{"id": "550e8400-e29b-41d4-a716-446655440000", "name": "Annual Club Championship (Updated)", ...}` |
| `tournament.completed` | Tournament marked as complete | `{"id": "550e8400-e29b-41d4-a716-446655440000", "name": "Annual Club Championship", ...}` |
| `player.registered` | Player registered for tournament | `{"tournamentId": "550e8400-e29b-41d4-a716-446655440000", "playerId": "ce44b918-3ce5-42ba-91c3-99d2cf856143", ...}` |
| `score.submitted` | Score submitted for player | `{"tournamentId": "550e8400-e29b-41d4-a716-446655440000", "playerId": "ce44b918-3ce5-42ba-91c3-99d2cf856143", ...}` |
| `leaderboard.updated` | Leaderboard standings changed | `{"tournamentId": "550e8400-e29b-41d4-a716-446655440000", "timestamp": "2023-08-15T16:45:22Z", ...}` |

### Registering Webhooks

**Endpoint:** `POST /api/webhooks`

**Request Body:**

```json
{
  "url": "https://example.com/webhook-receiver",
  "events": ["tournament.created", "tournament.completed", "score.submitted"],
  "secret": "your_webhook_secret_for_verification",
  "description": "Integration with club management system"
}
```

**Response:**

```json
{
  "id": "wh_123456789",
  "url": "https://example.com/webhook-receiver",
  "events": ["tournament.created", "tournament.completed", "score.submitted"],
  "active": true,
  "createdAt": "2023-08-03T18:00:15Z"
}
```

## Third-Party Integration APIs

### Statistics Export

#### Export Tournament Data

Exports tournament data in various formats for integration with other golf systems.

**Endpoint:** `GET /api/export/tournament/{tournamentId}`

**URL Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| tournamentId | string (UUID) | Unique identifier of the tournament |

**Query Parameters:**

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| format | string | Export format (`json`, `csv`, `xml`) | `json` |
| include | string | Data to include (`scores`, `players`, `all`) | `all` |

**Response:**

For JSON format:
```json
{
  "tournament": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Annual Club Championship",
    "...": "... other tournament fields ..."
  },
  "players": [
    // Array of player data
  ],
  "scores": [
    // Array of score data
  ],
  "exportedAt": "2023-08-03T18:15:30Z"
}
```

### Course Integration

#### Import Course Data

Imports course data from external golf course management systems.

**Endpoint:** `POST /api/courses/import`

**Request Body:**

```json
{
  "name": "Pebble Beach Golf Links",
  "address": {
    "street": "17 Mile Drive",
    "city": "Pebble Beach",
    "state": "CA",
    "zipCode": "93953",
    "country": "USA"
  },
  "holes": [
    {
      "number": 1,
      "par": 4,
      "distance": {
        "championship": 380,
        "regular": 346,
        "forward": 309
      },
      "handicap": 8
    },
    // Additional holes...
  ],
  "totalPar": 72,
  "rating": 75.5,
  "slope": 145,
  "source": "GolfCourseDatabase",
  "sourceId": "PB001"
}
```

**Response:**

```json
{
  "id": "c712b89e-3890-47ba-9297-caaa9561cd23",
  "name": "Pebble Beach Golf Links",
  "...": "... other course fields ...",
  "importedAt": "2023-08-03T18:30:45Z"
}
```

## OpenAPI Specification

The complete OpenAPI/Swagger specification is available at `/api/swagger.json`. You can also explore the API using the Swagger UI at `/api/swagger`.

### Sample OpenAPI Definition

```yaml
openapi: 3.0.0
info:
  title: Golf Tournament Organizer API
  version: 1.0.0
  description: API for managing golf tournaments, participants, and scores
paths:
  /api/tournaments:
    get:
      summary: List tournaments
      operationId: listTournaments
      parameters:
        - name: page
          in: query
          schema:
            type: integer
            default: 1
        # Additional parameters...
      responses:
        '200':
          description: A list of tournaments
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items:
                      $ref: '#/components/schemas/Tournament'
                  metadata:
                    $ref: '#/components/schemas/PaginationMetadata'
    # Additional paths...
components:
  schemas:
    Tournament:
      type: object
      properties:
        id:
          type: string
          format: uuid
        name:
          type: string
        # Additional properties...
    # Additional schemas...
```

## Client SDK Usage Examples

### JavaScript SDK

```javascript
// Initialize the client
const golfClient = new GolfTournamentClient({
  baseUrl: 'https://api.golfapp.example.com',
  authToken: 'your-auth-token'
});

// List tournaments
const tournaments = await golfClient.tournaments.list({
  page: 1,
  pageSize: 20,
  status: 'upcoming'
});

// Create a tournament
const newTournament = await golfClient.tournaments.create({
  name: 'Winter Classic',
  startDate: '2023-12-10T09:00:00Z',
  // Additional fields...
});

// Get leaderboard
const leaderboard = await golfClient.tournaments.getLeaderboard('550e8400-e29b-41d4-a716-446655440000', {
  type: 'gross'
});
```

### C# SDK

```csharp
// Initialize the client
var client = new GolfTournamentClient(
    baseUrl: "https://api.golfapp.example.com",
    authToken: "your-auth-token");

// List tournaments
var tournaments = await client.Tournaments.ListAsync(
    page: 1,
    pageSize: 20,
    status: TournamentStatus.Upcoming);

// Create a tournament
var newTournament = await client.Tournaments.CreateAsync(new CreateTournamentRequest
{
    Name = "Spring Tournament",
    StartDate = DateTime.Parse("2024-04-15T08:00:00Z"),
    // Additional fields...
});

// Get leaderboard
var leaderboard = await client.Tournaments.GetLeaderboardAsync(
    tournamentId: "550e8400-e29b-41d4-a716-446655440000",
    type: LeaderboardType.Gross);
```

## Best Practices for API Usage

1. **Authentication**: Store auth tokens securely and refresh when needed
2. **Rate Limiting**: Implement exponential backoff for rate limit errors
3. **Webhooks**: Validate webhook signatures using your shared secret
4. **Caching**: Cache frequently accessed data like tournament details
5. **Realtime Updates**: Use Supabase subscriptions for low-latency updates
6. **Error Handling**: Check error responses and handle appropriately

```typescript
// Example of proper error handling
try {
  const response = await api.post('/api/tournaments', tournamentData);
  return response.data;
} catch (error) {
  if (error.response) {
    // Server responded with error status
    const status = error.response.status;
    const errorData = error.response.data;
    
    if (status === 400 || status === 422) {
      // Validation error, handle field errors
      return {
        success: false,
        validationErrors: errorData.errors || {}
      };
    } else if (status === 401) {
      // Authentication error, refresh token or redirect to login
      await refreshAuthToken();
      // Retry the request
    } else if (status === 429) {
      // Rate limited, implement backoff
      const retryAfter = parseInt(error.response.headers['retry-after'] || '5');
      await wait(retryAfter * 1000);
      // Retry the request
    } else {
      // General error handling
      return {
        success: false,
        message: errorData.detail || 'An unknown error occurred'
      };
    }
  } else if (error.request) {
    // Request made but no response received (network error)
    return {
      success: false,
      message: 'Network error, please check your connection'
    };
  } else {
    // Error in request setup
    return {
      success: false,
      message: 'Failed to make request'
    };
  }
}
```

## Conclusion

This API documentation provides a comprehensive reference for both internal development and third-party integrations with the Golf Tournament Organizer application. The API is designed to be consistent, secure, and performant, with comprehensive error handling and clear documentation of all endpoints.

For updates to this API documentation, check the developer portal or subscribe to the API changelog.
