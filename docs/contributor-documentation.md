# Contributor Documentation

## Overview

Welcome to the Golf Tournament Organizer application contributor documentation! This guide is designed to help new developers quickly understand the project architecture, set up their development environment, and start contributing effectively. The Golf Tournament Organizer is a modern web application built with React, Supabase, and Azure Functions to facilitate the organization and management of golf tournaments.

## Getting Started

### Prerequisites

Before you begin, ensure you have the following installed:

- **Node.js** (v16+)
- **npm** (v7+) or **yarn** (v1.22+)
- **Git** (v2.30+)
- **VS Code** (recommended) with the following extensions:
  - ESLint
  - Prettier
  - TypeScript
  - React Developer Tools

### Development Environment Setup

1. **Clone the repository**

```bash
git clone https://github.com/your-org/golf-tournament-organizer.git
cd golf-tournament-organizer
```

2. **Install dependencies**

```bash
# Using npm
npm install

# Using yarn
yarn install
```

3. **Set up environment variables**

Create a `.env` file in the root directory by copying the `.env.example` file:

```bash
cp .env.example .env
```

Update the environment variables with your local development values:

```
# Supabase Configuration
REACT_APP_SUPABASE_URL=your_supabase_url
REACT_APP_SUPABASE_ANON_KEY=your_supabase_anon_key

# Azure Functions Configuration
REACT_APP_AZURE_FUNCTION_URL=http://localhost:7071/api

# Feature Flags
REACT_APP_FEATURE_REALTIME_SCORING=true
REACT_APP_FEATURE_OFFLINE_MODE=true
```

4. **Set up local development services**

#### Supabase Local Development

We use Supabase for authentication, database, and real-time functionality. For local development, you can either:

- Use a dedicated development Supabase project (recommended)
- Run Supabase locally using Docker (advanced)

**Option 1: Development Supabase Project**

1. Create a new project at https://app.supabase.io
2. Go to Project Settings > API to find your URL and anon key
3. Update your `.env` file with these values
4. Import the database schema using the provided SQL file:

```bash
# From the project root
cd database
npm run db:dev:setup
```

**Option 2: Local Supabase with Docker**

```bash
# Install Supabase CLI
npm install -g supabase

# Start local Supabase
supabase start

# Initialize database with schema
supabase db reset
```

Update your `.env` file with the local Supabase URL and anon key provided after running `supabase start`.

#### Azure Functions Local Development

1. Install Azure Functions Core Tools:

```bash
# Using npm
npm install -g azure-functions-core-tools@4

# Using brew (macOS)
brew tap azure/functions
brew install azure-functions-core-tools@4
```

2. Start the local Azure Functions runtime:

```bash
cd functions
npm install
npm run start
```

5. **Start the development server**

```bash
# From the project root
npm run dev
```

The application should now be running at `http://localhost:3000`.

## Project Architecture

### High-Level Overview

The Golf Tournament Organizer application follows a modern architecture with the following key components:

1. **Frontend**: React application with TypeScript
2. **Backend**: Combination of Supabase and Azure Functions
3. **Database**: PostgreSQL (via Supabase)
4. **Authentication**: Supabase Auth
5. **Real-time**: Supabase Realtime
6. **Storage**: Supabase Storage

```
┌─────────────────────────────┐
│                             │
│     React Frontend (UI)     │
│                             │
└───────────────┬─────────────┘
                │
                ▼
┌─────────────────────────────┐
│                             │
│  React Context API + Hooks  │
│  (State Management)         │
│                             │
└─┬─────────────────────────┬─┘
  │                         │
  ▼                         ▼
┌────────────┐      ┌────────────────┐
│            │      │                │
│ Supabase   │      │ Azure          │
│ Client     │      │ Functions      │
│            │      │                │
└─┬──────────┘      └───────┬────────┘
  │                         │
  ▼                         ▼
┌────────────┐      ┌────────────────┐
│            │      │                │
│ Supabase   │      │ External       │
│ Services   │      │ Services       │
│            │      │                │
└────────────┘      └────────────────┘
```

### Directory Structure

