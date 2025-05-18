# Conflict Resolution Strategy

This document outlines our approach to handling data conflicts in the Golf Tournament Organizer application, with a particular focus on scenarios where users work offline and sync their changes later.

## Overview

The application supports offline functionality, which introduces the potential for data conflicts when multiple users modify the same data while disconnected from the network. Our conflict resolution strategy is designed to be straightforward, pragmatic, and aligned with the typical usage patterns of the application.

## Core Principle: Last Edit Wins

Our primary conflict resolution mechanism follows a "last edit wins" approach:

1. **Timestamp-Based Resolution**: Every edit operation is timestamped with a "last modified" datetime.
2. **Local Device Tracking**: Changes made offline are marked with local timestamps.
3. **Server Reconciliation**: Upon reconnection, local changes are compared with server data.
4. **Resolution Rule**: The most recent edit (based on timestamp) becomes the source of truth.

This approach is chosen based on the understanding that 99% of the time, only one person will be editing a particular entity (player, score, etc.) at any given time in our tournament context.

## Implementation Details

### Database Structure

Each entity in our database includes the following metadata fields:

```sql
-- Added to all tables that may have offline edits
created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
last_modified_by UUID REFERENCES auth.users(id),
last_modified_client_id TEXT NOT NULL
```

### Frontend Implementation

```typescript
// When making edits in the application
async function saveScore(scoreData: ScoreData): Promise<void> {
  // Add metadata
  const enhancedData = {
    ...scoreData,
    updated_at: new Date().toISOString(),
    last_modified_client_id: getDeviceId() // Unique identifier for this device/instance
  };
  
  // Save locally first (for offline capability)
  await localDatabase.scores.put(enhancedData);
  
  // Try to sync immediately if online
  if (navigator.onLine) {
    try {
      await syncToServer();
    } catch (error) {
      console.error('Failed to sync immediately, will retry later', error);
      // Will be picked up by background sync
    }
  }
}
```

### Sync Process

```typescript
// During synchronization process
async function syncToServer(): Promise<void> {
  // Get all locally modified records since last sync
  const modifiedRecords = await localDatabase.scores
    .where('updated_at')
    .above(lastSyncTime)
    .toArray();
    
  for (const record of modifiedRecords) {
    try {
      // Attempt to update server
      const { data, error } = await supabase
        .from('scores')
        .upsert(record, {
          onConflict: 'id', // Primary key conflict
          ignoreDuplicates: false // Process all conflicts
        })
        .select('id, updated_at');
        
      if (error) throw error;
      
      // Check if our update was accepted as the newest
      if (data && data[0]) {
        const serverRecord = data[0];
        const serverTimestamp = new Date(serverRecord.updated_at).getTime();
        const localTimestamp = new Date(record.updated_at).getTime();
        
        if (serverTimestamp !== localTimestamp) {
          // Server has a newer version - fetch and update local
          await fetchAndUpdateLocalRecord(record.id);
        }
      }
    } catch (error) {
      console.error(`Failed to sync record ${record.id}`, error);
      // Add to retry queue
      syncRetryQueue.push(record);
    }
  }
  
  // Update last sync time
  lastSyncTime = new Date();
  await localDatabase.metadata.put({ key: 'lastSyncTime', value: lastSyncTime.toISOString() });
}
```

### Server-Side Implementation

On the server side, we implement PostgreSQL functions to handle conflict resolution:

```sql
-- Function to handle score updates with conflict resolution
CREATE OR REPLACE FUNCTION handle_score_update()
RETURNS TRIGGER AS $$
BEGIN
  -- Check if the incoming update is newer than existing data
  IF NEW.updated_at > OLD.updated_at THEN
    -- Allow the update to proceed (last edit wins)
    RETURN NEW;
  ELSE
    -- Reject the update as there's a newer version already
    RETURN OLD;
  END IF;
END;
$$ LANGUAGE plpgsql;

-- Trigger to apply this logic on updates
CREATE TRIGGER score_update_conflict_resolution
BEFORE UPDATE ON scores
FOR EACH ROW
EXECUTE FUNCTION handle_score_update();
```

## Edge Cases and Special Considerations

While our "last edit wins" strategy works for the vast majority of cases, we have implemented additional handling for specific edge cases:

