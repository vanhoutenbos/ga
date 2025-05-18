# Supabase Error Handling Strategy

This document outlines our comprehensive error handling strategy for Supabase operations in the Golf Tournament Organizer application, with special emphasis on offline scenarios. The strategy aims to provide a seamless user experience while ensuring data integrity and proper error reporting.

## Table of Contents

1. [Overview](#overview)
2. [Offline Detection and Notification](#offline-detection-and-notification)
3. [Error Classification](#error-classification)
4. [User Feedback Mechanisms](#user-feedback-mechanisms)
5. [Error Reporting and Monitoring](#error-reporting-and-monitoring)
6. [Implementation Examples](#implementation-examples)
7. [Testing Strategy](#testing-strategy)
8. [Integration with Conflict Resolution](#integration-with-conflict-resolution)

## Overview

Our error handling strategy follows these core principles:

1. **User-First Approach**: Always communicate clearly with users about errors and their impact
2. **Graceful Degradation**: Maintain functionality when possible, even if some features are limited
3. **Data Preservation**: Never lose user data, especially during offline scenarios
4. **Transparency**: Be honest about limitations while providing reassurance
5. **Developer Insights**: Collect meaningful error data for troubleshooting and improvements

## Offline Detection and Notification

### Connection Monitoring

```typescript
// src/services/network/ConnectionMonitor.ts
export class ConnectionMonitor {
  private online: boolean = navigator.onLine;
  private listeners: Set<(online: boolean) => void> = new Set();
  private reconnectionAttempt: boolean = false;
  private toastService: ToastService;
  private lastOfflineNotificationTime: number = 0;
  
  constructor(toastService: ToastService) {
    this.toastService = toastService;
    
    // Listen for browser online/offline events
    window.addEventListener('online', this.handleConnectionChange.bind(this));
    window.addEventListener('offline', this.handleConnectionChange.bind(this));
    
    // Additional heartbeat check for unreliable connection detection
    this.startHeartbeat();
  }
  
  /**
   * Handle connection status changes
   */
  private handleConnectionChange(): void {
    const wasOnline = this.online;
    this.online = navigator.onLine;
    
    // Connection status changed
    if (wasOnline !== this.online) {
      // Notify listeners
      this.notifyListeners();
      
      // Show appropriate notification to user
      if (this.online) {
        // Coming back online
        this.toastService.success(
          'You\'re back online!', 
          'Your data will now sync automatically.'
        );
        
        // Reset reconnection attempt flag
        this.reconnectionAttempt = false;
      } else {
        // Going offline
        const now = Date.now();
        // Only show notification once every 30 seconds at most
        if (now - this.lastOfflineNotificationTime > 30000) {
          this.lastOfflineNotificationTime = now;
          this.toastService.warning(
            'You\'re offline', 
            'Don\'t worry, you can continue recording scores. Data will sync when you\'re back online.',
            { duration: 5000 } // Show for 5 seconds
          );
        }
      }
    }
  }
  
  /**
   * Add a listener for connection changes
   */
  public addListener(listener: (online: boolean) => void): () => void {
    this.listeners.add(listener);
    // Immediately call with current status
    listener(this.online);
    
    // Return function to remove listener
    return () => this.listeners.delete(listener);
  }
  
  /**
   * Notify all listeners of connection status change
   */
  private notifyListeners(): void {
    this.listeners.forEach(listener => {
      try {
        listener(this.online);
      } catch (error) {
        console.error('Error in connection listener:', error);
      }
    });
  }
  
  /**
   * Perform a heartbeat check to verify actual connectivity
   * beyond just the browser's navigator.onLine property
   */
  private startHeartbeat(): void {
    const checkConnectivity = async () => {
      // Skip check if already known to be offline
      if (!navigator.onLine) return;
      
      try {
        // Try to fetch a small endpoint
        const result = await fetch('/api/heartbeat', { 
          method: 'HEAD',
          // Short timeout to detect poor connections quickly
          signal: AbortSignal.timeout(3000)
        });
        
        // Update online status if needed
        if (!result.ok && this.online) {
          this.online = false;
          this.notifyListeners();
          
          // Only show notification if this is first detection
          if (!this.reconnectionAttempt) {
            this.reconnectionAttempt = true;
            this.toastService.warning(
              'Connection issues detected', 
              'You can continue recording scores. We\'ll keep trying to reconnect.'
            );
          }
        } else if (result.ok && !this.online) {
          // We detected we're back online before the browser did
          this.online = true;
          this.notifyListeners();
          this.toastService.success(
            'Connection restored!', 
            'Your data will now sync automatically.'
          );
          this.reconnectionAttempt = false;
        }
      } catch (error) {
        // Fetch failed, likely offline
        if (this.online) {
          this.online = false;
          this.notifyListeners();
          
          // Only show notification if this is first detection
          if (!this.reconnectionAttempt) {
            this.reconnectionAttempt = true;
            this.toastService.warning(
              'Connection issues detected', 
              'You can continue recording scores. We\'ll keep trying to reconnect.'
            );
          }
        }
      }
    };
    
    // Check initially and then every 30 seconds
    checkConnectivity();
    setInterval(checkConnectivity, 30000);
  }
  
  /**
   * Check if currently online
   */
  public isOnline(): boolean {
    return this.online;
  }
}
```

### Post-Transaction Offline Detection

```typescript
// src/services/data/ScoreService.ts
export class ScoreService {
  private supabase: SupabaseClient;
  private connectionMonitor: ConnectionMonitor;
  private toastService: ToastService;
  private modalService: ModalService;
  private offlineEducationShown: boolean = false;
  
  constructor(
    supabase: SupabaseClient, 
    connectionMonitor: ConnectionMonitor,
    toastService: ToastService,
    modalService: ModalService
  ) {
    this.supabase = supabase;
    this.connectionMonitor = connectionMonitor;
    this.toastService = toastService;
    this.modalService = modalService;
  }
  
  /**
   * Save a score with offline handling
   */
  public async saveScore(score: Score): Promise<void> {
    // Check connection before attempting save
    const wasOnline = this.connectionMonitor.isOnline();
    
    try {
      // Always save to local IndexedDB first for offline capability
      await this.saveToLocalDatabase(score);
      
      // If online, try to sync immediately
      if (wasOnline) {
        try {
          await this.syncScoreToServer(score);
        } catch (error) {
          // Failed to sync despite being "online"
          console.error('Failed to sync score despite being online:', error);
          
          // We might have just lost connection - detect post-transaction offline state
          if (!this.connectionMonitor.isOnline()) {
            // Connection was lost during transaction
            this.handlePostTransactionOffline();
          } else {
            // It's a different error during online operation
            this.handleOnlineError(error, 'saving score');
          }
        }
      } else {
        // We're already offline, show a toast only if we haven't shown the education modal
        if (!this.offlineEducationShown) {
          this.toastService.info(
            'Score saved locally', 
            'It will sync automatically when you\'re back online.',
            { duration: 3000 }
          );
        }
      }
    } catch (error) {
      // Critical error - even local save failed
      console.error('Critical error saving score locally:', error);
      this.toastService.error(
        'Unable to save score', 
        'Please try again or contact support if the problem persists.'
      );
      
      // Report to error monitoring
      this.reportError('critical_local_save_failure', error, {
        scoreId: score.id,
        playerId: score.playerId,
        holeNumber: score.holeNumber
      });
    }
  }
  
  /**
   * Handle scenario where user goes offline immediately after a transaction
   */
  private handlePostTransactionOffline(): void {
    // First time seeing this? Show educational modal once per session
    if (!this.offlineEducationShown) {
      this.offlineEducationShown = true;
      
      this.modalService.show({
        title: 'You\'re offline',
        content: `
          <p>Your device just lost internet connection, but don't worry!</p>
          <p>You can continue recording scores while offline. All data is safely stored on your device.</p>
          <p>When your connection is restored, all scores will automatically sync to the server.</p>
        `,
        primaryButton: {
          text: 'Got it',
          action: () => this.modalService.close()
        }
      });
    } else {
      // Subsequently, just show a toast
      this.toastService.info(
        'You\'re offline', 
        'The score is saved locally and will sync when you\'re back online.',
        { duration: 3000 }
      );
    }
  }
  
  /**
   * Handle online errors with supabase operations
   */
  private handleOnlineError(error: any, operation: string): void {
    // Extract meaningful message if possible
    let message = 'An unexpected error occurred';
    
    if (error?.message) {
      message = error.message;
    } else if (typeof error === 'string') {
      message = error;
    }
    
    // Show user-friendly message
    this.toastService.error(
      `Error ${operation}`, 
      'Data is saved locally. We\'ll try again automatically.',
      { duration: 5000 }
    );
    
    // Report error for monitoring
    this.reportError(`online_operation_failure_${operation}`, error);
  }
  
  /**
   * Report error to monitoring system
   */
  private reportError(code: string, error: any, metadata?: Record<string, any>): void {
    // Implementation depends on your error reporting system
    // Example using a hypothetical ErrorReporter
    ErrorReporter.captureException(error, {
      tags: {
        component: 'ScoreService',
        errorCode: code
      },
      extra: {
        ...metadata,
        timestamp: new Date().toISOString(),
        online: this.connectionMonitor.isOnline()
      }
    });
  }
}
```

## Error Classification

We classify errors into the following categories to provide appropriate user feedback and handling strategies:

### 1. Connectivity Errors

These errors are related to the network connection status:

| Error Type | Description | User Impact | Handling Strategy |
|------------|-------------|------------|-------------------|
| Offline Status | Device is completely offline | Cannot sync data | Inform user data is saved locally, will sync later |
| Intermittent Connection | Connection drops during operations | Operation failure | Retry with exponential backoff |
| Low Bandwidth | Connection exists but is very slow | Timeout errors | Reduce payload size, extend timeouts |
| Reconnection | Device comes back online | None, positive | Automatic sync, notify user |

### 2. Supabase-Specific Errors

These errors are specific to Supabase operations:

| Error Type | Description | User Impact | Handling Strategy |
|------------|-------------|------------|-------------------|
| Authentication Errors | JWT expired or invalid | Cannot perform operations | Attempt token refresh, prompt re-login if needed |
| Permission Errors | User lacks permission | Operation rejected | Clear error explaining permission issue |
| Rate Limiting | Too many requests | Operation failure | Implement backoff, queue operations |
| Database Constraints | Violates DB rules | Data rejection | User-friendly explanation of the constraint |
| Realtime Subscription Limit | Free tier limits exceeded | Missing updates | Fall back to polling, notify admin of limit |

### 3. Data Processing Errors

These errors occur during data processing:

| Error Type | Description | User Impact | Handling Strategy |
|------------|-------------|------------|-------------------|
| Validation Errors | Client-side validation fails | Cannot submit invalid data | Immediate feedback on form fields |
| Serialization Errors | Data cannot be serialized | Operation failure | Log error, attempt with sanitized data |
| Conflict Errors | Data conflicts with server | Operation rejection | Utilize conflict resolution strategy |
| Storage Errors | IndexedDB errors | Cannot save offline | Attempt localStorage fallback, notify if critical |

## User Feedback Mechanisms

We use several mechanisms to provide appropriate feedback to users:

### 1. Toast Notifications

For non-disruptive, transient information:

```typescript
// src/services/ui/ToastService.ts
export class ToastService {
  // Toast levels
  public info(title: string, message: string, options?: ToastOptions): void {
    this.show({ title, message, type: 'info', ...options });
  }
  
  public success(title: string, message: string, options?: ToastOptions): void {
    this.show({ title, message, type: 'success', ...options });
  }
  
  public warning(title: string, message: string, options?: ToastOptions): void {
    this.show({ title, message, type: 'warning', ...options });
  }
  
  public error(title: string, message: string, options?: ToastOptions): void {
    this.show({ title, message, type: 'error', ...options });
  }
  
  // Implementation depends on your UI framework
  private show(toast: Toast): void {
    // Example using a toast library
    showToast({
      title: toast.title,
      message: toast.message,
      type: toast.type,
      duration: toast.duration || 3000,
      position: toast.position || 'bottom',
      // Additional options...
    });
  }
}
```

### 2. Status Bar

For persistent status information:

```typescript
// src/components/StatusBar.tsx
export const StatusBar: React.FC = () => {
  const [online, setOnline] = useState(navigator.onLine);
  const [syncStatus, setSyncStatus] = useState<'synced' | 'pending' | 'syncing'>('synced');
  const [pendingChanges, setPendingChanges] = useState(0);
  const connectionMonitor = useConnectionMonitor();
  const syncService = useSyncService();
  
  useEffect(() => {
    // Listen for connection status
    const removeListener = connectionMonitor.addListener(setOnline);
    
    // Listen for sync status changes
    const unsubscribeSyncStatus = syncService.addSyncStatusListener((status, count) => {
      setSyncStatus(status);
      setPendingChanges(count);
    });
    
    return () => {
      removeListener();
      unsubscribeSyncStatus();
    };
  }, []);
  
  return (
    <div className={`status-bar ${online ? 'online' : 'offline'}`}>
      <div className="connection-status">
        {online ? (
          <span className="online-indicator">
            <OnlineIcon /> Online
          </span>
        ) : (
          <span className="offline-indicator">
            <OfflineIcon /> Offline
          </span>
        )}
      </div>
      
      <div className="sync-status">
        {syncStatus === 'synced' && online && (
          <span className="synced">
            <SyncedIcon /> All changes saved
          </span>
        )}
        {syncStatus === 'pending' && (
          <span className="pending">
            <PendingIcon /> {pendingChanges} changes pending
          </span>
        )}
        {syncStatus === 'syncing' && (
          <span className="syncing">
            <SyncingIcon spin /> Syncing changes...
          </span>
        )}
      </div>
    </div>
  );
};
```

### 3. Modal Dialogs

For critical errors or important educational information:

```typescript
// src/services/ui/ModalService.ts
export class ModalService {
  private modalStack: Modal[] = [];
  private listeners: Set<(modals: Modal[]) => void> = new Set();
  
  /**
   * Show a modal dialog
   */
  public show(modal: ModalOptions): void {
    const newModal: Modal = {
      id: generateUniqueId(),
      ...modal
    };
    
    this.modalStack.push(newModal);
    this.notifyListeners();
  }
  
  /**
   * Show an error modal with reporting option
   */
  public showErrorModal(title: string, message: string, error: any): void {
    this.show({
      title,
      content: `
        <p>${message}</p>
        <p>Error details: ${error.message || 'Unknown error'}</p>
        <p>If this problem persists, please contact support.</p>
      `,
      primaryButton: {
        text: 'OK',
        action: () => this.close()
      },
      secondaryButton: {
        text: 'Report Issue',
        action: () => {
          this.reportIssue(error);
          this.close();
        }
      }
    });
  }
  
  /**
   * Close the top-most modal
   */
  public close(): void {
    if (this.modalStack.length > 0) {
      this.modalStack.pop();
      this.notifyListeners();
    }
  }
  
  /**
   * Report an issue to the developer
   */
  private reportIssue(error: any): void {
    // Implementation depends on your error reporting system
    ErrorReporter.captureException(error, {
      tags: {
        component: 'UserReported',
        source: 'ErrorModal'
      },
      extra: {
        userTriggered: true,
        timestamp: new Date().toISOString()
      }
    });
  }
  
  // Additional methods for managing modals...
}
```

### 4. Inline Form Validation

For immediate feedback on data entry:

```typescript
// src/components/ScoreEntryForm.tsx
export const ScoreEntryForm: React.FC<ScoreEntryFormProps> = ({ player, hole, onSubmit }) => {
  const [strokes, setStrokes] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  
  const validateScore = (value: string): string | null => {
    if (!value.trim()) {
      return 'Score is required';
    }
    
    const numValue = parseInt(value, 10);
    if (isNaN(numValue)) {
      return 'Score must be a number';
    }
    
    // Check against hole par and reasonable limits
    if (numValue < 1) {
      return 'Score must be at least 1';
    }
    
    if (numValue > 20) {
      return 'Score seems unusually high. Please verify.';
    }
    
    return null; // No error
  };
  
  const handleChange = (e: React.ChangeEvent<HTMLInputElement>): void => {
    const value = e.target.value;
    setStrokes(value);
    
    // Validate on change
    const error = validateScore(value);
    setErrors(prev => ({
      ...prev,
      strokes: error || ''
    }));
  };
  
  const handleSubmit = (e: React.FormEvent): void => {
    e.preventDefault();
    
    // Final validation
    const error = validateScore(strokes);
    if (error) {
      setErrors(prev => ({
        ...prev,
        strokes: error
      }));
      return;
    }
    
    // Submit valid score
    onSubmit({
      playerId: player.id,
      holeNumber: hole.number,
      strokes: parseInt(strokes, 10),
      // Additional fields...
    });
    
    // Reset form
    setStrokes('');
    setErrors({});
  };
  
  return (
    <form onSubmit={handleSubmit}>
      <div className="form-group">
        <label htmlFor="strokes">Strokes</label>
        <input
          id="strokes"
          type="number"
          value={strokes}
          onChange={handleChange}
          className={errors.strokes ? 'input-error' : ''}
        />
        {errors.strokes && (
          <div className="error-message">{errors.strokes}</div>
        )}
      </div>
      
      <button type="submit" disabled={!!errors.strokes}>
        Save Score
      </button>
    </form>
  );
};
```

## Error Reporting and Monitoring

To track and improve error handling, we implement robust error reporting:

```typescript
// src/services/monitoring/ErrorReporter.ts
export class ErrorReporter {
  private static instance: ErrorReporter;
  private initialized: boolean = false;
  private metadata: Record<string, any> = {};
  
  private constructor() {
    // Private constructor for singleton
  }
  
  /**
   * Get the singleton instance
   */
  public static getInstance(): ErrorReporter {
    if (!ErrorReporter.instance) {
      ErrorReporter.instance = new ErrorReporter();
    }
    return ErrorReporter.instance;
  }
  
  /**
   * Initialize the error reporter
   */
  public initialize(config: ErrorReporterConfig): void {
    if (this.initialized) return;
    
    // Setup your preferred error reporting service
    // Example with a hypothetical service:
    window.ErrorTracker.initialize({
      apiKey: config.apiKey,
      environment: config.environment,
      release: config.release,
      sampleRate: config.sampleRate || 1.0,
      ignoreErrors: [
        // Ignore some known non-critical errors
        'ResizeObserver loop limit exceeded',
        'Network request failed',
        /Failed to fetch/i
      ]
    });
    
    // Set global metadata
    this.metadata = {
      appVersion: config.version,
      buildNumber: config.buildNumber,
      // Additional metadata...
    };
    
    // Set up global error handler
    window.onerror = (message, source, lineno, colno, error) => {
      this.captureException(error || new Error(String(message)), {
        extra: { source, lineno, colno }
      });
      
      // Don't prevent default handling
      return false;
    };
    
    // Set up unhandled promise rejection handler
    window.onunhandledrejection = (event) => {
      this.captureException(event.reason || new Error('Unhandled Promise rejection'), {
        tags: { unhandledRejection: true }
      });
    };
    
    this.initialized = true;
  }
  
  /**
   * Capture an exception
   */
  public captureException(error: any, options?: ErrorReportOptions): void {
    if (!this.initialized) {
      console.error('Error reporter not initialized:', error);
      return;
    }
    
    try {
      // Prepare the error and metadata
      const errorObject = error instanceof Error ? error : new Error(String(error));
      
      // Combine global and specific metadata
      const combinedExtra = {
        ...this.metadata,
        ...options?.extra,
        timestamp: new Date().toISOString(),
        online: navigator.onLine
      };
      
      // Report to your error tracking service
      window.ErrorTracker.captureException(errorObject, {
        tags: options?.tags,
        extra: combinedExtra
      });
      
      // Log to console in development
      if (process.env.NODE_ENV === 'development') {
        console.error('Error captured:', errorObject, combinedExtra);
      }
    } catch (reportingError) {
      // Last resort if reporting itself fails
      console.error('Failed to report error:', reportingError);
      console.error('Original error:', error);
    }
  }
  
  /**
   * Record a user action for context
   */
  public addBreadcrumb(category: string, message: string, data?: Record<string, any>): void {
    if (!this.initialized) return;
    
    try {
      window.ErrorTracker.addBreadcrumb({
        category,
        message,
        data,
        level: 'info'
      });
    } catch (error) {
      console.error('Failed to add breadcrumb:', error);
    }
  }
  
  /**
   * Set user context for error reports
   */
  public setUser(user: { id: string; email?: string; role?: string }): void {
    if (!this.initialized) return;
    
    try {
      window.ErrorTracker.setUser(user);
    } catch (error) {
      console.error('Failed to set user context:', error);
    }
  }
}
```

## Implementation Examples

### Example 1: Saving Scores with Full Error Handling

```typescript
// src/features/scoring/useScoreSubmission.ts
export function useScoreSubmission() {
  const scoreService = useScoreService();
  const toastService = useToastService();
  const errorReporter = useErrorReporter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  
  const submitScore = async (score: Score): Promise<boolean> => {
    setIsSubmitting(true);
    
    try {
      // Add breadcrumb for error context
      errorReporter.addBreadcrumb('score_submission', 'User submitted score', {
        playerId: score.playerId,
        holeNumber: score.holeNumber,
        tournamentId: score.tournamentId
      });
      
      // Attempt to save the score
      await scoreService.saveScore(score);
      
      // Success - only show toast if we're online
      // (offline case is handled within the service)
      if (navigator.onLine) {
        toastService.success(
          'Score saved', 
          'The leaderboard has been updated.',
          { duration: 2000 }
        );
      }
      
      return true;
    } catch (error) {
      // This should rarely happen as the service handles most errors
      // This is a fallback for truly unexpected errors
      errorReporter.captureException(error, {
        tags: {
          component: 'ScoreSubmission',
          operation: 'submitScore'
        },
        extra: { score }
      });
      
      toastService.error(
        'Unable to save score', 
        'Please try again or contact support if the problem persists.'
      );
      
      return false;
    } finally {
      setIsSubmitting(false);
    }
  };
  
  return {
    submitScore,
    isSubmitting
  };
}
```

### Example 2: Progressive Enhancement for Offline UI

```typescript
// src/components/OfflineAwareButton.tsx
export const OfflineAwareButton: React.FC<{
  onClick: () => void;
  requiresConnection?: boolean;
  offlineTooltip?: string;
  children: React.ReactNode;
  className?: string;
}> = ({
  onClick,
  requiresConnection = false,
  offlineTooltip = 'This action requires an internet connection',
  children,
  className = ''
}) => {
  const [online] = useConnectionStatus();
  const [showTooltip, setShowTooltip] = useState(false);
  
  const handleClick = () => {
    if (requiresConnection && !online) {
      // Show tooltip explaining why button is disabled
      setShowTooltip(true);
      setTimeout(() => setShowTooltip(false), 3000);
      return;
    }
    
    onClick();
  };
  
  return (
    <div className="relative inline-block">
      <button
        onClick={handleClick}
        disabled={requiresConnection && !online}
        className={`${className} ${requiresConnection && !online ? 'disabled' : ''}`}
      >
        {children}
        {!online && requiresConnection && (
          <span className="offline-indicator">
            <OfflineIcon size="small" />
          </span>
        )}
      </button>
      
      {showTooltip && (
        <div className="tooltip">{offlineTooltip}</div>
      )}
    </div>
  );
};
```

### Example 3: Sync Status Indicator

```typescript
// src/components/SyncStatusIndicator.tsx
export const SyncStatusIndicator: React.FC = () => {
  const [status, setStatus] = useState<'synced' | 'pending' | 'syncing' | 'error'>('synced');
  const [pendingCount, setPendingCount] = useState(0);
  const [errorCount, setErrorCount] = useState(0);
  const syncService = useSyncService();
  const modalService = useModalService();
  
  useEffect(() => {
    return syncService.addStatusListener((newStatus, counts) => {
      setStatus(newStatus);
      setPendingCount(counts.pending);
      setErrorCount(counts.errors);
    });
  }, []);
  
  const handleClick = () => {
    if (status === 'error') {
      // Show error details modal
      modalService.show({
        title: 'Sync Issues',
        content: `
          <p>There are ${errorCount} items that could not be synced.</p>
          <p>You can:</p>
          <ul>
            <li>Retry syncing now</li>
            <li>View detailed error information</li>
            <li>Continue working - we'll keep trying automatically</li>
          </ul>
        `,
        primaryButton: {
          text: 'Retry Now',
          action: () => {
            syncService.retryAll();
            modalService.close();
          }
        },
        secondaryButton: {
          text: 'View Details',
          action: () => {
            modalService.close();
            // Navigate to sync details page
            navigate('/sync-details');
          }
        },
        tertiaryButton: {
          text: 'Continue Working',
          action: () => modalService.close()
        }
      });
    } else if (status === 'pending') {
      // Trigger immediate sync
      syncService.syncNow();
    }
  };
  
  const getStatusDisplay = () => {
    switch (status) {
      case 'synced':
        return (
          <div className="sync-status synced">
            <CheckIcon /> All changes saved
          </div>
        );
      case 'pending':
        return (
          <div className="sync-status pending" onClick={handleClick}>
            <ClockIcon /> {pendingCount} change{pendingCount !== 1 ? 's' : ''} pending
          </div>
        );
      case 'syncing':
        return (
          <div className="sync-status syncing">
            <SpinnerIcon spin /> Syncing...
          </div>
        );
      case 'error':
        return (
          <div className="sync-status error" onClick={handleClick}>
            <AlertIcon /> {errorCount} sync error{errorCount !== 1 ? 's' : ''}
          </div>
        );
      default:
        return null;
    }
  };
  
  return (
    <div className="sync-status-container">
      {getStatusDisplay()}
    </div>
  );
};
```

## Testing Strategy

To ensure our error handling works correctly, we implement specific tests:

### 1. Mock Network Conditions

```typescript
// src/tests/utils/networkMock.ts
export class NetworkMock {
  private originalOnline = navigator.onLine;
  private originalAddEventListener = window.addEventListener;
  private originalRemoveEventListener = window.removeEventListener;
  private listeners: Map<string, Function[]> = new Map();
  
  /**
   * Set up network condition mocking
   */
  public setup(): void {
    // Override navigator.onLine
    Object.defineProperty(navigator, 'onLine', {
      configurable: true,
      get: () => this.originalOnline,
      set: (value) => { this.originalOnline = value; }
    });
    
    // Mock event listeners
    window.addEventListener = (event, listener) => {
      if (event === 'online' || event === 'offline') {
        if (!this.listeners.has(event)) {
          this.listeners.set(event, []);
        }
        this.listeners.get(event)?.push(listener as Function);
      } else {
        this.originalAddEventListener.call(window, event, listener);
      }
    };
    
    window.removeEventListener = (event, listener) => {
      if (event === 'online' || event === 'offline') {
        const listeners = this.listeners.get(event) || [];
        const index = listeners.indexOf(listener as Function);
        if (index > -1) {
          listeners.splice(index, 1);
        }
      } else {
        this.originalRemoveEventListener.call(window, event, listener);
      }
    };
  }
  
  /**
   * Simulate going offline
   */
  public goOffline(): void {
    navigator.onLine = false;
    this.dispatchEvent('offline');
  }
  
  /**
   * Simulate going online
   */
  public goOnline(): void {
    navigator.onLine = true;
    this.dispatchEvent('online');
  }
  
  /**
   * Dispatch a mocked event
   */
  private dispatchEvent(eventName: string): void {
    const listeners = this.listeners.get(eventName) || [];
    listeners.forEach(listener => {
      try {
        listener(new Event(eventName));
      } catch (error) {
        console.error(`Error in ${eventName} listener:`, error);
      }
    });
  }
  
  /**
   * Clean up and restore original behavior
   */
  public cleanup(): void {
    Object.defineProperty(navigator, 'onLine', {
      configurable: true,
      get: () => this.originalOnline,
      set: (value) => { this.originalOnline = value; }
    });
    
    window.addEventListener = this.originalAddEventListener;
    window.removeEventListener = this.originalRemoveEventListener;
    this.listeners.clear();
  }
}
```

### 2. Testing Offline Behavior

```typescript
// src/tests/features/scoring/offlineScoring.test.ts
describe('Offline Scoring', () => {
  let networkMock: NetworkMock;
  let indexedDBMock: IndexedDBMock;
  
  beforeEach(() => {
    networkMock = new NetworkMock();
    networkMock.setup();
    
    indexedDBMock = new IndexedDBMock();
    indexedDBMock.setup();
  });
  
  afterEach(() => {
    networkMock.cleanup();
    indexedDBMock.cleanup();
  });
  
  test('should save score locally when offline', async () => {
    // Arrange
    networkMock.goOffline();
    
    const scoreService = new ScoreService(
      mockSupabaseClient,
      mockConnectionMonitor,
      mockToastService,
      mockModalService
    );
    
    const testScore = {
      id: 'test-score-1',
      playerId: 'player-1',
      tournamentId: 'tournament-1',
      holeNumber: 1,
      strokes: 4,
      updated_at: new Date().toISOString()
    };
    
    // Act
    await scoreService.saveScore(testScore);
    
    // Assert
    const savedScore = await indexedDBMock.get('scores', 'test-score-1');
    expect(savedScore).toBeDefined();
    expect(savedScore.strokes).toBe(4);
    
    // Should not have called the Supabase client
    expect(mockSupabaseClient.from).not.toHaveBeenCalled();
    
    // Should have shown appropriate toast
    expect(mockToastService.info).toHaveBeenCalledWith(
      'Score saved locally',
      expect.any(String),
      expect.any(Object)
    );
  });
  
  test('should handle post-transaction offline scenario', async () => {
    // Arrange
    networkMock.goOnline(); // Start online
    
    // Mock Supabase to fail and simultaneously go offline
    const mockSupabase = {
      from: jest.fn().mockReturnValue({
        upsert: jest.fn().mockImplementation(() => {
          // Go offline during the operation
          networkMock.goOffline();
          // Return a failed promise
          return Promise.reject(new Error('Network error'));
        })
      })
    };
    
    const scoreService = new ScoreService(
      mockSupabase as any,
      new ConnectionMonitor(mockToastService),
      mockToastService,
      mockModalService
    );
    
    const testScore = {
      id: 'test-score-2',
      playerId: 'player-1',
      tournamentId: 'tournament-1',
      holeNumber: 2,
      strokes: 5,
      updated_at: new Date().toISOString()
    };
    
    // Act
    await scoreService.saveScore(testScore);
    
    // Assert
    // Should have saved locally
    const savedScore = await indexedDBMock.get('scores', 'test-score-2');
    expect(savedScore).toBeDefined();
    
    // Should have shown the offline education modal
    expect(mockModalService.show).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'You\'re offline'
      })
    );
  });
  
  test('should sync when coming back online', async () => {
    // Arrange
    networkMock.goOffline(); // Start offline
    
    const scoreService = new ScoreService(
      mockSupabaseClient,
      mockConnectionMonitor,
      mockToastService,
      mockModalService
    );
    
    // Save a score while offline
    const testScore = {
      id: 'test-score-3',
      playerId: 'player-1',
      tournamentId: 'tournament-1',
      holeNumber: 3,
      strokes: 3,
      updated_at: new Date().toISOString()
    };
    
    await scoreService.saveScore(testScore);
    
    // Reset mocks for the next phase
    jest.clearAllMocks();
    
    // Mock the sync method
    const syncSpy = jest.spyOn(scoreService, 'syncPendingScores');
    
    // Act - go online
    networkMock.goOnline();
    
    // Wait for sync to be triggered
    await new Promise(resolve => setTimeout(resolve, 100));
    
    // Assert
    expect(syncSpy).toHaveBeenCalled();
    expect(mockToastService.success).toHaveBeenCalledWith(
      'You\'re back online!',
      expect.any(String)
    );
  });
});
```

## Integration with Conflict Resolution

Our error handling strategy integrates with the existing conflict resolution approach:

```typescript
// src/services/sync/SyncService.ts
export class SyncService {
  private connectionMonitor: ConnectionMonitor;
  private toastService: ToastService;
  private modalService: ModalService;
  private supabase: SupabaseClient;
  private statusListeners: Set<StatusListener> = new Set();
  private syncInProgress: boolean = false;
  private pendingCount: number = 0;
  private errorCount: number = 0;
  
  constructor(
    connectionMonitor: ConnectionMonitor,
    toastService: ToastService,
    modalService: ModalService,
    supabase: SupabaseClient
  ) {
    this.connectionMonitor = connectionMonitor;
    this.toastService = toastService;
    this.modalService = modalService;
    this.supabase = supabase;
    
    // Listen for connection changes
    this.connectionMonitor.addListener(this.handleConnectionChange.bind(this));
    
    // Check pending count periodically
    this.startPendingCountCheck();
  }
  
  /**
   * Handle connection status changes
   */
  private handleConnectionChange(online: boolean): void {
    if (online) {
      // Coming back online - trigger sync
      this.syncNow();
    }
  }
  
  /**
   * Start periodic check for pending changes
   */
  private startPendingCountCheck(): void {
    const checkPending = async () => {
      try {
        const pending = await this.countPendingChanges();
        const errors = await this.countErroredChanges();
        
        if (this.pendingCount !== pending || this.errorCount !== errors) {
          this.pendingCount = pending;
          this.errorCount = errors;
          this.notifyStatusListeners();
        }
      } catch (error) {
        console.error('Error checking pending count:', error);
      }
    };
    
    // Check initially and then every 30 seconds
    checkPending();
    setInterval(checkPending, 30000);
  }
  
  /**
   * Sync all pending changes now
   */
  public async syncNow(): Promise<void> {
    if (this.syncInProgress) {
      return; // Already syncing
    }
    
    if (!this.connectionMonitor.isOnline()) {
      this.toastService.warning(
        'Cannot sync', 
        'You\'re currently offline. Changes will sync when you\'re back online.'
      );
      return;
    }
    
    this.syncInProgress = true;
    this.updateStatus('syncing');
    
    try {
      await this.syncPendingChanges();
      
      // Check for any remaining pending or errors
      this.pendingCount = await this.countPendingChanges();
      this.errorCount = await this.countErroredChanges();
      
      if (this.errorCount > 0) {
        this.updateStatus('error');
        // Only show toast if there are multiple errors
        if (this.errorCount > 1) {
          this.toastService.error(
            'Sync issues', 
            `${this.errorCount} items could not be synced. Click for details.`
          );
        }
      } else if (this.pendingCount > 0) {
        this.updateStatus('pending');
      } else {
        this.updateStatus('synced');
      }
    } catch (error) {
      console.error('Error during sync:', error);
      this.errorCount = await this.countErroredChanges();
      this.updateStatus('error');
      
      this.toastService.error(
        'Sync failed', 
        'There was a problem syncing your changes. We\'ll try again automatically.'
      );
    } finally {
      this.syncInProgress = false;
    }
  }
  
  /**
   * Sync pending changes with conflict resolution
   */
  private async syncPendingChanges(): Promise<void> {
    // Get all pending changes
    const pendingChanges = await this.getPendingChanges();
    
    // Group by entity type for batch processing
    const changesByType = this.groupChangesByType(pendingChanges);
    
    // Process each type in sequence
    for (const [entityType, changes] of Object.entries(changesByType)) {
      try {
        await this.processBatch(entityType, changes);
      } catch (error) {
        console.error(`Error processing ${entityType} batch:`, error);
        // Continue with other types even if one fails
      }
    }
  }
  
  /**
   * Process a batch of changes for a specific entity type
   */
  private async processBatch(entityType: string, changes: PendingChange[]): Promise<void> {
    const processor = this.getEntityProcessor(entityType);
    if (!processor) {
      console.error(`No processor found for entity type: ${entityType}`);
      return;
    }
    
    const batchSize = 10; // Process in smaller batches
    
    for (let i = 0; i < changes.length; i += batchSize) {
      const batch = changes.slice(i, i + batchSize);
      
      try {
        const results = await processor.processChanges(batch, this.supabase);
        
        // Handle results
        for (const result of results) {
          if (result.status === 'success') {
            // Remove from pending queue
            await this.markChangeAsProcessed(result.id);
          } else if (result.status === 'conflict') {
            // Handle conflict according to our resolution strategy
            await this.handleConflict(result);
          } else if (result.status === 'error') {
            // Handle error
            await this.markChangeAsErrored(result.id, result.error);
          }
        }
      } catch (error) {
        console.error(`Error processing batch of ${entityType}:`, error);
        
        // Mark all in batch as errored
        for (const change of batch) {
          await this.markChangeAsErrored(
            change.id, 
            error instanceof Error ? error.message : 'Unknown error'
          );
        }
      }
    }
  }
  
  /**
   * Handle a sync conflict
   */
  private async handleConflict(result: SyncResult): Promise<void> {
    if (result.resolution === 'auto_resolved') {
      // Conflict was automatically resolved
      await this.markChangeAsProcessed(result.id);
      
      // For significant conflicts, notify the user
      if (result.isSignificant) {
        this.toastService.info(
          'Conflict resolved', 
          'A conflict was automatically resolved. Tap for details.',
          {
            onClick: () => this.showConflictDetails(result)
          }
        );
      }
    } else if (result.resolution === 'needs_manual_resolution') {
      // Needs manual resolution
      await this.markChangeAsConflicted(result.id, result);
      
      // Show conflict resolution UI if it's important
      if (result.isSignificant) {
        this.showManualResolutionDialog(result);
      } else {
        // Just notify about the conflict
        this.toastService.warning(
          'Sync conflict', 
          'Please review and resolve the conflicting changes.',
          {
            onClick: () => this.navigateToConflictResolution()
          }
        );
      }
    }
  }
  
  /**
   * Show conflict details modal
   */
  private showConflictDetails(result: SyncResult): void {
    this.modalService.show({
      title: 'Sync Conflict Details',
      content: `
        <div class="conflict-details">
          <p>A conflict was detected and automatically resolved.</p>
          
          <div class="conflict-item">
            <h4>Your version:</h4>
            <pre>${JSON.stringify(result.localData, null, 2)}</pre>
          </div>
          
          <div class="conflict-item">
            <h4>Server version:</h4>
            <pre>${JSON.stringify(result.serverData, null, 2)}</pre>
          </div>
          
          <div class="conflict-item conflict-resolved">
            <h4>Resolution:</h4>
            <pre>${JSON.stringify(result.resolvedData, null, 2)}</pre>
          </div>
          
          <p>The conflict was resolved using our "${result.resolutionStrategy}" strategy.</p>
        </div>
      `,
      primaryButton: {
        text: 'OK',
        action: () => this.modalService.close()
      }
    });
  }
  
  /**
   * Show manual conflict resolution dialog
   */
  private showManualResolutionDialog(result: SyncResult): void {
    this.modalService.show({
      title: 'Resolve Sync Conflict',
      content: `
        <div class="conflict-resolution">
          <p>Changes you made conflict with changes from another device or user.</p>
          
          <div class="conflict-versions">
            <div class="conflict-version">
              <h4>Your version:</h4>
              <pre>${JSON.stringify(result.localData, null, 2)}</pre>
              <button class="choose-version" data-choice="local">Use this version</button>
            </div>
            
            <div class="conflict-version">
              <h4>Server version:</h4>
              <pre>${JSON.stringify(result.serverData, null, 2)}</pre>
              <button class="choose-version" data-choice="server">Use this version</button>
            </div>
          </div>
          
          <div class="conflict-merged">
            <h4>Merge both versions (recommended):</h4>
            <pre>${JSON.stringify(result.mergedData, null, 2)}</pre>
            <button class="choose-version" data-choice="merged">Use merged version</button>
          </div>
        </div>
      `,
      // Instead of direct buttons, we use data attributes in the content
      // and handle via event delegation
      customHandler: (event) => {
        const choice = event.target.getAttribute('data-choice');
        if (choice) {
          this.resolveConflictManually(result.id, choice);
          this.modalService.close();
        }
      },
      primaryButton: {
        text: 'Decide Later',
        action: () => this.modalService.close()
      }
    });
  }
  
  /**
   * Resolve a conflict manually
   */
  private async resolveConflictManually(
    changeId: string, 
    resolution: 'local' | 'server' | 'merged'
  ): Promise<void> {
    try {
      const change = await this.getConflictedChange(changeId);
      if (!change) return;
      
      let resolvedData;
      let resolutionStrategy;
      
      switch (resolution) {
        case 'local':
          resolvedData = change.localData;
          resolutionStrategy = 'user_chose_local';
          break;
        case 'server':
          resolvedData = change.serverData;
          resolutionStrategy = 'user_chose_server';
          break;
        case 'merged':
          resolvedData = change.mergedData;
          resolutionStrategy = 'user_chose_merged';
          break;
      }
      
      // Apply the chosen resolution
      await this.applyResolution(changeId, resolvedData, resolutionStrategy);
      
      this.toastService.success(
        'Conflict resolved', 
        'Your changes have been saved.'
      );
      
      // Update counts and status
      this.pendingCount = await this.countPendingChanges();
      this.errorCount = await this.countErroredChanges();
      this.updateStatus(
        this.errorCount > 0 ? 'error' : 
        this.pendingCount > 0 ? 'pending' : 'synced'
      );
    } catch (error) {
      console.error('Error resolving conflict manually:', error);
      this.toastService.error(
        'Error resolving conflict', 
        'Please try again or contact support.'
      );
    }
  }
  
  // Helper methods for local database interactions, status notifications, etc.
  // Implementation details omitted for brevity
}
```

By implementing this comprehensive error handling strategy, we ensure our Golf Tournament Organizer application provides a reliable and user-friendly experience, even in challenging connectivity scenarios. The approach prioritizes data integrity while keeping users informed and confident that their scores and tournament data are secure.