```
golf-tournament-organizer/
├── public/                  # Static files
├── src/                     # Frontend source code
│   ├── assets/              # Images, icons, etc.
│   ├── components/          # React components
│   │   ├── common/          # Shared components
│   │   ├── layout/          # Layout components
│   │   ├── features/        # Feature-specific components
│   │   └── pages/           # Page components
│   ├── context/             # React context providers
│   ├── hooks/               # Custom React hooks
│   ├── services/            # Service integrations
│   │   ├── supabase/        # Supabase related services
│   │   └── azure/           # Azure Functions related services
│   ├── types/               # TypeScript type definitions
│   ├── utils/               # Helper functions
│   └── App.tsx              # Application root
├── functions/               # Azure Functions
│   ├── src/                 # Functions source code
│   ├── local.settings.json  # Local settings
│   └── host.json            # Azure Functions host configuration
├── database/                # Database schema and migrations
│   ├── migrations/          # SQL migrations
│   └── seeds/               # Seed data
├── docs/                    # Project documentation
└── tests/                   # Test files
```

### Key Technologies

1. **Frontend**:
   - React
   - TypeScript
   - React Router
   - React Query
   - TailwindCSS
   - Date-fns

2. **Backend**:
   - Supabase
   - Azure Functions (Node.js)
   - PostgreSQL

3. **Development Tools**:
   - ESLint
   - Prettier
   - Jest
   - React Testing Library
   - Cypress

## Development Workflow

### Branching Strategy

We use a simplified Git Flow approach:

- `main`: Production branch, always deployable
- `develop`: Development branch, integration branch for features
- `feature/*`: Feature branches for new development
- `bugfix/*`: Bug fix branches
- `release/*`: Release branches
- `hotfix/*`: Hotfix branches for production issues

### Branch Naming Convention

```
<type>/<issue-number>-<short-description>
```

Examples:
- `feature/123-add-scorecard-export`
- `bugfix/456-fix-leaderboard-sorting`
- `hotfix/789-authentication-issue`

### Creating a New Feature

1. Create a new branch from `develop`:

```bash
git checkout develop
git pull origin develop
git checkout -b feature/123-add-scorecard-export
```

2. Make your changes, following the coding standards
3. Write tests for your changes
4. Commit your changes with meaningful commit messages:

```bash
git commit -m "feat: add scorecard export functionality"
```

4. Push your branch and create a pull request:

```bash
git push origin feature/123-add-scorecard-export
```

5. Request a code review from team members

### Commit Message Convention

We follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

Types:
- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `style`: Changes that do not affect the meaning of the code
- `refactor`: Code change that neither fixes a bug nor adds a feature
- `perf`: Code change that improves performance
- `test`: Adding missing tests or correcting existing tests
- `chore`: Changes to the build process or auxiliary tools

Examples:
- `feat(scoring): add hole-by-hole score entry`
- `fix(auth): resolve login issue on Safari`
- `docs: update contributor documentation`

### Pull Request Process

1. Create a pull request from your feature branch to `develop`
2. Fill in the PR template with details about your changes
3. Link the PR to any related issues
4. Request reviews from at least two team members
5. Address any feedback or change requests
6. Once approved, squash and merge your PR

## Coding Standards

### General Guidelines

- Write clean, readable, and maintainable code
- Follow SOLID principles
- Keep components and functions small and focused
- Use meaningful variable and function names
- Document complex logic or business rules
- Write tests for all new features

### TypeScript Guidelines

- Use strict TypeScript typing
- Create interfaces or types for all data structures
- Avoid using `any` type
- Use optional chaining and nullish coalescing when appropriate
- Use enums for defined sets of values

```typescript
// Good
interface Player {
  id: string;
  name: string;
  handicap?: number;
  scores: Score[];
}

// Avoid
const player: any = {
  id: '123',
  name: 'John Doe'
};
```

### React Component Guidelines

- Use functional components with hooks
- Keep components small and focused on a single responsibility
- Use TypeScript props interface for component props
- Extract common logic to custom hooks
- Use React.memo for performance optimization when needed

```typescript
// Component structure
import React from 'react';
import './PlayerCard.css';

interface PlayerCardProps {
  player: Player;
  onScoreUpdate?: (playerId: string, holeId: string, score: number) => void;
}

export const PlayerCard: React.FC<PlayerCardProps> = ({ 
  player, 
  onScoreUpdate 
}) => {
  // Component logic

  return (
    <div className="player-card">
      {/* Component rendering */}
    </div>
  );
};
```

### State Management Guidelines

- Use React Context for global state
- Use local state for component-specific state
- Use custom hooks to encapsulate state logic
- Consider performance implications when using context