### 1. Scorer Priority

In tournament contexts, the official scorer's edits take precedence:

```typescript
// Special case for tournament scorers
if (userHasRole(user, 'official_scorer') && record.tournament_id === user.assigned_tournament_id) {
  // Add special flag to indicate official score submission
  enhancedData.is_official_score = true;
}
```

On the server side:

```sql
-- In the conflict resolution trigger
IF NEW.is_official_score = true AND OLD.is_official_score = false THEN
  -- Official scorer's update always wins regardless of timestamp
  RETURN NEW;
END IF;
```

### 2. Partial Updates

When users are editing different fields of the same record, we implement field-level merging rather than record-level replacement:

```typescript
async function mergeRecords(localRecord, serverRecord) {
  // Start with server record as base
  const mergedRecord = { ...serverRecord };
  
  // For each field that was modified locally
  for (const field of getModifiedFields(localRecord)) {
    // If local change is newer than server for this specific field
    if (getFieldLastModified(localRecord, field) > getFieldLastModified(serverRecord, field)) {
      mergedRecord[field] = localRecord[field];
    }
  }
  
  return mergedRecord;
}
```

### 3. Conflict Notification

In the rare cases where significant conflicts occur, we notify users:

```typescript
function handleSignificantConflict(localRecord, serverRecord) {
  // If changes are materially different and important
  if (isSignificantConflict(localRecord, serverRecord)) {
    // Show notification to user
    showConflictResolutionNotification({
      message: "Your changes to this score conflicted with another user's edits. The most recent changes have been applied.",
      localVersion: localRecord,
      serverVersion: serverRecord,
      resolvedVersion: serverRecord, // In this case server version won
      timestamp: new Date()
    });
    
    // Log for audit purposes
    logConflictResolution(localRecord, serverRecord);
  }
}
```

### 4. Prolonged Offline Scenarios

When a user remains offline for an extended period (e.g., an entire tournament day) with significant local changes:

```typescript
async function handleLongOfflineSyncEvent(tournamentId, localChanges) {
  // If offline for more than 4 hours with substantial changes
  const offlineDuration = Date.now() - lastSyncTimestamp;
  const isLongOfflinePeriod = offlineDuration > 4 * 60 * 60 * 1000; // 4 hours
  const hasSubstantialChanges = localChanges.length > 20; // More than 20 score entries
  
  if (isLongOfflinePeriod && hasSubstantialChanges) {
    // Get server state for comparison
    const serverState = await fetchTournamentCurrentState(tournamentId);
    
    // Perform deep comparison to identify potential conflicts
    const conflicts = detectMeaningfulConflicts(localChanges, serverState);
    
    if (conflicts.length > 0) {
      // Offer guided conflict resolution to user
      await showExtendedConflictResolutionUI(conflicts, localChanges, serverState);
    } else {
      // Apply all changes with standard last-edit-wins approach
      await syncLocalChangesToServer(localChanges);
    }
    
    // Log the significant offline sync event
    await logSignificantSyncEvent(tournamentId, {
      offlineDuration,
      changesCount: localChanges.length,
      conflictsDetected: conflicts.length,
      timestamp: new Date()
    });
  } else {
    // Handle with standard sync process
    await standardSyncProcess(localChanges);
  }
}
```

### 5. Tournament Status Transitions

Special handling for conflicts involving tournament status changes (e.g., from "active" to "completed"):

```typescript
function handleTournamentStatusConflict(localTournament, serverTournament) {
  // Status transitions have special rules
  const statusTransitions = {
    "draft": ["active", "cancelled"],
    "active": ["paused", "completed", "cancelled"],
    "paused": ["active", "completed", "cancelled"],
    "completed": [], // Terminal state, no transitions allowed
    "cancelled": []  // Terminal state, no transitions allowed
  };
  
  // If server is in terminal state, it always wins
  if (["completed", "cancelled"].includes(serverTournament.status)) {
    return serverTournament;
  }
  
  // If local change tries to transition to terminal state, it wins
  if (["completed", "cancelled"].includes(localTournament.status) && 
      !["completed", "cancelled"].includes(serverTournament.status)) {
    return localTournament;
  }
  
  // Check if transition is valid based on server current state
  const allowedTransitions = statusTransitions[serverTournament.status] || [];
  if (allowedTransitions.includes(localTournament.status)) {
    // Apply local status change but keep other server updates
    return {
      ...serverTournament,
      status: localTournament.status,
      updated_at: localTournament.updated_at
    };
  }
  
  // Default to server state for invalid transitions
  return serverTournament;
}
```

