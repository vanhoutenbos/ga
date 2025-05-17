# Technical Architecture

Based on the comprehensive analysis of the project's evolution and the insights gained from examining multiple generations of the codebase, the following architecture is recommended:

## Recommended Architecture

### Frontend Architecture
- **Progressive Web Application (PWA)**
  - React-based SPA with TypeScript for type safety
  - Full PWA implementation with offline capabilities
  - Service workers for background sync and caching
  - Web App Manifest for installability on devices
  - Responsive design optimized for mobile, tablet, and desktop  - Modular component architecture for reusable UI elements
  - Device API integration for camera (QR code scanning) and geolocation
  - Internationalization (i18n) with react-i18next for multi-language support

- **Single Application with Role-Based Modules**
  - Unified codebase for scoring, viewing, and administration
  - Dynamic module loading for performance optimization
  - Role-based navigation and permission system
  - Shared core services across all modules
  - Context-aware UI that adapts to user role and device

### Backend Architecture
- **API Gateway**
  - Node.js Express-based API gateway
  - JWT validation middleware for Supabase tokens
  - Request routing and load balancing
  - Caching layer for performance
  - Rate limiting for service protection

- **Supabase Integration**
  - Authentication via Supabase Auth
  - Database access via Supabase PostgreSQL
  - Storage management via Supabase Storage
  - Real-time subscriptions via Supabase Realtime

- **Serverless Functions (for custom logic)**
  - Azure Functions for complex operations
  - Tournament management operations
  - Custom scoring calculations
  - Integration with third-party services

- **Cross-Cutting Concerns**
  - Centralized logging and monitoring
  - Distributed tracing
  - Error handling and resilience patterns
  - API documentation with Swagger/OpenAPI

### Database Architecture
- **Primary Database: Supabase PostgreSQL**
  - Fully-managed PostgreSQL database via Supabase
  - Row Level Security for authorization enforcement
  - Advanced PostgreSQL features (JSON, array types, full-text search)
  - Entity-relationship model based on existing domain models
  - Migrations through Supabase migrations API
  - Optimized indexes for common queries

- **Real-time Data Features via Supabase**
  - Real-time subscriptions for leaderboard updates
  - Broadcast channels for tournament events
  - Presence tracking for active recorders/players
  - Conflict resolution for offline-first approach

- **Supporting Data Stores**
  - Supabase Storage for user uploads and exports
  - Local storage (IndexedDB) for offline data
  - Service worker cache for offline application functionality

## Technical Decisions and Implementation Tips

### PWA vs. Native App Decision

After careful consideration of the project requirements, a Progressive Web App (PWA) approach has been chosen over React Native for the following reasons:

1. **Development Efficiency**
   - Single codebase for web and mobile experiences
   - Faster iteration cycles and deployment process
   - Reduced complexity in CI/CD pipeline
   - Broader developer availability for web technologies

2. **Installation and Distribution**
   - No app store approval process required
   - Instant updates without user action
   - Lower friction for new user adoption
   - Still installable on home screen on modern devices

3. **Offline Capabilities**
   - Service workers provide robust offline functionality
   - IndexedDB and Cache API for local data storage
   - Background sync for score submission
   - Works well for the intermittent connectivity of golf courses

4. **Cost Considerations**
   - Lower development and maintenance costs
   - Single hosting infrastructure
   - Simplified testing matrix
   - More efficient use of developer resources

5. **Platform Capabilities**
   - Modern browser APIs provide access to:
     - Camera (for QR code scanning)
     - Geolocation (for course positioning)
     - Push notifications (for tournament updates)
     - Storage (for offline data)
   - Progressive enhancement for older browsers

### Unified App Architecture

Instead of separate applications for scoring and leaderboard viewing, a modular single-app approach is recommended:

1. **Code Organization**
   - Feature-based module structure:
     ```
     src/
     ├── core/                # Shared core functionality
     │   ├── auth/            # Authentication services
     │   ├── api/             # API client services
     │   ├── state/           # Global state management
     │   └── ui/              # Shared UI components
     ├── modules/
     │   ├── scoring/         # Score recording module
     │   ├── leaderboard/     # Tournament viewing module
     │   └── admin/           # Tournament management module
     └── features/            # Cross-cutting features
         ├── offline/         # Offline functionality
         ├── sync/            # Data synchronization
         └── notifications/   # Push notification handling
     ```

2. **Code Splitting Implementation**
   - Route-based code splitting for optimized loading:
     ```javascript
     // Dynamic imports for code splitting
     const ScoringModule = React.lazy(() => import('./modules/scoring'));
     const LeaderboardModule = React.lazy(() => import('./modules/leaderboard'));
     const AdminModule = React.lazy(() => import('./modules/admin'));
     
     // Routing with suspense for loading states
     <Suspense fallback={<LoadingIndicator />}>
       <Routes>
         <Route path="/scoring/*" element={<ScoringModule />} />
         <Route path="/leaderboard/*" element={<LeaderboardModule />} />
         <Route path="/admin/*" element={<AdminModule />} />
       </Routes>
     </Suspense>
     ```

3. **Role-Based Experience**
   - Conditional navigation based on user role:
     ```javascript
     function AppNavigation({ user }) {
       // Determine available routes based on user role
       const routes = [];
       
       // All users can view tournaments
       routes.push({ path: '/tournaments', label: 'Tournaments' });
       
       // Players can access scoring
       if (user.roles.includes('player')) {
         routes.push({ path: '/scoring', label: 'Score Entry' });
       }
       
       // Organizers can access admin features
       if (user.roles.includes('organizer')) {
         routes.push({ path: '/admin', label: 'Management' });
       }
       
       return (
         <NavigationBar routes={routes} />
       );
     }
     ```

4. **Shared Services Architecture**
   - Core services available to all modules:
     ```javascript
     // Core API service
     export class TournamentApiService {
       async getTournaments() { /* ... */ }
       async getLeaderboard(tournamentId) { /* ... */ }
       async submitScore(tournamentId, scoreData) { /* ... */ }
     }
     
     // Module-specific service that uses core service
     export class ScoringService {
       constructor(apiService) {
         this.apiService = apiService;
       }
       
       async saveScoreCard(scoreCard) {
         // Add offline handling, validation, etc.
         return this.apiService.submitScore(scoreCard.tournamentId, scoreCard);
       }
     }
     ```

5. **Context-Specific UI Modes**
   - UI adapts based on context:
     ```javascript
     function TournamentView({ tournament, userContext }) {
       // Determine the appropriate view based on context
       const isOrganizer = userContext.roles.includes('organizer');
       const isPlayer = userContext.isPlayerInTournament(tournament.id);
       const isMobile = useMediaQuery('(max-width: 768px)');
       
       return (
         <div className="tournament-view">
           {/* Common tournament header */}
           <TournamentHeader tournament={tournament} />
           
           {/* Context-specific content */}
           {isOrganizer && <OrganizerControls tournament={tournament} />}
           {isPlayer && <PlayerDashboard tournament={tournament} />}
           
           {/* Responsive leaderboard */}
           <Leaderboard 
             tournament={tournament}
             compact={isMobile}
             highlightPlayer={userContext.playerId}
           />
         </div>
       );
     }
     ```

### Offline-First Implementation

1. **Service Worker Strategy**
   - Registration and lifecycle management:
     ```javascript
     // Register service worker
     if ('serviceWorker' in navigator) {
       window.addEventListener('load', () => {
         navigator.serviceWorker.register('/service-worker.js')
           .then(registration => {
             console.log('SW registered:', registration);
           })
           .catch(error => {
             console.log('SW registration failed:', error);
           });
       });
     }
     ```