```typescript
// Custom hook example
import { useState, useEffect } from 'react';
import { supabase } from '../services/supabase';

export function useTournament(tournamentId: string) {
  const [tournament, setTournament] = useState<Tournament | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    const fetchTournament = async () => {
      try {
        setLoading(true);
        const { data, error } = await supabase
          .from('tournaments')
          .select('*, courses(*)')
          .eq('id', tournamentId)
          .single();

        if (error) throw error;
        setTournament(data);
      } catch (err) {
        setError(err as Error);
      } finally {
        setLoading(false);
      }
    };

    fetchTournament();
  }, [tournamentId]);

  return { tournament, loading, error };
}
```

## Testing Strategy

### Unit Testing

- Use Jest and React Testing Library
- Test individual components and hooks
- Mock external services and API calls
- Focus on behavior, not implementation details

```typescript
// Example component test
import { render, screen, fireEvent } from '@testing-library/react';
import { PlayerCard } from './PlayerCard';

describe('PlayerCard', () => {
  const mockPlayer = {
    id: 'player-1',
    name: 'John Doe',
    handicap: 12,
    scores: []
  };

  it('renders player name correctly', () => {
    render(<PlayerCard player={mockPlayer} />);
    expect(screen.getByText('John Doe')).toBeInTheDocument();
  });

  it('calls onScoreUpdate when score is changed', () => {
    const handleScoreUpdate = jest.fn();
    render(
      <PlayerCard player={mockPlayer} onScoreUpdate={handleScoreUpdate} />
    );
    
    fireEvent.click(screen.getByRole('button', { name: /update score/i }));
    fireEvent.change(screen.getByLabelText(/score/i), { target: { value: '4' } });
    fireEvent.click(screen.getByRole('button', { name: /save/i }));
    
    expect(handleScoreUpdate).toHaveBeenCalledWith('player-1', expect.any(String), 4);
  });
});
```

### Integration Testing

- Test interactions between components
- Test data flow through multiple components
- Test integration with services

### End-to-End Testing

- Use Cypress for end-to-end testing
- Test critical user flows
- Test responsive behavior

```typescript
// Example Cypress test
describe('Tournament Creation', () => {
  beforeEach(() => {
    cy.login('organizer@example.com', 'password');
    cy.visit('/tournaments/create');
  });

  it('should create a new tournament', () => {
    cy.get('[data-testid=tournament-name]').type('Summer Classic 2023');
    cy.get('[data-testid=tournament-date]').type('2023-07-15');
    cy.get('[data-testid=course-selector]').click();
    cy.get('[data-testid=course-option-1]').click();
    cy.get('[data-testid=create-button]').click();
    
    cy.url().should('include', '/tournaments/');
    cy.contains('Summer Classic 2023').should('be.visible');
  });
});
```

### Running Tests

```bash
# Run unit and integration tests
npm test

# Run tests in watch mode
npm test -- --watch

# Run end-to-end tests
npm run cy:run

# Open Cypress test runner
npm run cy:open
```

## Working with Supabase

### Database Schema

Our PostgreSQL database schema is defined in SQL migrations and includes the following core tables:

- `users`: User information
- `tournaments`: Tournament details
- `courses`: Golf course information
- `players`: Tournament participants
- `scores`: Player scores by hole
- `teams`: Team information for team tournaments
- `leaderboards`: Computed leaderboard data

### Authentication

We use Supabase Authentication for user management:

```typescript
// Sign up a new user
const signUp = async (email: string, password: string) => {
  const { user, error } = await supabase.auth.signUp({
    email,
    password,
  });
  
  if (error) throw error;
  return user;
};

// Sign in a user
const signIn = async (email: string, password: string) => {
  const { user, error } = await supabase.auth.signInWithPassword({
    email,
    password,
  });
  
  if (error) throw error;
  return user;
};

// Sign out
const signOut = async () => {
  const { error } = await supabase.auth.signOut();
  if (error) throw error;
};
```

### Row-Level Security (RLS)

We implement row-level security in Supabase for data protection. Example RLS policy:

```sql
-- Allow users to read tournaments they are participating in or organizing
CREATE POLICY "Users can view tournaments they participate in" ON tournaments
  FOR SELECT
  USING (
    auth.uid() IN (
      SELECT user_id FROM tournament_participants WHERE tournament_id = id
    ) OR
    auth.uid() = organizer_id
  );

-- Allow tournament organizers to update their tournaments
CREATE POLICY "Organizers can update tournaments" ON tournaments
  FOR UPDATE
  USING (auth.uid() = organizer_id);
```

### Real-time Subscriptions

We use Supabase's real-time functionality for live updates:

```typescript
// Subscribe to tournament scores
const subscribeToScores = (tournamentId: string, callback: (payload: any) => void) => {
  const subscription = supabase
    .from(`scores:tournament_id=eq.${tournamentId}`)
    .on('INSERT', callback)
    .on('UPDATE', callback)
    .subscribe();
  
  return () => {
    supabase.removeSubscription(subscription);
  };
};
```

## Working with Azure Functions

### Function Structure

Azure Functions are used for complex operations, external integrations, and tasks that require server-side execution:

```typescript
// Example Azure Function
import { AzureFunction, Context, HttpRequest } from "@azure/functions";

const httpTrigger: AzureFunction = async function (
  context: Context,
  req: HttpRequest
): Promise<void> {
  const tournamentId = req.params.tournamentId;
  
  try {
    // Function logic here
    const result = await processTournamentData(tournamentId);
    
    context.res = {
      status: 200,
      body: result
    };
  } catch (error) {
    context.log.error(`Error processing tournament ${tournamentId}: ${error.message}`);
    context.res = {
      status: 500,
      body: {
        error: "Failed to process tournament data"
      }
    };
  }
};

export default httpTrigger;
```

### Calling Azure Functions

```typescript
// Service to call Azure Functions
export const calculateLeaderboard = async (tournamentId: string) => {
  const response = await fetch(
    `${process.env.REACT_APP_AZURE_FUNCTION_URL}/calculateLeaderboard/${tournamentId}`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
    }
  );
  
  if (!response.ok) {
    throw new Error('Failed to calculate leaderboard');
  }
  
  return await response.json();
};
```

## Deployment

### Development Environment

1. Frontend: Vercel Preview Deployments
2. Backend: Supabase Development Project
3. Functions: Azure Functions Development Slot

### Staging Environment

1. Frontend: Vercel Staging Deployment
2. Backend: Supabase Staging Project
3. Functions: Azure Functions Staging Slot

### Production Environment

1. Frontend: Vercel Production Deployment
2. Backend: Supabase Production Project
3. Functions: Azure Functions Production Slot

### Deployment Process

1. Merge features into `develop` for automatic deployment to development environment
2. Create a release branch for staging deployment
3. After testing in staging, merge to `main` for production deployment

## Troubleshooting

### Common Development Issues

1. **Supabase Connection Issues**

Check your environment variables and ensure your Supabase project is correctly set up:

```bash
# Verify environment variables
cat .env | grep SUPABASE

# Test connection
npx supabase-js-cli connection-test --url $REACT_APP_SUPABASE_URL --key $REACT_APP_SUPABASE_ANON_KEY
```

2. **Azure Functions Local Development Issues**

```bash
# Check if Azure Functions Core Tools is installed
func --version

# Verify local.settings.json exists
ls -la functions/local.settings.json

# Check Azure Functions logs
func logs
```

3. **Type Errors**

```bash
# Run TypeScript compiler check
npm run tsc

# Update TypeScript definitions if needed
npm run generate-types
```

### Debugging Tools

1. **React Developer Tools**
   - Inspect component hierarchy
   - Monitor component re-renders
   - Check props and state

2. **Redux DevTools** (if using Redux)
   - Track state changes
   - Time-travel debugging

3. **Chrome DevTools**
   - Network tab for API requests
   - Console for errors
   - Sources tab for debugging

4. **Supabase Dashboard**
   - Database explorer
   - Real-time inspector
   - Authentication logs

## Contributing to Documentation

Documentation is an essential part of our project. Here's how to contribute:

1. Update relevant documentation when making code changes
2. Use markdown for all documentation files
3. Place documentation in the appropriate directory:
   - `/docs`: General project documentation
   - `/src/components/*/README.md`: Component-specific documentation
   - `/functions/README.md`: Azure Functions documentation

## Getting Help

### Team Communication

- **Slack**: Join the #golf-app-development channel
- **GitHub Discussions**: For longer technical discussions
- **Weekly Sync Meetings**: Thursday at 10am EST

### Resources

- [Project Wiki](https://github.com/your-org/golf-tournament-organizer/wiki)
- [Supabase Documentation](https://supabase.io/docs)
- [React Documentation](https://reactjs.org/docs)
- [Azure Functions Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/)

## Conclusion

This contributor documentation should provide you with the necessary information to get started contributing to the Golf Tournament Organizer application. If you encounter any issues or have questions not addressed here, please reach out to the team through the appropriate channels.

Welcome aboard, and happy coding!