### 6. Score Validation Conflicts

Special handling for scores that fail validation against current course/tournament rules:

```typescript
async function handleScoreValidationConflict(localScore, serverScore, tournamentRules) {
  // Check if local score complies with tournament rules
  const validationResult = validateScore(localScore, tournamentRules);
  
  if (!validationResult.isValid) {
    // If local score fails validation but server score is valid, keep server
    const serverValidation = validateScore(serverScore, tournamentRules);
    if (serverValidation.isValid) {
      return {
        resolvedScore: serverScore,
        resolution: "SERVER_VALID_LOCAL_INVALID",
        message: validationResult.message
      };
    }
    
    // Both are invalid, apply special handling
    return {
      resolvedScore: computeNearestValidScore(localScore, serverScore, tournamentRules),
      resolution: "BOTH_INVALID_COMPUTED_VALID",
      message: "Both scores failed validation. A valid score was computed."
    };
  }
  
  // Local score is valid, check if server is also valid
  const serverValidation = validateScore(serverScore, tournamentRules);
  if (!serverValidation.isValid) {
    // Server invalid, local valid - use local
    return {
      resolvedScore: localScore,
      resolution: "LOCAL_VALID_SERVER_INVALID",
      message: serverValidation.message
    };
  }
  
  // Both valid, use standard timestamp conflict resolution
  return {
    resolvedScore: localScore.updated_at > serverScore.updated_at ? localScore : serverScore,
    resolution: "BOTH_VALID_TIMESTAMP_RESOLUTION",
    message: null
  };
}
```

### 7. Multi-device Synchronization

Handling scenarios where the same user edits from multiple devices:

```typescript
async function handleMultiDeviceSyncForSameUser(userId, deviceId, localChanges) {
  // Get records of other devices for same user
  const otherDeviceSyncs = await db
    .from('device_sync_records')
    .select('*')
    .eq('user_id', userId)
    .neq('device_id', deviceId)
    .order('last_sync_at', { ascending: false })
    .limit(5);
  
  if (otherDeviceSyncs.length > 0) {
    const recentOtherDeviceSync = otherDeviceSyncs[0];
    
    // If another device for same user synced very recently
    const timeSinceOtherSync = Date.now() - new Date(recentOtherDeviceSync.last_sync_at).getTime();
    if (timeSinceOtherSync < 60000) { // Less than a minute ago
      // Apply enhanced merge strategy and notify user
      const mergedChanges = await performEnhancedUserMerge(
        localChanges, 
        recentOtherDeviceSync.device_id,
        userId
      );
      
      notifyUser({
        type: 'info',
        title: 'Changes synchronized across your devices',
        message: `Changes you made on another device were merged with this device's changes.`,
        details: `${otherDeviceSyncs.length} other device(s) have synchronized recently.`
      });
      
      return mergedChanges;
    }
  }
  
  // Standard processing if no recent syncs from other devices
  return localChanges;
}
```

## Future Enhancements

As the application evolves, we plan to implement the following enhancements to our conflict resolution strategy:

### 1. Conflict Resolution UI

For critical data conflicts where automated merging strategies are insufficient, we will develop a dedicated UI that allows users to manually resolve conflicts:

```typescript
// Component to allow manual conflict resolution
function ManualConflictResolutionModal({ 
  conflicts, 
  onResolve, 
  onCancel 
}) {
  const [resolutions, setResolutions] = useState({});
  
  // Allow user to choose for each conflict
  const handleResolutionChoice = (conflictId, choice) => {
    setResolutions(prev => ({
      ...prev,
      [conflictId]: choice
    }));
  };
  
  // Preview merged result based on user choices
  const previewMerged = () => {
    return conflicts.map(conflict => ({
      field: conflict.field,
      resolvedValue: resolutions[conflict.id] === 'local' 
        ? conflict.localValue 
        : conflict.serverValue
    }));
  };
  
  return (
    <Modal title="Resolve Conflicts">
      <p>Changes you made offline conflict with more recent changes from other users.</p>
      
      {conflicts.map(conflict => (
        <ConflictItem 
          key={conflict.id}
          conflict={conflict}
          selectedResolution={resolutions[conflict.id] || 'server'}
          onChoose={choice => handleResolutionChoice(conflict.id, choice)}
        />
      ))}
      
      <div className="preview">
        <h4>Preview of resolved record:</h4>
        <pre>{JSON.stringify(previewMerged(), null, 2)}</pre>
      </div>
      
      <div className="actions">
        <Button onClick={onCancel}>Cancel</Button>
        <Button 
          onClick={() => onResolve(resolutions)} 
          disabled={Object.keys(resolutions).length !== conflicts.length}
          primary
        >
          Apply Resolutions
        </Button>
      </div>
    </Modal>
  );
}
```

### 2. Enhanced Merge Strategies

We will implement more sophisticated field-level merging algorithms for complex data structures:

```typescript
// Enhanced merging for array types (e.g., list of players)
function mergeArrayFields(localArray, serverArray, idField = 'id') {
  // Create maps for quick lookups
  const localMap = new Map(localArray.map(item => [item[idField], item]));
  const serverMap = new Map(serverArray.map(item => [item[idField], item]));
  
  const result = [];
  
  // Add or update items from both arrays
  const allIds = new Set([
    ...localArray.map(item => item[idField]),
    ...serverArray.map(item => item[idField])
  ]);
  
  for (const id of allIds) {
    const localItem = localMap.get(id);
    const serverItem = serverMap.get(id);
    
    if (!localItem) {
      // Item exists only on server, add it
      result.push(serverItem);
    } else if (!serverItem) {
      // Item exists only locally, add it
      result.push(localItem);
    } else {
      // Item exists in both, merge based on timestamps
      if (localItem.updated_at > serverItem.updated_at) {
        result.push(localItem);
      } else {
        result.push(serverItem);
      }
    }
  }
  
  return result;
}