2. **Caching Strategy**
   - Implementation for different resource types:
     ```javascript
     // In service-worker.js
     // Cache static assets with Cache-First strategy
     workbox.routing.registerRoute(
       ({request}) => request.destination === 'style' || 
                      request.destination === 'script' ||
                      request.destination === 'font',
       new workbox.strategies.CacheFirst({
         cacheName: 'static-resources',
         plugins: [
           new workbox.expiration.ExpirationPlugin({
             maxEntries: 60,
             maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
           }),
         ],
       })
     );
     
     // Cache API requests with Network-First strategy
     workbox.routing.registerRoute(
       ({url}) => url.pathname.startsWith('/api/'),
       new workbox.strategies.NetworkFirst({
         cacheName: 'api-responses',
         plugins: [
           new workbox.expiration.ExpirationPlugin({
             maxEntries: 50,
             maxAgeSeconds: 24 * 60 * 60, // 1 day
           }),
         ],
       })
     );
     ```

3. **Data Synchronization**
   - Background sync for offline data:
     ```javascript
     // Queue score submissions when offline
     async function submitScore(scoreData) {
       try {
         // Try to submit score directly
         await fetch('/api/scores', {
           method: 'POST',
           body: JSON.stringify(scoreData),
           headers: { 'Content-Type': 'application/json' }
         });
       } catch (error) {
         // If offline, store in IndexedDB and register for sync
         await saveToOfflineStore('pendingScores', scoreData);
         
         if ('serviceWorker' in navigator && 'SyncManager' in window) {
           const sw = await navigator.serviceWorker.ready;
           await sw.sync.register('sync-scores');
         }
       }
     }
     
     // In service worker - handle background sync
     self.addEventListener('sync', (event) => {
       if (event.tag === 'sync-scores') {
         event.waitUntil(syncPendingScores());
       }
     });
     
     async function syncPendingScores() {
       const pendingScores = await getPendingScoresFromDB();
       
       return Promise.all(pendingScores.map(async (score) => {
         try {
           await fetch('/api/scores', {
             method: 'POST',
             body: JSON.stringify(score),
             headers: { 'Content-Type': 'application/json' }
           });
           
           // If successful, remove from pending
           await removeFromOfflineStore('pendingScores', score.id);
         } catch (error) {
           // Leave in store to try again later
           console.error('Failed to sync score:', error);
         }
       }));
     }
     ```

### Real-time Leaderboard Implementation with Supabase

1. **Supabase Realtime Subscriptions**
   - Efficient real-time updates via Supabase client:
     ```javascript
     // Subscribe to leaderboard updates
     const leaderboardSubscription = supabase
       .from('tournament_leaderboard')
       .on('*', (payload) => {
         // Update UI with new leaderboard data
         updateLeaderboardData(payload.new);
       })
       .subscribe();
     ```

2. **Optimized Real-time Queries**
   - Database views for efficient leaderboard calculations:
     ```sql
     -- Create a view for the leaderboard to optimize queries
     CREATE VIEW tournament_leaderboard AS
     SELECT 
       p.id as player_id,
       p.display_name,
       t.id as tournament_id,
       t.name as tournament_name,
       SUM(s.strokes) as total_strokes,
       SUM(CASE WHEN s.strokes < h.par + 2 THEN 1 ELSE 0 END) as pars_or_better,
       ROW_NUMBER() OVER (PARTITION BY t.id ORDER BY SUM(s.strokes)) as position
     FROM scores s
     JOIN profiles p ON s.player_id = p.id
     JOIN tournaments t ON s.tournament_id = t.id
     JOIN holes h ON s.hole_id = h.id
     GROUP BY p.id, t.id
     ORDER BY t.id, total_strokes;
     
     -- Enable realtime for this view
     ALTER PUBLICATION supabase_realtime ADD TABLE tournament_leaderboard;
     ```

3. **Fallback Strategy**
   - Polling when WebSockets unavailable:
     ```javascript
     // Fallback polling function
     function setupLeaderboardPolling(tournamentId) {
       if (!supportsRealtime) {
         const pollInterval = setInterval(async () => {
           try {
             const { data } = await supabase
               .from('tournament_leaderboard')
               .select('*')
               .eq('tournament_id', tournamentId);
               
             updateLeaderboardData(data);
           } catch (error) {
             console.error('Polling failed:', error);
           }
         }, 15000); // Poll every 15 seconds
         
         return pollInterval;
       }
       return null;
     }
     ```

