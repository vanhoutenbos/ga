# Golf App

A comprehensive golf tournament organizer application for managing, scoring, and viewing golf tournaments.

## Features

- Tournament creation and management
- Mobile score entry with offline capability
- Real-time leaderboards and statistics
- Player and flight management
- Multi-language support (English and Dutch)
- Track golf courses across the Netherlands and globally
- View detailed information about courses, tees, and holes

## Data Structure

The application uses a hierarchical data structure:
- Golf Clubs
  - Courses
    - Tees
      - Holes
      - Playing Handicaps (PHCP)

## API Integration

The app integrates with the e-golf4u API to fetch course data.

## Local Development Setup

### Prerequisites

1. **Required Software**:
   - [Node.js](https://nodejs.org/) (v16 or higher)
   - [Docker Desktop](https://www.docker.com/products/docker-desktop) (for Supabase local development)
   - [Supabase CLI](https://supabase.com/docs/reference/cli) - Install with `npm install -g supabase`
   - [PowerShell](https://docs.microsoft.com/en-us/powershell/) 5.1 or higher (for Windows users)
   - [Git](https://git-scm.com/downloads) for version control

2. **Clone the Repository**:
   ```powershell
   git clone https://github.com/yourusername/the-golfapp.git
   cd the-golfapp
   ```

3. **Install Dependencies**:
   ```powershell
   npm install
   ```

### Setting up Supabase Locally

The application uses Supabase for database, authentication, and edge functions. Set up the local Supabase environment using our helper script:

1. **Complete Setup** (all-in-one command):
   ```powershell
   ./dev.ps1 setup
   ```
   This will:
   - Start the local Supabase instance
   - Apply all database migrations
   - Seed the development data
   - Generate TypeScript types

2. **Step-by-Step Setup** (if you prefer more control):
   ```powershell
   # Start Supabase services
   ./dev.ps1 start
   
   # Apply database migrations
   ./dev.ps1 migrate
   
   # Seed development data
   ./dev.ps1 seed
   
   # Generate TypeScript types
   ./dev.ps1 types
   ```

3. **Access Local Supabase**:
   - Supabase Studio: [http://localhost:54323](http://localhost:54323)
   - API Endpoint: [http://localhost:54321](http://localhost:54321)
   - Database Connection:
     - Host: `localhost`
     - Port: `54322`
     - Database: `postgres`
     - Username: `postgres`
     - Password: `postgres`

### Running Edge Functions

For testing the API rate limiting and other edge functions:

```powershell
./dev.ps1 serve
```

### Database Management

- **Create a new migration**:
  ```powershell
  ./dev.ps1 migration your_migration_name
  ```

- **Reset the database** (caution: this deletes all data):
  ```powershell
  ./dev.ps1 reset
  ```

- **Get database status**:
  ```powershell
  ./dev.ps1 status
  ```

### Running the Application

1. **Start the frontend development server**:
   ```powershell
   npm run dev
   ```

2. **Access the application**:
   Open your browser and navigate to [http://localhost:3000](http://localhost:3000)

### Working with Golf Course Data

Run the included script to update golf course data:
```powershell
./fetch_golf_data.ps1
```

## Deployment

The application can be deployed to both development and production environments:

- **Deploy to Development**:
  ```powershell
  ./dev.ps1 deploy-dev
  ```

- **Deploy to Production**:
  ```powershell
  ./dev.ps1 deploy-prod
  ```

## Key Supabase Features Used

- **Database**: PostgreSQL with row-level security policies
- **Authentication**: User management and role-based access control
- **Edge Functions**: Rate limiting and API optimizations
- **Realtime**: Live tournament leaderboards and scoring updates
- **Caching**: Client-side caching with offline support

## Multi-Language Support

The application fully supports:
- English (default)
- Dutch

Community translations can be contributed following the guidelines in our [multi-language strategy](docs/multi-language-strategy.md).

## Documentation

For detailed documentation on specific aspects of the application:
- [API Documentation](docs/api-documentation.md)
- [Supabase Implementation](docs/supabase-implementation.md)
- [Caching Strategy](docs/api-caching-strategy.md)
- [Rate Limiting Implementation](docs/api-rate-limiting-abuse-prevention.md)

## License

[MPL License](LICENSE)