// Advanced JSON structure merging
function mergeJsonFields(localJson, serverJson, path = '') {
  if (typeof localJson !== 'object' || typeof serverJson !== 'object') {
    // Base case: primitive values, use timestamp-based approach
    const localTimestamp = getPathLastModified(path);
    const serverTimestamp = getPathLastModified(path);
    
    return localTimestamp > serverTimestamp ? localJson : serverJson;
  }
  
  // Handle arrays
  if (Array.isArray(localJson) && Array.isArray(serverJson)) {
    return mergeArrayFields(localJson, serverJson);
  }
  
  // Handle objects by recursively merging their properties
  const result = {...serverJson};
  
  for (const key of Object.keys(localJson)) {
    const newPath = path ? `${path}.${key}` : key;
    
    if (key in result) {
      // Property exists in both, merge recursively
      result[key] = mergeJsonFields(localJson[key], result[key], newPath);
    } else {
      // Property only exists locally, add it
      result[key] = localJson[key];
    }
  }
  
  return result;
}
```

### 3. Optimistic Concurrency Control

We will add version numbers or ETags to detect conflicts more precisely, enabling better conflict handling:

```typescript
// Optimistic concurrency control with versioning
async function updateWithVersioning(table, id, data) {
  // First retrieve current version
  const { data: current, error: fetchError } = await supabase
    .from(table)
    .select('version, *')
    .eq('id', id)
    .single();
    
  if (fetchError) throw fetchError;
  
  // Current version plus our data with incremented version
  const currentVersion = current.version;
  const updateData = {
    ...data,
    version: currentVersion + 1
  };
  
  // Update with version check
  const { data: result, error: updateError } = await supabase
    .from(table)
    .update(updateData)
    .eq('id', id)
    .eq('version', currentVersion) // Will fail if version changed
    .select();
    
  if (updateError) {
    // Handle concurrency conflict
    throw new ConcurrencyError('Record was modified by another user');
  }
  
  // If no rows updated, we have a concurrency issue
  if (result.length === 0) {
    // Fetch the latest version for conflict resolution
    const { data: latest } = await supabase
      .from(table)
      .select('*')
      .eq('id', id)
      .single();
      
    throw new ConcurrencyConflictError('Version conflict detected', {
      currentVersion: latest,
      attemptedUpdate: data
    });
  }
  
  return result;
}
```

Database trigger implementation:

```sql
-- Add version column to tables
ALTER TABLE scores ADD COLUMN version INTEGER NOT NULL DEFAULT 1;

