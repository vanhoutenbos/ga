# Project Structure and Implementation Plan

This document outlines the detailed project structure and implementation approach for the Golf Tournament Organizer application. It serves as a technical guide for developers implementing the React-based PWA with Supabase backend integration.

## Table of Contents

1. [Technology Stack](#technology-stack)
2. [Project Structure](#project-structure)
3. [Frontend Implementation](#frontend-implementation)
4. [Backend Implementation](#backend-implementation)
5. [Database Schema Integration](#database-schema-integration)
6. [Authentication Flow](#authentication-flow)
7. [Offline Implementation](#offline-implementation)
8. [Internationalization Approach](#internationalization-approach)
9. [Key Feature Implementation](#key-feature-implementation)
10. [CI/CD Pipeline](#cicd-pipeline)
11. [Testing Strategy](#testing-strategy)
12. [Performance Optimization](#performance-optimization)
13. [Planned Caching and Performance Optimization (Future Version)](#planned-caching-and-performance-optimization-future-version)
14. [TODOs and Open Questions](#todos-and-open-questions)

## Technology Stack

### Frontend
- **Core Framework**: React 18+ with TypeScript
- **State Management**: React Context API with custom hooks, Redux Toolkit for complex state
- **Routing**: React Router v6+
- **Styling**: Tailwind CSS with custom design system
- **Component Library**: Headless UI + custom components
- **PWA Capabilities**: Workbox for service worker management
- **API Communication**: Axios with custom interceptors, SWR for data fetching
- **Offline Storage**: IndexedDB with Dexie.js wrapper
- **Form Handling**: React Hook Form with Zod validation
- **Internationalization**: react-i18next with JSON-based translation files
- **Data Visualization**: Recharts for statistics and leaderboards
- **Date Handling**: date-fns for date operations

### Backend
- **API Layer**: Azure Functions with .NET 8
- **Authentication**: Supabase Auth with JWT validation
- **Database**: PostgreSQL via Supabase
- **Real-time Updates**: Supabase Realtime subscriptions
- **Storage**: Supabase Storage for user uploads
- **Monitoring**: Application Insights

## Project Structure

The application follows a feature-based architecture with shared core functionality:

```
src/
├── assets/                 # Static assets (images, icons, etc.)
├── components/             # Shared UI components
│   ├── common/             # Reusable primitive components
│   │   ├── Button.tsx
│   │   ├── Card.tsx
│   │   ├── Input.tsx
│   │   └── ...
│   ├── layout/             # Layout components
│   │   ├── AppShell.tsx
│   │   ├── Header.tsx
│   │   ├── Footer.tsx
│   │   └── ...
│   └── domain/             # Domain-specific components
│       ├── tournament/
│       ├── scorecard/
│       └── ...
├── contexts/               # React contexts for state management
│   ├── AuthContext.tsx
│   ├── OfflineContext.tsx
│   └── ...
├── features/               # Feature modules
│   ├── auth/               # Authentication feature
│   │   ├── components/     # Feature-specific components
│   │   ├── hooks/          # Feature-specific hooks
│   │   ├── services/       # Feature-specific services
│   │   ├── types.ts        # TypeScript types for the feature
│   │   └── index.ts        # Feature public API
│   ├── home/               # Home page feature
│   ├── tournaments/        # Tournament management feature
│   ├── scoring/            # Score recording feature
│   ├── leaderboard/        # Leaderboard viewing feature
│   └── ...
├── hooks/                  # Shared custom hooks
│   ├── useApi.ts
│   ├── useOffline.ts
│   └── ...
├── lib/                    # Utility libraries and configurations
│   ├── api.ts              # API client configuration
│   ├── supabase.ts         # Supabase client configuration
│   ├── i18n.ts             # i18n configuration
│   └── ...
├── locales/                # Translation files
│   ├── en/                 # English translations
│   └── nl/                 # Dutch translations
├── pages/                  # Top-level route components
│   ├── HomePage.tsx
│   ├── TournamentPage.tsx
│   ├── LeaderboardPage.tsx
│   └── ...
├── services/               # Shared services
│   ├── api/                # API interaction services
│   ├── auth/               # Authentication services
│   ├── storage/            # Storage services
│   └── ...
├── types/                  # Global TypeScript types
│   ├── api.ts              # API-related types
│   ├── models.ts           # Domain model types
│   └── ...
├── utils/                  # Utility functions
│   ├── date.ts
│   ├── format.ts
│   └── ...
├── App.tsx                 # Main application component
├── index.tsx               # Entry point
├── routes.tsx              # Route definitions
└── service-worker.ts       # Service worker for PWA
```

## Frontend Implementation

### PWA Configuration

The application will be configured as a Progressive Web App with the following features:

```typescript
// public/manifest.json
{
  "name": "Golf Tournament Organizer",
  "short_name": "GolfApp",
  "start_url": "/",
  "display": "standalone",
  "background_color": "#ffffff",
  "theme_color": "#3b7d35",
  "icons": [
    {
      "src": "icons/icon-72x72.png",
      "sizes": "72x72",
      "type": "image/png"
    },
    // Additional icon sizes...
  ]
}

// src/service-worker.ts
import { clientsClaim } from 'workbox-core';
import { precacheAndRoute, createHandlerBoundToURL } from 'workbox-precaching';
import { registerRoute } from 'workbox-routing';
import { StaleWhileRevalidate, CacheFirst } from 'workbox-strategies';
import { ExpirationPlugin } from 'workbox-expiration';
import { CacheableResponsePlugin } from 'workbox-cacheable-response';

clientsClaim();
precacheAndRoute(self.__WB_MANIFEST);

// Cache page navigations
registerRoute(
  ({ request }) => request.mode === 'navigate',
  new StaleWhileRevalidate({
    cacheName: 'pages-cache',
  })
);

// Cache the API responses
registerRoute(
  ({ url }) => url.pathname.startsWith('/api/'),
  new StaleWhileRevalidate({
    cacheName: 'api-cache',
    plugins: [
      new CacheableResponsePlugin({
        statuses: [0, 200],
      }),
      new ExpirationPlugin({
        maxEntries: 50,
        maxAgeSeconds: 30 * 60, // 30 minutes
      }),
    ],
  })
);

// Cache static assets
registerRoute(
  ({ request }) => 
    request.destination === 'style' ||
    request.destination === 'script' ||
    request.destination === 'font',
  new CacheFirst({
    cacheName: 'static-resources',
    plugins: [
      new CacheableResponsePlugin({
        statuses: [0, 200],
      }),
      new ExpirationPlugin({
        maxEntries: 60,
        maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
      }),
    ],
  })
);
```

### Application Routing

The application will use React Router v6 for routing with code splitting:

```typescript
// src/routes.tsx
import { Suspense, lazy } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { LoadingSpinner } from './components/common/LoadingSpinner';
import { useAuth } from './hooks/useAuth';

// Lazy-loaded components for code splitting
const HomePage = lazy(() => import('./pages/HomePage'));
const LoginPage = lazy(() => import('./pages/LoginPage'));
const RegisterPage = lazy(() => import('./pages/RegisterPage'));
const TournamentsPage = lazy(() => import('./pages/TournamentsPage'));
const TournamentDetailsPage = lazy(() => import('./pages/TournamentDetailsPage'));
const LeaderboardPage = lazy(() => import('./pages/LeaderboardPage'));
const ScoringPage = lazy(() => import('./pages/ScoringPage'));
const PlayerProfilePage = lazy(() => import('./pages/PlayerProfilePage'));
const AdminDashboardPage = lazy(() => import('./pages/AdminDashboardPage'));

// Protected route component
const ProtectedRoute = ({ children, roles = [] }) => {
  const { user, isAuthenticated } = useAuth();
  
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  
  if (roles.length > 0 && !roles.some(role => user.roles.includes(role))) {
    return <Navigate to="/" replace />;
  }
  
  return children;
};

// Main routes component
export const AppRoutes = () => {
  return (
    <Suspense fallback={<LoadingSpinner />}>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/tournaments" element={<TournamentsPage />} />
        <Route path="/tournaments/:id" element={<TournamentDetailsPage />} />
        <Route path="/leaderboard/:id" element={<LeaderboardPage />} />
        
        {/* Protected routes */}
        <Route path="/scoring/:id" element={
          <ProtectedRoute roles={['player', 'recorder', 'admin']}>
            <ScoringPage />
          </ProtectedRoute>
        } />
        
        <Route path="/profile" element={
          <ProtectedRoute>
            <PlayerProfilePage />
          </ProtectedRoute>
        } />
        
        <Route path="/admin/*" element={
          <ProtectedRoute roles={['admin', 'organizer']}>
            <AdminDashboardPage />
          </ProtectedRoute>
        } />
        
        {/* Fallback route */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  );
};
```

### State Management

The application will use a combination of React Context API and custom hooks for state management:

```typescript
// src/contexts/AuthContext.tsx
import { createContext, useContext, useState, useEffect } from 'react';
import { supabase } from '../lib/supabase';
import { User } from '../types/auth';

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  signInWithEmail: (email: string, password: string) => Promise<void>;
  signInWithGoogle: () => Promise<any>;
  signInWithFacebook: () => Promise<any>;
  signInWithApple: () => Promise<any>;
  signInWithTwitter: () => Promise<any>;
  signOut: () => Promise<void>;
  hasRole: (tenantId: string, role: string) => boolean;
  hasAnyRole: (tenantId: string, roles: string[]) => boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  
  useEffect(() => {
    // Check for existing session
    const checkUser = async () => {
      const { data, error } = await supabase.auth.getSession();
      
      if (data?.session) {
        // Fetch additional user data from profiles table
        const { data: profileData } = await supabase
          .from('profiles')
          .select('*')
          .eq('id', data.session.user.id)
          .single();
          
        // Get tenant roles for the user
        const { data: tenantRoles } = await supabase
          .from('tenant_users')
          .select('tenant_id, role')
          .eq('user_id', data.session.user.id);
          
        // Construct full user object with profile and roles
        setUser({
          ...data.session.user,
          profile: profileData || {},
          roles: tenantRoles || []
        });
        
        // Cache roles for offline access if needed
        if (tenantRoles) {
          for (const tenantRole of tenantRoles) {
            await offlineDB.cacheUserRole(
              data.session.user.id,
              tenantRole.tenant_id,
              tenantRole.role
            );
          }
        }
      }
      
      setIsLoading(false);
    };
    
    checkUser();
    
    // Set up auth state change listener
    const { data } = supabase.auth.onAuthStateChange(async (event, session) => {
      if (session) {
        // Fetch profile data and set user
        const { data: profileData } = await supabase
          .from('profiles')
          .select('*')
          .eq('id', session.user.id)
          .single();
          
        // Get tenant roles for the user
        const { data: tenantRoles } = await supabase
          .from('tenant_users')
          .select('tenant_id, role')
          .eq('user_id', session.user.id);
          
        // Construct full user object with profile and roles
        setUser({
          ...session.user,
          profile: profileData || {},
          roles: tenantRoles || []
        });
        
        // Cache roles for offline access
        if (tenantRoles) {
          for (const tenantRole of tenantRoles) {
            await offlineDB.cacheUserRole(
              session.user.id,
              tenantRole.tenant_id,
              tenantRole.role
            );
          }
        }
      } else {
        setUser(null);
      }
      
      setIsLoading(false);
    });
    
    return () => {
      data?.subscription.unsubscribe();
    };
  }, []);
  
  // Social login methods
  const signInWithEmail = async (email: string, password: string) => {
    setIsLoading(true);
    const { error } = await supabase.auth.signInWithPassword({ email, password });
    
    if (error) {
      throw error;
    }
    
    setIsLoading(false);
  };
  
  const signInWithGoogle = async () => {
    return supabase.auth.signInWithOAuth({
      provider: 'google',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`
      }
    });
  };
  
  const signInWithFacebook = async () => {
    return supabase.auth.signInWithOAuth({
      provider: 'facebook',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`
      }
    });
  };
  
  const signInWithApple = async () => {
    return supabase.auth.signInWithOAuth({
      provider: 'apple',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`
      }
    });
  };
  
  const signInWithTwitter = async () => {
    return supabase.auth.signInWithOAuth({
      provider: 'twitter',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`
      }
    });
  };
  
  const signOut = async () => {
    setIsLoading(true);
    const { error } = await supabase.auth.signOut();
    
    if (error) {
      throw error;
    }
    
    setUser(null);
    setIsLoading(false);
  };
  
  // Check if user has a specific role for a tenant
  const hasRole = (tenantId: string, role: string) => {
    if (!user || !user.roles) return false;
    
    return user.roles.some(
      tenantRole => tenantRole.tenant_id === tenantId && tenantRole.role === role
    );
  };
  
  // Check if user has any of the required roles for a tenant
  const hasAnyRole = (tenantId: string, roles: string[]) => {
    if (!user || !user.roles) return false;
    
    return user.roles.some(
      tenantRole => tenantRole.tenant_id === tenantId && roles.includes(tenantRole.role)
    );
  };
  
  const value = {
    user,
    isAuthenticated: !!user,
    isLoading,
    signInWithEmail,
    signInWithGoogle,
    signInWithFacebook,
    signInWithApple,
    signInWithTwitter,
    signOut,
    hasRole,
    hasAnyRole
  };
  
  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  
  return context;
};
```

## Backend Implementation

### API Services Integration

The application will integrate with Supabase for most operations and Azure Functions for complex business logic:

```typescript
// src/lib/supabase.ts
import { createClient } from '@supabase/supabase-js';
import { Database } from '../types/supabase';

const supabaseUrl = process.env.REACT_APP_SUPABASE_URL!;
const supabaseAnonKey = process.env.REACT_APP_SUPABASE_ANON_KEY!;

export const supabase = createClient<Database>(supabaseUrl, supabaseAnonKey);

// src/lib/api.ts
import axios from 'axios';
import { supabase } from './supabase';

const baseURL = process.env.REACT_APP_API_URL || 'https://api.golfapp.com';

export const apiClient = axios.create({
  baseURL,
  headers: {
    'Content-Type': 'application/json'
  }
});

// Add auth token to requests
apiClient.interceptors.request.use(async (config) => {
  const session = await supabase.auth.getSession();
  
  if (session?.data?.session?.access_token) {
    config.headers.Authorization = `Bearer ${session.data.session.access_token}`;
  }
  
  return config;
});

// Handle errors
apiClient.interceptors.response.use(
  response => response,
  error => {
    // Handle offline scenario
    if (!navigator.onLine) {
      // Queue request for later, when online
      // This will be handled by the service worker
    }
    
    return Promise.reject(error);
  }
);
```

### Tournament Service

Example implementation for tournament-related operations:

```typescript
// src/services/api/tournamentService.ts
import { apiClient } from '../../lib/api';
import { supabase } from '../../lib/supabase';
import { Tournament, TournamentPlayer, Flight } from '../../types/models';

export const tournamentService = {
  // Get all tournaments (with optional filtering)
  async getTournaments(filters = {}) {
    const query = supabase
      .from('tournaments')
      .select('*, course:courses(*)');
      
    // Apply filters
    Object.entries(filters).forEach(([key, value]) => {
      query.eq(key, value);
    });
    
    const { data, error } = await query;
    
    if (error) {
      throw error;
    }
    
    return data;
  },
  
  // Get a specific tournament with related data
  async getTournament(id: string) {
    const { data, error } = await supabase
      .from('tournaments')
      .select(`
        *,
        course:courses(*),
        tournament_players(*),
        flights(*),
        tee_times(*)
      `)
      .eq('id', id)
      .single();
      
    if (error) {
      throw error;
    }
    
    return data;
  },
  
  // Create a new tournament
  async createTournament(tournament: Omit<Tournament, 'id'>) {
    // For complex operations, use Azure Functions
    return apiClient.post('/tournaments', tournament);
  },
  
  // Real-time subscription to tournament updates
  subscribeToTournament(id: string, callback: (payload: any) => void) {
    const subscription = supabase
      .channel(`tournament:${id}`)
      .on(
        'postgres_changes',
        {
          event: '*',
          schema: 'public',
          table: 'tournaments',
          filter: `id=eq.${id}`
        },
        callback
      )
      .subscribe();
      
    return () => {
      supabase.removeChannel(subscription);
    };
  }
};
```

## Database Schema Integration

The application will use the following TypeScript types that correspond to the PostgreSQL schema:

```typescript
// src/types/models.ts
export interface Tenant {
  id: string;
  name: string;
  domain: string | null;
  plan_type: 'free' | 'basic' | 'premium' | 'enterprise';
  is_active: boolean;
  settings: Record<string, any> | null;
  created_at: string;
  updated_at: string;
}

export interface TenantUser {
  id: string;
  tenant_id: string;
  user_id: string;
  role: 'admin' | 'organizer' | 'player' | 'recorder' | 'spectator';
  is_active: boolean;
  created_at: string;
  updated_at: string;
}

export interface Profile {
  id: string;
  display_name: string;
  first_name: string | null;
  last_name: string | null;
  email: string | null;
  phone: string | null;
  handicap: number | null;
  profile_picture_url: string | null;
  created_at: string;
  updated_at: string;
}

export interface Course {
  id: string;
  tenant_id: string;
  name: string;
  location: string | null;
  address: string | null;
  city: string | null;
  state: string | null;
  postal_code: string | null;
  country: string | null;
  website: string | null;
  phone: string | null;
  total_holes: 9 | 18 | 27 | 36;
  par: number | null;
  course_rating: number | null;
  slope_rating: number | null;
  is_public: boolean;
  created_by: string | null;
  created_at: string;
  updated_at: string;
  
  // Relationships (not in the database)
  tees?: Tee[];
}

export interface Tee {
  id: string;
  course_id: string;
  name: string;
  color: string | null;
  gender: 'male' | 'female' | 'unisex' | null;
  par: number | null;
  course_rating: number | null;
  slope_rating: number | null;
  created_at: string;
  updated_at: string;
  
  // Relationships (not in the database)
  holes?: Hole[];
}

export interface Hole {
  id: string;
  tee_id: string;
  hole_number: number;
  par: number;
  stroke_index: number;
  distance_meters: number | null;
  distance_yards: number | null;
  created_at: string;
  updated_at: string;
}

export interface Tournament {
  id: string;
  tenant_id: string;
  name: string;
  description: string | null;
  course_id: string | null;
  start_date: string;
  end_date: string;
  registration_deadline: string | null;
  status: 'draft' | 'registration' | 'active' | 'completed' | 'cancelled';
  format: 'stroke' | 'stableford' | 'match';
  is_public: boolean;
  max_players: number | null;
  organizer_name: string | null;
  organizer_email: string | null;
  organizer_phone: string | null;
  logo_url: string | null;
  banner_url: string | null;
  settings: Record<string, any> | null;
  created_by: string;
  created_at: string;
  updated_at: string;
  
  // Relationships (not in the database)
  course?: Course;
  tournament_players?: TournamentPlayer[];
  flights?: Flight[];
}

export interface TournamentPlayer {
  id: string;
  tournament_id: string;
  player_id: string | null;
  player_name: string;
  email: string | null;
  phone: string | null;
  handicap: number | null;
  tee_id: string | null;
  status: 'registered' | 'confirmed' | 'withdrawn';
  created_at: string;
  updated_at: string;
  
  // Relationships (not in the database)
  tee?: Tee;
  scores?: Score[];
}

export interface Flight {
  id: string;
  tournament_id: string;
  name: string;
  start_time: string | null;
  end_time: string | null;
  created_at: string;
  updated_at: string;
  
  // Relationships (not in the database)
  players?: TournamentPlayer[];
}

export interface Score {
  id: string;
  tournament_id: string;
  player_id: string;
  hole_number: number;
  strokes: number | null;
  putts: number | null;
  penalties: number | null;
  fairway_hit: boolean | null;
  green_in_regulation: boolean | null;
  recorded_by: string | null;
  recorded_at: string;
  status: 'draft' | 'submitted' | 'confirmed' | 'rejected';
  created_at: string;
  updated_at: string;
}
```

## Authentication Flow and User Roles

### User Role System

The application implements a comprehensive role-based access control system with the following user roles:

1. **Admin**
   - Full system access
   - Manage all tournaments across tenants
   - Configure global settings
   - Access to analytics and reporting

2. **Tournament Organizer**
   - Create and manage tournaments
   - Invite players and assign recorders
   - Manage course information
   - View and export tournament data

3. **Player**
   - Register for tournaments
   - Enter personal scores (if allowed by tournament settings)
   - View leaderboards and personal performance
   - Access their player profile

4. **Score Recorder**
   - Input scores for assigned players/groups
   - View and edit submitted scores
   - Mark scorecards as complete

5. **Spectator**
   - View tournament information and leaderboards
   - No ability to modify data
   - Public access to specific tournament views

### Authentication Implementation

The authentication system will be implemented using Supabase Auth with the following features:

```typescript
// src/lib/auth.ts
import { supabase } from './supabase';

export const authService = {
  // Email/password authentication
  async signInWithEmail(email: string, password: string) {
    return supabase.auth.signInWithPassword({ email, password });
  },
  
  async signUpWithEmail(email: string, password: string, userData = {}) {
    return supabase.auth.signUp({
      email,
      password,
      options: {
        data: userData
      }
    });
  },
  
  // Social login providers - implemented for future use
  async signInWithGoogle() {
    return supabase.auth.signInWithOAuth({
      provider: 'google',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`
      }
    });
  },
  
  async signInWithFacebook() {
    return supabase.auth.signInWithOAuth({
      provider: 'facebook',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`
      }
    });
  },
  
  async signInWithApple() {
    return supabase.auth.signInWithOAuth({
      provider: 'apple',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`
      }
    });
  },
  
  async signInWithTwitter() {
    return supabase.auth.signInWithOAuth({
      provider: 'twitter',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`
      }
    });
  },
  
  // Session management
  async getCurrentUser() {
    const { data } = await supabase.auth.getUser();
    return data?.user || null;
  },
  
  async signOut() {
    return supabase.auth.signOut();
  },
  
  // Role management
  async getUserRole(userId: string, tenantId: string) {
    const { data, error } = await supabase
      .from('tenant_users')
      .select('role')
      .eq('user_id', userId)
      .eq('tenant_id', tenantId)
      .single();
    
    if (error) {
      console.error('Error fetching user role:', error);
      return null;
    }
    
    return data?.role || null;
  },
  
  // Permission checking
  hasPermission(userRole: string, requiredRoles: string[]) {
    return requiredRoles.includes(userRole);
  }
};

// src/features/auth/components/LoginForm.tsx
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../../hooks/useAuth';
import { Button } from '../../../components/common/Button';
import { Input } from '../../../components/common/Input';
import { Alert } from '../../../components/common/Alert';
import { Divider } from '../../../components/common/Divider';
import { GoogleIcon, FacebookIcon, AppleIcon, TwitterIcon } from '../../../components/icons';

export const LoginForm = () => {
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { signInWithEmail, signInWithGoogle, signInWithFacebook, signInWithApple, signInWithTwitter } = useAuth();
  const navigate = useNavigate();
  
  const handleEmailSubmit = async (e) => {
    e.preventDefault();
    setErrorMessage('');
    setIsLoading(true);
    
    try {
      await signInWithEmail(email, password);
      navigate('/tournaments');
    } catch (error) {
      setErrorMessage(error.message || 'Failed to sign in');
    } finally {
      setIsLoading(false);
    }
  };
  
  const handleSocialLogin = async (provider) => {
    setErrorMessage('');
    try {
      switch (provider) {
        case 'google':
          await signInWithGoogle();
          break;
        case 'facebook':
          await signInWithFacebook();
          break;
        case 'apple':
          await signInWithApple();
          break;
        case 'twitter':
          await signInWithTwitter();
          break;
      }
    } catch (error) {
      setErrorMessage(error.message || `Failed to sign in with ${provider}`);
    }
  };
  
  return (
    <div className="auth-container">
      <form onSubmit={handleEmailSubmit} className="space-y-4">
        {errorMessage && <Alert type="error">{errorMessage}</Alert>}
        
        <div>
          <Input
            type="email"
            label={t('auth.emailAddress')}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </div>
        
        <div>
          <Input
            type="password"
            label={t('auth.password')}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </div>
        
        <Button
          type="submit"
          variant="primary"
          fullWidth
          isLoading={isLoading}
        >
          {t('auth.signIn')}
        </Button>
      </form>
      
      <Divider text={t('auth.orContinueWith')} className="my-6" />
      
      <div className="social-login-buttons grid grid-cols-2 gap-3">
        <Button 
          variant="outline"
          onClick={() => handleSocialLogin('google')}
          leftIcon={<GoogleIcon />}
        >
          Google
        </Button>
        
        <Button 
          variant="outline"
          onClick={() => handleSocialLogin('facebook')}
          leftIcon={<FacebookIcon />}
        >
          Facebook
        </Button>
        
        <Button 
          variant="outline"
          onClick={() => handleSocialLogin('apple')}
          leftIcon={<AppleIcon />}
        >
          Apple
        </Button>
        
        <Button 
          variant="outline"
          onClick={() => handleSocialLogin('twitter')}
          leftIcon={<TwitterIcon />}
        >
          Twitter
        </Button>
      </div>
    </div>
  );
};
```

## Offline Implementation

The offline functionality will be implemented using service workers and IndexedDB:

```typescript
// src/features/offline/services/offlineStorage.ts
import Dexie from 'dexie';
import { v4 as uuidv4 } from 'uuid';

export class GolfAppDatabase extends Dexie {
  tournaments: Dexie.Table<any, string>;
  scores: Dexie.Table<any, string>;
  pendingRequests: Dexie.Table<any, string>;
  userRoles: Dexie.Table<any, string>; // Store user roles for offline access
  
  constructor() {
    super('GolfAppDatabase');
    
    this.version(1).stores({
      tournaments: 'id, status, start_date',
      scores: 'id, tournament_id, player_id, hole_number, sync_status',
      pendingRequests: 'id, url, method, data, headers, timestamp',
      userRoles: 'id, user_id, tenant_id, role, timestamp' // For offline role-based permissions
    });
    
    this.tournaments = this.table('tournaments');
    this.scores = this.table('scores');
    this.pendingRequests = this.table('pendingRequests');
    this.userRoles = this.table('userRoles');
  }
  
  async saveScore(score) {
    // Add client-side ID and sync status
    const scoreWithMeta = {
      ...score,
      id: score.id || uuidv4(),
      sync_status: 'pending' // pending, synced, error
    };
    
    await this.scores.put(scoreWithMeta);
    
    // Queue API request for when online
    await this.queueRequest({
      url: '/scores',
      method: 'POST',
      data: {
        ...score,
        recorded_at: new Date().toISOString()
      }
    });
    
    return scoreWithMeta;
  }
  
  async queueRequest(request) {
    await this.pendingRequests.add({
      id: uuidv4(),
      ...request,
      timestamp: new Date().toISOString()
    });
  }
  
  async processPendingRequests(apiClient) {
    if (!navigator.onLine) {
      return;
    }
    
    const pendingRequests = await this.pendingRequests.toArray();
    
    for (const request of pendingRequests) {
      try {
        await apiClient({
          url: request.url,
          method: request.method,
          data: request.data,
          headers: request.headers
        });
        
        // If successful, remove from queue
        await this.pendingRequests.delete(request.id);
        
        // Update sync status if it's a score
        if (request.url === '/scores' && request.method === 'POST') {
          const score = await this.scores
            .where({ id: request.data.id })
            .first();
            
          if (score) {
            await this.scores.update(score.id, { sync_status: 'synced' });
          }
        }
      } catch (error) {
        console.error('Failed to process pending request', error);
        // Could implement retry logic here
      }
    }
  }
  
  // Role-based permission checking for offline mode
  async checkUserPermission(userId: string, tenantId: string, requiredRoles: string[]) {
    if (navigator.onLine) {
      // When online, always check against the server
      return false;
    }
    
    // When offline, check local role cache
    const userRole = await this.userRoles
      .where({ user_id: userId, tenant_id: tenantId })
      .first();
      
    if (!userRole) {
      return false;
    }
    
    return requiredRoles.includes(userRole.role);
  }
  
  // Cache user roles for offline access
  async cacheUserRole(userId: string, tenantId: string, role: string) {
    await this.userRoles.put({
      id: `${userId}-${tenantId}`,
      user_id: userId,
      tenant_id: tenantId,
      role: role,
      timestamp: new Date().toISOString()
    });
  }
}

export const offlineDB = new GolfAppDatabase();

// src/hooks/useOfflineSync.ts
import { useState, useEffect } from 'react';
import { offlineDB } from '../features/offline/services/offlineStorage';
import { apiClient } from '../lib/api';

export const useOfflineSync = () => {
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [isSyncing, setIsSyncing] = useState(false);
  const [pendingCount, setPendingCount] = useState(0);
  
  // Update online status
  useEffect(() => {
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);
    
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);
  
  // Count pending requests
  useEffect(() => {
    const countPending = async () => {
      const count = await offlineDB.pendingRequests.count();
      setPendingCount(count);
    };
    
    countPending();
    
    // Set up an interval to recount
    const interval = setInterval(countPending, 5000);
    
    return () => clearInterval(interval);
  }, []);
  
  // Sync when back online
  useEffect(() => {
    if (isOnline && pendingCount > 0 && !isSyncing) {
      const syncData = async () => {
        setIsSyncing(true);
        
        try {
          await offlineDB.processPendingRequests(apiClient);
        } finally {
          setIsSyncing(false);
          
          // Recount pending
          const count = await offlineDB.pendingRequests.count();
          setPendingCount(count);
        }
      };
      
      syncData();
    }
  }, [isOnline, pendingCount, isSyncing]);
  
  return {
    isOnline,
    isSyncing,
    pendingCount
  };
};
```

## Internationalization Approach

The application will use react-i18next for internationalization:

```typescript
// src/lib/i18n.ts
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import Backend from 'i18next-http-backend';

i18n
  .use(Backend)
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    fallbackLng: 'en',
    supportedLngs: ['en', 'nl'],
    debug: process.env.NODE_ENV === 'development',
    interpolation: {
      escapeValue: false
    },
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage']
    },
    backend: {
      loadPath: '/locales/{{lng}}/{{ns}}.json'
    }
  });

export default i18n;

// Example translation usage
// src/features/tournaments/components/TournamentCard.tsx
import { useTranslation } from 'react-i18next';
import { format } from 'date-fns';
import { enUS, nl } from 'date-fns/locale';

export const TournamentCard = ({ tournament }) => {
  const { t, i18n } = useTranslation();
  
  // Get the correct date-fns locale based on the current language
  const dateLocale = i18n.language === 'nl' ? nl : enUS;
  
  const formatDate = (date) => {
    return format(new Date(date), 'PPP', { locale: dateLocale });
  };
  
  return (
    <div className="tournament-card">
      <h3>{tournament.name}</h3>
      <p>{t('tournaments.dateRange', {
        startDate: formatDate(tournament.start_date),
        endDate: formatDate(tournament.end_date)
      })}</p>
      <p>{t(`tournaments.status.${tournament.status}`)}</p>
      
      <div className="tournament-actions">
        <button>{t('common.view')}</button>
        {tournament.status === 'registration' && (
          <button>{t('tournaments.register')}</button>
        )}
      </div>
    </div>
  );
};
```

## Key Feature Implementation

### Tournament Creation Flow

The tournament creation will be implemented as a multi-step form:

```typescript
// src/features/tournaments/components/TournamentForm.tsx
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useForm, FormProvider } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { tournamentSchema } from '../validation/tournamentSchema';
import { tournamentService } from '../../../services/api/tournamentService';
import { Steps, Step } from '../../../components/common/Steps';
import { TournamentBasicInfo } from './steps/TournamentBasicInfo';
import { TournamentCourseSelection } from './steps/TournamentCourseSelection';
import { TournamentFormatSettings } from './steps/TournamentFormatSettings';
import { TournamentRegistrationSettings } from './steps/TournamentRegistrationSettings';
import { TournamentReview } from './steps/TournamentReview';

export const TournamentForm = () => {
  const { t } = useTranslation();
  const [currentStep, setCurrentStep] = useState(0);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const navigate = useNavigate();
  
  const methods = useForm({
    resolver: zodResolver(tournamentSchema),
    defaultValues: {
      name: '',
      description: '',
      start_date: '',
      end_date: '',
      course_id: '',
      format: 'stroke',
      is_public: true,
      status: 'draft',
      // Additional fields...
    }
  });
  
  const steps = [
    {
      title: t('tournaments.form.steps.basicInfo'),
      component: <TournamentBasicInfo />
    },
    {
      title: t('tournaments.form.steps.courseSelection'),
      component: <TournamentCourseSelection />
    },
    {
      title: t('tournaments.form.steps.formatSettings'),
      component: <TournamentFormatSettings />
    },
    {
      title: t('tournaments.form.steps.registrationSettings'),
      component: <TournamentRegistrationSettings />
    },
    {
      title: t('tournaments.form.steps.review'),
      component: <TournamentReview />
    }
  ];
  
  const nextStep = () => {
    setCurrentStep(prev => Math.min(prev + 1, steps.length - 1));
  };
  
  const prevStep = () => {
    setCurrentStep(prev => Math.max(prev - 1, 0));
  };
  
  const onSubmit = async (data) => {
    setIsSubmitting(true);
    
    try {
      const result = await tournamentService.createTournament(data);
      navigate(`/tournaments/${result.data.id}`);
    } catch (error) {
      console.error('Failed to create tournament', error);
      // Handle error
    } finally {
      setIsSubmitting(false);
    }
  };
  
  return (
    <FormProvider {...methods}>
      <form onSubmit={methods.handleSubmit(onSubmit)}>
        <Steps currentStep={currentStep}>
          {steps.map((step, index) => (
            <Step
              key={index}
              title={step.title}
              isActive={index === currentStep}
              isCompleted={index < currentStep}
            />
          ))}
        </Steps>
        
        <div className="mt-6">
          {steps[currentStep].component}
        </div>
        
        <div className="mt-8 flex justify-between">
          {currentStep > 0 && (
            <button
              type="button"
              onClick={prevStep}
              className="btn btn-secondary"
            >
              {t('common.back')}
            </button>
          )}
          
          {currentStep < steps.length - 1 ? (
            <button
              type="button"
              onClick={nextStep}
              className="btn btn-primary"
            >
              {t('common.next')}
            </button>
          ) : (
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isSubmitting}
            >
              {isSubmitting ? t('common.submitting') : t('common.createTournament')}
            </button>
          )}
        </div>
      </form>
    </FormProvider>
  );
};
```

### Score Recording Feature

The score recording feature will be implemented with offline support:

```typescript
// src/features/scoring/components/ScoreCard.tsx
import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { scoringService } from '../services/scoringService';
import { offlineDB } from '../../../features/offline/services/offlineStorage';
import { useAuth } from '../../../hooks/useAuth';
import { ScoreHole } from './ScoreHole';
import { ScoreNav } from './ScoreNav';
import { SyncStatus } from './SyncStatus';

export const ScoreCard = () => {
  const { id: tournamentId } = useParams();
  const { t } = useTranslation();
  const { user } = useAuth();
  const [tournament, setTournament] = useState(null);
  const [currentPlayer, setCurrentPlayer] = useState(null);
  const [scores, setScores] = useState({});
  const [currentHole, setCurrentHole] = useState(1);
  const [syncStatus, setSyncStatus] = useState('synced'); // synced, pending, error
  
  // Load tournament and score data
  useEffect(() => {
    const loadData = async () => {
      // Try to load from IndexedDB first for offline support
      const offlineTournament = await offlineDB.tournaments
        .where({ id: tournamentId })
        .first();
        
      if (offlineTournament) {
        setTournament(offlineTournament);
        
        // Set current player (typically the logged-in user)
        const player = offlineTournament.tournament_players.find(
          p => p.player_id === user.id
        );
        
        if (player) {
          setCurrentPlayer(player);
        }
      } else {
        // Fetch from API if not in offline storage
        try {
          const data = await scoringService.getTournamentForScoring(tournamentId);
          setTournament(data);
          
          // Save to offline storage
          await offlineDB.tournaments.put(data);
          
          // Set current player
          const player = data.tournament_players.find(
            p => p.player_id === user.id
          );
          
          if (player) {
            setCurrentPlayer(player);
          }
        } catch (error) {
          console.error('Failed to load tournament data', error);
          // Handle error
        }
      }
      
      // Load existing scores
      try {
        const existingScores = await scoringService.getPlayerScores(
          tournamentId,
          user.id
        );
        
        // Transform scores array to object for easier access
        const scoresMap = {};
        existingScores.forEach(score => {
          scoresMap[score.hole_number] = score;
        });
        
        setScores(scoresMap);
      } catch (error) {
        console.error('Failed to load scores', error);
        // Handle error - try offline storage
        const offlineScores = await offlineDB.scores
          .where({ tournament_id: tournamentId, player_id: user.id })
          .toArray();
          
        if (offlineScores.length > 0) {
          const scoresMap = {};
          offlineScores.forEach(score => {
            scoresMap[score.hole_number] = score;
          });
          
          setScores(scoresMap);
          
          // Check if there are pending scores to sync
          const hasPendingScores = offlineScores.some(
            score => score.sync_status === 'pending'
          );
          
          if (hasPendingScores) {
            setSyncStatus('pending');
          }
        }
      }
    };
    
    loadData();
  }, [tournamentId, user.id]);
  
  // Save score for a hole
  const saveScore = async (holeNumber, scoreData) => {
    const updatedScores = { ...scores };
    
    // Create or update score
    const scoreEntry = {
      tournament_id: tournamentId,
      player_id: user.id,
      hole_number: holeNumber,
      ...scoreData,
      recorded_by: user.id,
      recorded_at: new Date().toISOString(),
      status: 'submitted'
    };
    
    // If there's an existing score, preserve the ID
    if (updatedScores[holeNumber]) {
      scoreEntry.id = updatedScores[holeNumber].id;
    }
    
    // Update local state
    updatedScores[holeNumber] = scoreEntry;
    setScores(updatedScores);
    
    try {
      // Try to save to API
      const result = await scoringService.saveScore(scoreEntry);
      
      // Update with server data
      updatedScores[holeNumber] = result;
      setScores({ ...updatedScores });
      setSyncStatus('synced');
    } catch (error) {
      console.error('Failed to save score', error);
      
      // Save to offline storage
      const offlineResult = await offlineDB.saveScore(scoreEntry);
      
      // Update with offline data
      updatedScores[holeNumber] = offlineResult;
      setScores({ ...updatedScores });
      setSyncStatus('pending');
    }
  };
  
  if (!tournament || !currentPlayer) {
    return <div>{t('common.loading')}</div>;
  }
  
  return (
    <div className="scorecard-container">
      <div className="scorecard-header">
        <h2>{tournament.name}</h2>
        <p>{t('scoring.player')}: {currentPlayer.player_name}</p>
        <SyncStatus status={syncStatus} />
      </div>
      
      <ScoreNav
        currentHole={currentHole}
        totalHoles={tournament.course.total_holes || 18}
        scores={scores}
        onHoleChange={setCurrentHole}
      />
      
      <ScoreHole
        hole={currentHole}
        course={tournament.course}
        score={scores[currentHole] || {}}
        onSaveScore={(scoreData) => saveScore(currentHole, scoreData)}
      />
      
      <div className="scorecard-navigation">
        <button
          disabled={currentHole === 1}
          onClick={() => setCurrentHole(prev => Math.max(prev - 1, 1))}
        >
          {t('scoring.previousHole')}
        </button>
        
        <button
          disabled={currentHole === (tournament.course.total_holes || 18)}
          onClick={() => setCurrentHole(prev => Math.min(prev + 1, tournament.course.total_holes || 18))}
        >
          {t('scoring.nextHole')}
        </button>
      </div>
    </div>
  );
};
```

### Leaderboard Feature

The leaderboard will use Supabase Realtime for live updates:

```typescript
// src/features/leaderboard/components/TournamentLeaderboard.tsx
import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { leaderboardService } from '../services/leaderboardService';
import { LeaderboardTable } from './LeaderboardTable';
import { ScoreFilters } from './ScoreFilters';
import { LeaderboardRefreshIndicator } from './LeaderboardRefreshIndicator';

export const TournamentLeaderboard = () => {
  const { id: tournamentId } = useParams();
  const { t } = useTranslation();
  const [tournament, setTournament] = useState(null);
  const [leaderboard, setLeaderboard] = useState([]);
  const [filters, setFilters] = useState({
    flight: 'all',
    round: 1,
    view: 'gross' // gross, net, stableford
  });
  const [isLoading, setIsLoading] = useState(true);
  const [lastUpdated, setLastUpdated] = useState(new Date());
  
  // Load tournament and leaderboard data
  useEffect(() => {
    const loadData = async () => {
      setIsLoading(true);
      
      try {
        // Get tournament details
        const tournamentData = await leaderboardService.getTournament(tournamentId);
        setTournament(tournamentData);
        
        // Get initial leaderboard
        const leaderboardData = await leaderboardService.getLeaderboard(
          tournamentId,
          filters
        );
        
        setLeaderboard(leaderboardData);
        setLastUpdated(new Date());
      } catch (error) {
        console.error('Failed to load tournament data', error);
        // Handle error
      } finally {
        setIsLoading(false);
      }
    };
    
    loadData();
  }, [tournamentId, filters]);
  
  // Subscribe to real-time updates
  useEffect(() => {
    if (!tournamentId) return;
    
    const unsubscribe = leaderboardService.subscribeToLeaderboardUpdates(
      tournamentId,
      (payload) => {
        // Refresh leaderboard data when scores are updated
        leaderboardService
          .getLeaderboard(tournamentId, filters)
          .then(data => {
            setLeaderboard(data);
            setLastUpdated(new Date());
          })
          .catch(error => {
            console.error('Failed to refresh leaderboard', error);
          });
      }
    );
    
    return () => {
      unsubscribe();
    };
  }, [tournamentId, filters]);
  
  const handleFilterChange = (newFilters) => {
    setFilters(prev => ({ ...prev, ...newFilters }));
  };
  
  const handleRefresh = async () => {
    setIsLoading(true);
    
    try {
      const data = await leaderboardService.getLeaderboard(
        tournamentId,
        filters
      );
      
      setLeaderboard(data);
      setLastUpdated(new Date());
    } catch (error) {
      console.error('Failed to refresh leaderboard', error);
    } finally {
      setIsLoading(false);
    }
  };
  
  if (!tournament && isLoading) {
    return <div>{t('common.loading')}</div>;
  }
  
  if (!tournament) {
    return <div>{t('leaderboard.tournamentNotFound')}</div>;
  }
  
  return (
    <div className="leaderboard-container">
      <div className="leaderboard-header">
        <h1>{tournament.name}</h1>
        <p>{tournament.course?.name}</p>
        
        <LeaderboardRefreshIndicator
          lastUpdated={lastUpdated}
          onRefresh={handleRefresh}
          isLoading={isLoading}
        />
      </div>
      
      <ScoreFilters
        tournament={tournament}
        filters={filters}
        onChange={handleFilterChange}
      />
      
      <LeaderboardTable
        leaderboard={leaderboard}
        scoreFormat={filters.view}
        isLoading={isLoading}
      />
    </div>
  );
};
```

## CI/CD Pipeline

The project will use GitHub Actions for CI/CD:

```yaml
# .github/workflows/ci.yml
name: CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Set up Node.js
      uses: actions/setup-node@v3
      with:
        node-version: '18'
        cache: 'npm'
        
    - name: Install dependencies
      run: npm ci
      
    - name: Run linter
      run: npm run lint
      
    - name: Run unit tests
      run: npm test -- --coverage
      
    - name: Build
      run: npm run build
      
  deploy:
    needs: test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Set up Node.js
      uses: actions/setup-node@v3
      with:
        node-version: '18'
        cache: 'npm'
        
    - name: Install dependencies
      run: npm ci
      
    - name: Build
      run: npm run build
      
    - name: Deploy to Azure Static Web Apps
      uses: Azure/static-web-apps-deploy@v1
      with:
        azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
        repo_token: ${{ secrets.GITHUB_TOKEN }}
        action: "upload"
        app_location: "build"
        api_location: "api"
        skip_app_build: true
```

## Testing Strategy

The testing approach will include:

1. **Unit Tests**: Component and utility function testing with Jest and React Testing Library
2. **Integration Tests**: API interaction and feature flow testing
3. **E2E Tests**: Critical user flows with Cypress
4. **Accessibility Testing**: Using jest-axe for component-level a11y
5. **Performance Testing**: Lighthouse CI integration
6. **Offline Testing**: Service worker and offline functionality testing

Example unit test for a component:

```typescript
// src/components/common/Button.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { Button } from './Button';

describe('Button Component', () => {
  test('renders correctly with default props', () => {
    render(<Button>Click Me</Button>);
    
    const button = screen.getByRole('button', { name: /click me/i });
    expect(button).toBeInTheDocument();
    expect(button).not.toBeDisabled();
    expect(button).toHaveClass('btn');
    expect(button).not.toHaveClass('btn-loading');
  });
  
  test('applies primary variant class', () => {
    render(<Button variant="primary">Primary Button</Button>);
    
    const button = screen.getByRole('button', { name: /primary button/i });
    expect(button).toHaveClass('btn-primary');
  });
  
  test('shows loading state', () => {
    render(<Button isLoading>Loading Button</Button>);
    
    const button = screen.getByRole('button', { name: /loading button/i });
    expect(button).toHaveClass('btn-loading');
    expect(button).toBeDisabled();
    
    // Should show loading spinner
    const spinner = screen.getByTestId('loading-spinner');
    expect(spinner).toBeInTheDocument();
  });
  
  test('handles click events', () => {
    const handleClick = jest.fn();
    
    render(<Button onClick={handleClick}>Clickable Button</Button>);
    
    const button = screen.getByRole('button', { name: /clickable button/i });
    fireEvent.click(button);
    
    expect(handleClick).toHaveBeenCalledTimes(1);
  });
  
  test('is disabled when disabled prop is true', () => {
    render(<Button disabled>Disabled Button</Button>);
    
    const button = screen.getByRole('button', { name: /disabled button/i });
    expect(button).toBeDisabled();
  });
});
```

## Performance Optimization

Key performance optimizations include:

1. **Code Splitting**: Using dynamic imports for route-based code splitting
2. **Resource Caching**: Aggressive caching strategy for static assets and API responses
3. **Lazy Loading**: Deferring the loading of non-critical resources
4. **Virtualized Lists**: For rendering large collections like leaderboards
5. **Image Optimization**: Using modern formats (WebP) and responsive images
6. **Font Loading Strategy**: Using font-display: swap and preloading critical fonts
7. **Memoization**: Using React.memo, useMemo, and useCallback for expensive operations
8. **Performance Monitoring**: Integration with performance monitoring tools

Implementation example for virtualized lists:

```typescript
// src/features/leaderboard/components/LeaderboardTable.tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FixedSizeList } from 'react-window';
import AutoSizer from 'react-virtualized-auto-sizer';

// Row renderer for virtualized list
const LeaderboardRow = ({ data, index, style }) => {
  const player = data[index];
  
  return (
    <div
      className={`leaderboard-row ${index % 2 === 0 ? 'even' : 'odd'}`}
      style={style}
    >
      <div className="position">{player.position}</div>
      <div className="player-name">{player.player_name}</div>
      <div className="score">{player.total_score}</div>
      {/* Additional columns */}
    </div>
  );
};

export const LeaderboardTable = ({ leaderboard, scoreFormat, isLoading }) => {
  const { t } = useTranslation();
  const [sortBy, setSortBy] = useState('position');
  const [sortDirection, setSortDirection] = useState('asc');
  
  // Sort players based on current sort settings
  const sortedLeaderboard = [...leaderboard].sort((a, b) => {
    const direction = sortDirection === 'asc' ? 1 : -1;
    
    if (sortBy === 'position') {
      return (a.position - b.position) * direction;
    }
    
    if (sortBy === 'name') {
      return a.player_name.localeCompare(b.player_name) * direction;
    }
    
    // Additional sort options
    
    return 0;
  });
  
  if (isLoading) {
    return <div>{t('common.loading')}</div>;
  }
  
  if (leaderboard.length === 0) {
    return <div>{t('leaderboard.noScores')}</div>;
  }
  
  return (
    <div className="leaderboard-table">
      <div className="leaderboard-header-row">
        <div
          className="position sortable"
          onClick={() => {
            if (sortBy === 'position') {
              setSortDirection(prev => prev === 'asc' ? 'desc' : 'asc');
            } else {
              setSortBy('position');
              setSortDirection('asc');
            }
          }}
        >
          {t('leaderboard.position')}
          {sortBy === 'position' && (
            <span className={`sort-indicator ${sortDirection}`} />
          )}
        </div>
        
        <div
          className="player-name sortable"
          onClick={() => {
            if (sortBy === 'name') {
              setSortDirection(prev => prev === 'asc' ? 'desc' : 'asc');
            } else {
              setSortBy('name');
              setSortDirection('asc');
            }
          }}
        >
          {t('leaderboard.playerName')}
          {sortBy === 'name' && (
            <span className={`sort-indicator ${sortDirection}`} />
          )}
        </div>
        
        <div className="score">{t('leaderboard.totalScore')}</div>
        {/* Additional headers */}
      </div>
      
      <div className="leaderboard-body" style={{ height: '60vh' }}>
        <AutoSizer>
          {({ height, width }) => (
            <FixedSizeList
              height={height}
              width={width}
              itemCount={sortedLeaderboard.length}
              itemSize={50} // Row height
              itemData={sortedLeaderboard}
            >
              {LeaderboardRow}
            </FixedSizeList>
          )}
        </AutoSizer>
      </div>
    </div>
  );
};
```