4. **Offline Cache Updates**
   - Synchronize with latest leaderboard on reconnection:
     ```javascript
     // Update cache on network reconnection
     window.addEventListener('online', async () => {
       const { data } = await supabase
         .from('tournament_leaderboard')
         .select('*')
         .eq('tournament_id', currentTournamentId);
       
       if (data) {
         // Update local state
         updateLeaderboardData(data);
         
         // Update IndexedDB cache for offline access
         await localforage.setItem(`leaderboard_${currentTournamentId}`, data);
       }
     });
     ```

### App Store Publication Strategy for PWA

For distribution through app stores while maintaining the PWA approach:

1. **PWA Wrappers**
   - Use lightweight native wrappers around the PWA:
     - [PWABuilder](https://www.pwabuilder.com/) for automated app generation
     - [Capacitor](https://capacitorjs.com/) for native API access
     - [Trusted Web Activities (TWA)](https://developer.chrome.com/docs/android/trusted-web-activity/) for Android

2. **App Store Metadata**
   - Description emphasizing offline functionality
   - Screenshots highlighting mobile-optimized UI
   - Clarify that updates happen automatically
   - Emphasize benefits over traditional apps

3. **Platform-Specific Considerations**
   - Apple App Store requires additional native functionality
   - Google Play Store is more PWA-friendly with TWAs
   - Consider using platform-specific enhancements where beneficial

### Performance Optimization Strategies

1. **Initial Load Performance**
   - Critical CSS inlining
   - Route-based code splitting
   - Asset preloading for critical paths
   - Server-side rendering for initial content

2. **Runtime Performance**
   - Virtualized lists for leaderboards
   - Memoization for expensive calculations
   - Deferred loading of non-critical features
   - Web workers for intensive operations

3. **Monitoring and Analysis**
   - Implement Web Vitals tracking
   - Set up real user monitoring
   - Create performance budgets   - Regular Lighthouse audits

### Internationalization Architecture

1. **Translation Management**
   - JSON-based translation files organized by feature:
     ```
     /public/locales/
       ├── en/                    # English (default)
       │   ├── common.json        # Shared across app
       │   ├── tournament.json    # Tournament-specific
       │   └── scoring.json       # Scoring-specific
       └── nl/                    # Dutch
           ├── common.json
           ├── tournament.json
           └── scoring.json
     ```
   - Key-based translations with nested structure for organization
   - Support for variable interpolation and pluralization

2. **Frontend Implementation**
   - React-i18next integration:
     ```jsx
     import { useTranslation } from 'react-i18next';
     
     function TournamentHeader({ tournament }) {
       const { t } = useTranslation('tournament');
       
       return (
         <header>
           <h1>{t('details.title', { name: tournament.name })}</h1>
           <p>{t('details.dateRange', { 
             startDate: formatDate(tournament.startDate, i18n.language),
             endDate: formatDate(tournament.endDate, i18n.language)
           })}</p>
         </header>
       );
     }
     ```
   - Language switcher component for user control
   - Automatic detection based on browser settings
   - Persistence of language preference

3. **Locale-Specific Formatting**
   - Date and time formatting via Intl.DateTimeFormat
   - Number formatting with Intl.NumberFormat
   - Currency display adapted to locale
   - Measurement units conversion (yards/meters) based on locale preference

4. **Right-to-Left (RTL) Support**
   - CSS logical properties for future RTL language support
   - Dynamic text direction based on language
   - Component testing in both LTR and RTL contexts

### Integration Architecture
- **External Integration Points**
  - RESTful APIs for third-party integrations
  - Webhook system for event notifications
  - OAuth2 for authenticated integrations
  - GraphQL API for flexible data access