-- Create trigger to enforce version check
CREATE OR REPLACE FUNCTION check_version_on_update()
RETURNS TRIGGER AS $$
BEGIN
  -- If version matches, increment it
  IF OLD.version = NEW.version - 1 THEN
    RETURN NEW;
  ELSE
    -- Version mismatch, reject update
    RAISE EXCEPTION 'Version conflict: expected %, found %', 
      OLD.version + 1, NEW.version
      USING HINT = 'Refresh and try again';
  END IF;
END;
$$ LANGUAGE plpgsql;

-- Apply the trigger to relevant tables
CREATE TRIGGER enforce_versioning
BEFORE UPDATE ON scores
FOR EACH ROW
EXECUTE FUNCTION check_version_on_update();
```

### 4. Operational Transformation

For future collaborative editing features, we will implement operational transformation algorithms that allow for real-time collaborative editing:

```typescript
// Basic operational transformation for collaborative text editing
class OTClient {
  private pendingOps: Operation[] = [];
  private serverVersion: number = 0;
  private localVersion: number = 0;
  
  constructor(
    private documentId: string,
    private initialContent: string,
    private onUpdate: (content: string) => void,
    private supabase: SupabaseClient
  ) {
    // Subscribe to remote changes
    this.subscribeToChanges();
  }
  
  // Local edit happened
  public applyLocalOperation(operation: Operation): void {
    // Apply locally first
    this.applyOperation(operation);
    
    // Track as pending
    this.pendingOps.push(operation);
    this.localVersion++;
    
    // Send to server
    this.sendOperation(operation);
  }
  
  // Apply an operation to the local document
  private applyOperation(operation: Operation): void {
    // Apply change to local content
    const newContent = executeOperation(this.initialContent, operation);
    this.initialContent = newContent;
    
    // Notify UI
    this.onUpdate(newContent);
  }
  
  // Send operation to server
  private async sendOperation(operation: Operation): Promise<void> {
    try {
      const { data, error } = await this.supabase
        .from('document_operations')
        .insert({
          document_id: this.documentId,
          operation: operation,
          base_version: this.serverVersion,
          client_id: getClientId()
        });
        
      if (error) throw error;
      
      // Operation accepted, update server version
      this.serverVersion++;
      
      // Remove from pending
      this.pendingOps.shift();
      
    } catch (error) {
      console.error('Failed to send operation', error);
      // Will be retried on reconnect
    }
  }
  
  // Subscribe to changes from other clients
  private subscribeToChanges(): void {
    this.supabase
      .from(`document_operations:document_id=eq.${this.documentId}`)
      .on('INSERT', async (payload) => {
        const remoteOp = payload.new;
        
        // Skip our own operations
        if (remoteOp.client_id === getClientId()) return;
        
        // Transform remote operation against pending local operations
        let transformedOp = remoteOp.operation;
        for (const pendingOp of this.pendingOps) {
          transformedOp = transformOperation(transformedOp, pendingOp);
        }
        
        // Apply transformed operation
        this.applyOperation(transformedOp);
        
        // Update server version
        this.serverVersion = Math.max(this.serverVersion, remoteOp.base_version + 1);
      })
      .subscribe();
  }
}
```

### 5. Blockchain-Inspired Audit Trail

For scenarios requiring high auditability (e.g., official tournaments), implement a blockchain-inspired append-only ledger of all changes with cryptographic verification:

```typescript
// Cryptographic verification of change history
async function addToChangeLog(change) {
  // Get latest change record to create chain
  const { data: latestChange } = await supabase
    .from('change_log')
    .select('id, hash')
    .order('created_at', { ascending: false })
    .limit(1);
    
  // Calculate hash of current change
  const previousHash = latestChange?.hash || '0000000000000000';
  const changeData = JSON.stringify(change);
  const timestamp = new Date().toISOString();
  
  // Calculate hash including previous hash (creating a chain)
  const hash = await crypto.subtle.digest(
    'SHA-256',
    new TextEncoder().encode(`${previousHash}:${changeData}:${timestamp}`)
  );
  
  // Convert hash to hex string
  const hashHex = Array.from(new Uint8Array(hash))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');
    
  // Insert into log
  const { data, error } = await supabase
    .from('change_log')
    .insert({
      entity_id: change.entityId,
      entity_type: change.entityType,
      operation: change.operation,
      user_id: change.userId,
      change_data: changeData,
      previous_hash: previousHash,
      hash: hashHex,
      created_at: timestamp
    });
    
  if (error) throw error;
  
  return data;
}

// Verify integrity of change history
async function verifyChangeLogIntegrity(entityId) {
  // Get all changes for this entity in order
  const { data: changes, error } = await supabase
    .from('change_log')
    .select('*')
    .eq('entity_id', entityId)
    .order('created_at', { ascending: true });
    
  if (error) throw error;
  
  let previousHash = '0000000000000000';
  
  // Verify each change
  for (const change of changes) {
    // Verify this change references the correct previous hash
    if (change.previous_hash !== previousHash) {
      return {
        valid: false,
        error: `Chain broken at change ${change.id}: expected previous hash ${previousHash} but found ${change.previous_hash}`
      };
    }
    
    // Recalculate hash to verify
    const calculateHash = await crypto.subtle.digest(
      'SHA-256',
      new TextEncoder().encode(`${previousHash}:${change.change_data}:${change.created_at}`)
    );
    
    const calculatedHashHex = Array.from(new Uint8Array(calculateHash))
      .map(b => b.toString(16).padStart(2, '0'))
      .join('');
      
    if (calculatedHashHex !== change.hash) {
      return {
        valid: false,
        error: `Hash mismatch at change ${change.id}: calculated ${calculatedHashHex} but recorded ${change.hash}`
      };
    }
    
    // This change is valid, use its hash for next verification
    previousHash = change.hash;
  }
  
  return { valid: true };
}
```

### 6. Machine Learning-Based Conflict Resolution

In the long term, we aim to implement ML-based conflict resolution for high-confidence automated resolving of complex conflicts:

```typescript
// Conceptual implementation of ML-based conflict resolution
async function resolveConflictWithML(localChange, serverChange, changeContext) {
  // Prepare features for the ML model
  const features = extractConflictFeatures(localChange, serverChange, changeContext);
  
  // Call ML service
  const predictionResult = await mlService.predict(
    'conflict-resolution-model',
    features
  );
  
  // Get prediction and confidence
  const { prediction, confidence } = predictionResult;
  
  // Apply ML decision if confidence is high enough
  if (confidence > 0.85) {
    return prediction === 'local' ? localChange : serverChange;
  }
  
  // Fall back to rule-based resolution for low confidence cases
  return fallbackResolution(localChange, serverChange);
}

// Extract features for ML model
function extractConflictFeatures(localChange, serverChange, context) {
  return {
    // Time-based features
    timeDifference: serverChange.timestamp - localChange.timestamp,
    localOfflineDuration: context.localOfflineDuration,
    
    // User role features
    localUserIsScorer: context.localUserRoles.includes('scorer'),
    serverUserIsScorer: context.serverUserRoles.includes('scorer'),
    localUserIsAdmin: context.localUserRoles.includes('admin'),
    serverUserIsAdmin: context.serverUserRoles.includes('admin'),
    
    // Change content features
    contentSimilarity: calculateSimilarity(localChange.data, serverChange.data),
    localChangeSize: calculateChangeSize(localChange),
    serverChangeSize: calculateChangeSize(serverChange),
    
    // Context features
    entityType: context.entityType,
    isScoreCard: context.entityType === 'scorecard',
    tournamentPhase: context.tournamentPhase,
    
    // Historical pattern features
    userEditAccuracyScore: context.userEditAccuracyScore
  };
}
```

## Conclusion

Our conflict resolution strategy prioritizes simplicity and practicality while addressing the real-world usage patterns of the Golf Tournament Organizer application. The "last edit wins" approach, with special handling for edge cases, provides a robust solution that minimizes disruption to users while maintaining data integrity.

The future enhancements outlined above provide a clear roadmap for evolving our conflict resolution capabilities as the application grows and user needs become more sophisticated.
