# Advanced Conflict Resolution Edge Cases

This document details specialized edge cases and their handling in our conflict resolution strategy for the Golf Tournament Organizer application. These scenarios go beyond the basic "last edit wins" approach to address complex real-world situations.

## Critical Tournament Edge Cases

### 1. Authoritative Score Overrides

In tournament contexts, certain users have authoritative roles that override normal conflict resolution:

```typescript
/**
 * Handles score conflicts with authority-based resolution
 * 
 * Priority order:
 * 1. Tournament Committee Member
 * 2. Official Scorer
 * 3. Designated Recorder
 * 4. Player Self-Reporting
 * 5. General User
 */
function resolveScoreWithAuthority(localScore, serverScore, userRoles) {
  // Define authority levels (higher number = higher authority)
  const authorityLevels = {
    'tournament_committee': 5,
    'official_scorer': 4,
    'recorder': 3,
    'player': 2,
    'user': 1
  };
  
  // Get authority levels for each score
  const localAuthorityLevel = Math.max(
    ...userRoles.local.map(role => authorityLevels[role] || 0)
  );
  
  const serverAuthorityLevel = Math.max(
    ...userRoles.server.map(role => authorityLevels[role] || 0)
  );
  
  // If authority levels differ, higher authority wins regardless of timestamp
  if (localAuthorityLevel !== serverAuthorityLevel) {
    const result = localAuthorityLevel > serverAuthorityLevel ? localScore : serverScore;
    return {
      resolvedScore: result,
      resolution: localAuthorityLevel > serverAuthorityLevel 
        ? 'LOCAL_HIGHER_AUTHORITY' 
        : 'SERVER_HIGHER_AUTHORITY',
      explanation: `Score from user with ${result === localScore ? 'local' : 'server'} 
        higher authority (${Math.max(localAuthorityLevel, serverAuthorityLevel)}) was chosen.`
    };
  }
  
  // Equal authority, fall back to timestamp-based resolution
  return resolveByTimestamp(localScore, serverScore);
}
```

### 2. Disqualification Handling

Disqualification events must be carefully handled in conflict resolution:

```typescript
function handleDisqualificationConflict(localPlayerData, serverPlayerData) {
  // Check if either version includes disqualification
  const localHasDQ = localPlayerData.status === 'disqualified';
  const serverHasDQ = serverPlayerData.status === 'disqualified';
  
  // If server has DQ but local doesn't, server wins regardless of timestamp
  if (serverHasDQ && !localHasDQ) {
    return {
      resolvedData: serverPlayerData,
      resolution: 'SERVER_DISQUALIFICATION_PRESERVED',
      conflicts: [{
        field: 'status',
        localValue: localPlayerData.status,
        serverValue: 'disqualified',
        resolution: 'SERVER_VALUE_USED'
      }]
    };
  }
  
  // If local has DQ but server doesn't, need to verify with committee
  if (localHasDQ && !serverHasDQ) {
    // Only tournament committee or admin can disqualify
    if (hasRole(['tournament_committee', 'admin'])) {
      return {
        resolvedData: localPlayerData,
        resolution: 'LOCAL_DISQUALIFICATION_APPLIED',
        conflicts: [{
          field: 'status',
          localValue: 'disqualified',
          serverValue: serverPlayerData.status,
          resolution: 'LOCAL_VALUE_USED'
        }]
      };
    } else {
      // Non-authorized users cannot apply disqualification
      return {
        resolvedData: serverPlayerData,
        resolution: 'LOCAL_DISQUALIFICATION_REJECTED',
        conflicts: [{
          field: 'status',
          localValue: 'disqualified',
          serverValue: serverPlayerData.status,
          resolution: 'SERVER_VALUE_USED'
        }]
      };
    }
  }
  
  // Both have same DQ status, resolve normally
  return resolveWithFieldLevelMerging(localPlayerData, serverPlayerData);
}
```

### 3. Tournament Phase Transitions

Special handling when tournament phases change (e.g., from "active" to "completed"):

```typescript
function resolveTournamentPhaseConflict(localTournament, serverTournament) {
  // Define tournament phase weights (higher = later in tournament lifecycle)
  const phaseWeights = {
    'draft': 1,
    'registration': 2,
    'ready': 3,
    'active': 4,
    'paused': 4.5,
    'scoring_closed': 5,
    'reviewing': 6,
    'completed': 7,
    'cancelled': 7,
    'archived': 8
  };
  
  const localPhase = localTournament.phase;
  const serverPhase = serverTournament.phase;
  
  // Get phase weights
  const localWeight = phaseWeights[localPhase] || 0;
  const serverWeight = phaseWeights[serverPhase] || 0;
  
  // Phase transition validation logic
  const validTransitions = {
    'draft': ['registration', 'cancelled'],
    'registration': ['draft', 'ready', 'cancelled'],
    'ready': ['registration', 'active', 'cancelled'],
    'active': ['paused', 'scoring_closed', 'cancelled'],
    'paused': ['active', 'scoring_closed', 'cancelled'],
    'scoring_closed': ['reviewing', 'completed', 'cancelled'],
    'reviewing': ['scoring_closed', 'completed', 'cancelled'],
    'completed': ['archived'],
    'cancelled': ['archived'],
    'archived': []
  };
  
  // If phases differ, apply special logic
  if (localPhase !== serverPhase) {
    // Check for valid phase transitions
    const isLocalTransitionValid = (
      validTransitions[serverPhase] && 
      validTransitions[serverPhase].includes(localPhase)
    );
    
    const isServerTransitionValid = (
      validTransitions[localPhase] && 
      validTransitions[localPhase].includes(serverPhase)
    );
    
    // If only one transition is valid, that one wins
    if (isLocalTransitionValid && !isServerTransitionValid) {
      // Apply local phase but keep other server updates
      return {
        ...serverTournament,
        phase: localPhase,
        updated_at: new Date().toISOString() // Generate new timestamp
      };
    } else if (!isLocalTransitionValid && isServerTransitionValid) {
      // Keep server phase
      return serverTournament;
    }
    
    // If both transitions valid or both invalid, prefer forward movement in tournament lifecycle
    if (localWeight !== serverWeight) {
      const preferredPhase = localWeight > serverWeight ? localPhase : serverPhase;
      const baseRecord = localWeight > serverWeight ? localTournament : serverTournament;
      
      return {
        ...baseRecord,
        phase: preferredPhase,
        phaseChangedAt: new Date().toISOString()
      };
    }
  }
  
  // Default to standard timestamp-based resolution for same phase
  // or when no resolution could be determined
  return localTournament.updated_at > serverTournament.updated_at 
    ? localTournament 
    : serverTournament;
}
```

### 4. Score Correction After Finalization

Handling score corrections after a tournament round is finalized:

```typescript
async function handleLateScoreCorrection(
  correctionData,
  tournamentId,
  roundId,
  scoreId
) {
  // First check if round is finalized
  const { data: round } = await supabase
    .from('tournament_rounds')
    .select('*')
    .eq('id', roundId)
    .single();
    
  const isFinalized = round.status === 'finalized';
  
  if (isFinalized) {
    // Create an audit trail for the correction
    const correctionLog = {
      tournament_id: tournamentId,
      round_id: roundId,
      score_id: scoreId,
      previous_value: correctionData.previousValue,
      corrected_value: correctionData.newValue,
      correction_reason: correctionData.reason,
      corrected_by: correctionData.userId,
      corrected_at: new Date().toISOString(),
      requires_approval: !hasRole(['tournament_committee', 'admin'])
    };
    
    // Log the correction request
    const { data: loggedCorrection } = await supabase
      .from('score_correction_log')
      .insert(correctionLog)
      .select();
      
    // If correction requires approval, don't apply yet
    if (correctionLog.requires_approval) {
      // Notify tournament committee
      await notifyCommitteeOfCorrection(correctionLog);
      
      return {
        applied: false,
        status: 'pending_approval',
        correctionId: loggedCorrection.id
      };
    }
    
    // Apply the correction (from admin or committee)
    const { data: updatedScore } = await supabase
      .from('scores')
      .update({
        value: correctionData.newValue,
        is_corrected: true,
        correction_id: loggedCorrection.id,
        updated_at: new Date().toISOString()
      })
      .eq('id', scoreId)
      .select();
      
    // Update tournament standings
    await recalculateStandings(tournamentId);
    
    return {
      applied: true,
      status: 'applied',
      correctionId: loggedCorrection.id
    };
  } else {
    // Round not finalized, apply normal update
    const { data: updatedScore } = await supabase
      .from('scores')
      .update({
        value: correctionData.newValue,
        updated_at: new Date().toISOString()
      })
      .eq('id', scoreId)
      .select();
      
    return {
      applied: true,
      status: 'normal_update'
    };
  }
}
```

## Technical Synchronization Edge Cases

### 1. Interrupted Sync Recovery

Handling cases where synchronization was interrupted mid-process:

```typescript
async function recoverFromInterruptedSync() {
  // Check for interrupted sync in metadata
  const { data: syncState } = await localDb.metadata
    .where('key')
    .equals('sync_state')
    .first();
    
  if (syncState && syncState.value === 'in_progress') {
    // Get last successful sync record
    const { data: lastRecord } = await localDb.metadata
      .where('key')
      .equals('last_successful_sync_record')
      .first();
      
    // Get transaction log
    const { data: transactionLog } = await localDb.syncTransactions
      .where('id')
      .equals(syncState.transactionId)
      .first();
      
    if (!transactionLog) {
      // No transaction log found, reset sync state
      await localDb.metadata.put({ key: 'sync_state', value: 'idle' });
      return;
    }
    
    // Determine which records were processed
    const processedIds = new Set(transactionLog.processedRecords || []);
    
    // Get pending records that weren't processed
    const pendingChanges = await localDb.pendingChanges.toArray();
    const unprocessedChanges = pendingChanges.filter(change => 
      !processedIds.has(change.id)
    );
    
    // Log the recovery
    console.log(`Recovering from interrupted sync. ${processedIds.size} records were processed, 
      ${unprocessedChanges.length} records still pending.`);
    
    // Clear the pending queue and re-add only unprocessed changes
    await localDb.pendingChanges.clear();
    if (unprocessedChanges.length > 0) {
      await localDb.pendingChanges.bulkAdd(unprocessedChanges);
    }
    
    // Reset sync state
    await localDb.metadata.put({ key: 'sync_state', value: 'idle' });
    
    return {
      recovered: true,
      processedCount: processedIds.size,
      remainingCount: unprocessedChanges.length
    };
  }
  
  return { recovered: false };
}
```

### 2. Timestamp Drift Handling

Managing inconsistent client timestamps between devices:

```typescript
function normalizeTimestamps(record, serverTimeOffset) {
  // If we don't have a server time offset yet, don't adjust
  if (serverTimeOffset === null) return record;
  
  // Clone the record to avoid modifying the original
  const normalizedRecord = { ...record };
  
  // Adjust created_at if present
  if (normalizedRecord.created_at) {
    const createdDate = new Date(normalizedRecord.created_at);
    createdDate.setMilliseconds(createdDate.getMilliseconds() + serverTimeOffset);
    normalizedRecord.created_at = createdDate.toISOString();
  }
  
  // Adjust updated_at
  if (normalizedRecord.updated_at) {
    const updatedDate = new Date(normalizedRecord.updated_at);
    updatedDate.setMilliseconds(updatedDate.getMilliseconds() + serverTimeOffset);
    normalizedRecord.updated_at = updatedDate.toISOString();
  }
  
  // Adjust any other timestamp fields specific to this record type
  if (normalizedRecord.submitted_at) {
    const submittedDate = new Date(normalizedRecord.submitted_at);
    submittedDate.setMilliseconds(submittedDate.getMilliseconds() + serverTimeOffset);
    normalizedRecord.submitted_at = submittedDate.toISOString();
  }
  
  return normalizedRecord;
}

// Calculate server time offset
async function calculateServerTimeOffset() {
  // Send current timestamp to server
  const clientTime = new Date().getTime();
  
  const { data, error } = await supabase
    .rpc('get_server_time', { client_time: clientTime });
    
  if (error) {
    console.error('Failed to get server time', error);
    return null;
  }
  
  // Calculate difference between server time and client time
  const serverTime = new Date(data.server_time).getTime();
  const roundTripTime = (new Date().getTime() - clientTime) / 2; // Estimate one-way latency
  
  // Adjust for round trip time
  const adjustedServerTime = serverTime + roundTripTime;
  
  // Calculate offset
  const offset = adjustedServerTime - clientTime;
  
  // Store this offset
  await localDb.metadata.put({ key: 'server_time_offset', value: offset });
  
  return offset;
}
```

### 3. Partial Update Conflict Handling

More sophisticated field-level merging for complex conflicts:

```typescript
function mergeFieldsWithMetadata(localRecord, serverRecord, fieldMetadata) {
  // Start with a copy of the server record as base
  const mergedRecord = { ...serverRecord };
  
  // Track which fields were merged and how
  const mergeInfo = {};
  
  // Process each field with its metadata
  for (const field in fieldMetadata) {
    if (!fieldMetadata.hasOwnProperty(field)) continue;
    
    const metadata = fieldMetadata[field];
    
    // Skip fields that don't exist in both records
    if (!(field in localRecord) || !(field in serverRecord)) {
      if (field in localRecord) {
        mergedRecord[field] = localRecord[field];
        mergeInfo[field] = 'added_from_local';
      }
      continue;
    }
    
    // Check field modification dates
    const localModified = metadata.localModified || localRecord.updated_at;
    const serverModified = metadata.serverModified || serverRecord.updated_at;
    
    // Convert dates for comparison
    const localDate = new Date(localModified).getTime();
    const serverDate = new Date(serverModified).getTime();
    
    // Determine which version to use based on field-specific rules
    if (metadata.mergeStrategy === 'custom' && metadata.customMerge) {
      // Use custom merge function for this field
      mergedRecord[field] = metadata.customMerge(
        localRecord[field], 
        serverRecord[field],
        { localRecord, serverRecord }
      );
      mergeInfo[field] = 'custom_merged';
    } else if (metadata.mergeStrategy === 'prefer_local') {
      // Always prefer local value for this field
      mergedRecord[field] = localRecord[field];
      mergeInfo[field] = 'prefer_local';
    } else if (metadata.mergeStrategy === 'prefer_server') {
      // Always prefer server value for this field
      mergedRecord[field] = serverRecord[field];
      mergeInfo[field] = 'prefer_server';
    } else {
      // Default to timestamp-based strategy
      if (localDate > serverDate) {
        mergedRecord[field] = localRecord[field];
        mergeInfo[field] = 'local_newer';
      } else {
        // Server is newer or same time, keep server value
        mergeInfo[field] = 'server_newer_or_equal';
      }
    }
  }
  
  // Add merge metadata
  mergedRecord._mergeInfo = mergeInfo;
  mergedRecord.merged_at = new Date().toISOString();
  
  return mergedRecord;
}

// Example usage with field metadata
const playerRecordMetadata = {
  name: { mergeStrategy: 'timestamp' },
  email: { mergeStrategy: 'timestamp' },
  phone: { mergeStrategy: 'timestamp' },
  handicap: { 
    mergeStrategy: 'custom',
    customMerge: (localVal, serverVal, context) => {
      // For handicap, take the most frequently updated one
      const localHandicapHistory = context.localRecord.handicap_history || [];
      const serverHandicapHistory = context.serverRecord.handicap_history || [];
      
      return localHandicapHistory.length > serverHandicapHistory.length 
        ? localVal 
        : serverVal;
    }
  },
  notes: {
    mergeStrategy: 'custom',
    customMerge: (localVal, serverVal) => {
      // For notes, merge text if possible
      if (typeof localVal === 'string' && typeof serverVal === 'string') {
        if (localVal === serverVal) return localVal;
        return `${serverVal}\n--- MERGED NOTES ---\n${localVal}`;
      }
      return localVal || serverVal;
    }
  },
  preferences: {
    mergeStrategy: 'custom',
    customMerge: (localVal, serverVal) => {
      // For preferences object, merge properties
      return { ...serverVal, ...localVal };
    }
  }
};
```

### 4. History-Aware Resolution

Conflict resolution that considers the edit history:

```typescript
async function resolveWithHistory(localRecord, serverRecord) {
  // Get edit history for this record
  const { data: editHistory } = await supabase
    .from('edit_history')
    .select('*')
    .eq('record_id', localRecord.id)
    .order('edited_at', { ascending: false })
    .limit(10);
    
  if (!editHistory || editHistory.length === 0) {
    // No history, use standard resolution
    return standardResolver.resolve(localRecord, serverRecord);
  }
  
  // Check if local change is based on outdated version
  const localBaseVersion = localRecord._baseVersion;
  
  if (localBaseVersion) {
    // Find position of base version in history
    const baseVersionIndex = editHistory.findIndex(h => 
      h.version === localBaseVersion
    );
    
    if (baseVersionIndex > 0) {
      // Local change was based on outdated version
      // There are newer versions between the base and the current server version
      
      // Check if changes can be automatically merged
      const intermediateChanges = editHistory.slice(0, baseVersionIndex);
      
      // Analyze which fields changed in intermediate edits
      const fieldsChangedSinceBase = new Set();
      for (const change of intermediateChanges) {
        if (change.changed_fields) {
          for (const field of change.changed_fields) {
            fieldsChangedSinceBase.add(field);
          }
        }
      }
      
      // Get fields changed in local edit
      const locallyChangedFields = getChangedFields(localRecord, editHistory[baseVersionIndex].data);
      
      // Check for conflicts in specific fields
      const conflictingFields = locallyChangedFields.filter(field => 
        fieldsChangedSinceBase.has(field)
      );
      
      if (conflictingFields.length === 0) {
        // No conflicts in changed fields, can safely apply local changes
        const mergedRecord = { ...serverRecord };
        
        for (const field of locallyChangedFields) {
          mergedRecord[field] = localRecord[field];
        }
        
        return {
          resolvedRecord: mergedRecord,
          resolution: 'HISTORY_AWARE_MERGE',
          conflicts: [],
          strategy: 'field_level_merge'
        };
      } else {
        // Conflicting fields detected, need more careful merging
        return {
          resolvedRecord: await mergeWithConflictingFields(
            localRecord, 
            serverRecord, 
            conflictingFields,
            intermediateChanges
          ),
          resolution: 'HISTORY_AWARE_MERGE_WITH_CONFLICTS',
          conflicts: conflictingFields.map(field => ({
            field,
            localValue: localRecord[field],
            serverValue: serverRecord[field]
          })),
          strategy: 'field_level_merge_with_conflict_resolution'
        };
      }
    }
  }
  
  // No history information available for smarter merging
  return standardResolver.resolve(localRecord, serverRecord);
}
```

### 5. Distributed Version Vectors

More sophisticated version tracking for multi-device synchronization:

```typescript
class VersionVector {
  constructor(initialVector = {}) {
    this.vector = { ...initialVector };
  }
  
  // Increment this client's counter
  increment(clientId) {
    if (!this.vector[clientId]) {
      this.vector[clientId] = 1;
    } else {
      this.vector[clientId]++;
    }
    return this;
  }
  
  // Merge with another version vector
  merge(otherVector) {
    const result = new VersionVector(this.vector);
    
    for (const [clientId, counter] of Object.entries(otherVector.vector)) {
      if (!result.vector[clientId] || result.vector[clientId] < counter) {
        result.vector[clientId] = counter;
      }
    }
    
    return result;
  }
  
  // Check if this vector is causally before another
  isBefore(otherVector) {
    // Check if every entry in this vector is <= the other vector
    // And at least one is strictly less than
    let hasLessThan = false;
    
    for (const [clientId, counter] of Object.entries(this.vector)) {
      if (!otherVector.vector[clientId] || counter > otherVector.vector[clientId]) {
        return false;
      }
      if (counter < otherVector.vector[clientId]) {
        hasLessThan = true;
      }
    }
    
    // Check for client IDs in other but not in this
    for (const clientId of Object.keys(otherVector.vector)) {
      if (!this.vector[clientId]) {
        hasLessThan = true;
        break;
      }
    }
    
    return hasLessThan;
  }
  
  // Check if this vector is concurrent with another (neither before nor after)
  isConcurrentWith(otherVector) {
    return !this.isBefore(otherVector) && !otherVector.isBefore(this);
  }
  
  // Serialize to string
  toString() {
    return JSON.stringify(this.vector);
  }
  
  // Create from string
  static fromString(str) {
    try {
      return new VersionVector(JSON.parse(str));
    } catch (e) {
      console.error('Failed to parse version vector', e);
      return new VersionVector();
    }
  }
}

// Use version vectors for conflict detection
function detectConflictWithVersionVectors(localRecord, serverRecord) {
  // Parse version vectors
  const localVector = VersionVector.fromString(localRecord._version || '{}');
  const serverVector = VersionVector.fromString(serverRecord._version || '{}');
  
  // Check relationship
  if (localVector.isBefore(serverVector)) {
    return {
      hasConflict: false,
      resolution: 'SERVER_NEWER'
    };
  } else if (serverVector.isBefore(localVector)) {
    return {
      hasConflict: false,
      resolution: 'LOCAL_NEWER'
    };
  } else {
    // Vectors are concurrent, conflict detected
    return {
      hasConflict: true,
      resolution: 'CONCURRENT_EDITS'
    };
  }
}

// Update version vector when saving
async function saveWithVersionVector(record, clientId) {
  // Get current record to get its version vector
  const { data: currentRecord } = await supabase
    .from('records')
    .select('_version')
    .eq('id', record.id)
    .single();
    
  // Parse current version vector
  const currentVector = currentRecord && currentRecord._version
    ? VersionVector.fromString(currentRecord._version)
    : new VersionVector();
    
  // Increment client's counter in the vector
  const newVector = currentVector.increment(clientId);
  
  // Update record with new version vector
  const updatedRecord = {
    ...record,
    _version: newVector.toString()
  };
  
  // Save to database
  const { data, error } = await supabase
    .from('records')
    .upsert(updatedRecord);
    
  return { data, error };
}
```

## User Experience Edge Cases

### 1. Conflict Communication Design

Strategies for informing users about conflicts in a non-intrusive manner:

```typescript
// Different notification levels based on conflict severity
function notifyUserAboutConflict(conflictDetails, severity) {
  switch (severity) {
    case 'high':
      // Immediate attention required - modal dialog
      showConflictResolutionModal(conflictDetails);
      break;
      
    case 'medium':
      // Important but not blocking - toast notification with action
      showToastNotification({
        message: 'Some of your changes conflicted with recent updates',
        actionText: 'Review',
        actionHandler: () => showConflictDetails(conflictDetails),
        duration: 8000, // 8 seconds
        type: 'warning'
      });
      break;
      
    case 'low':
      // Informational - subtle indicator
      addToNotificationCenter({
        type: 'conflict',
        title: 'Data Updated',
        message: 'Some data was updated with newer information',
        timestamp: new Date(),
        details: conflictDetails
      });
      
      // Show indicator but don't interrupt
      showIndicator('conflict-indicator');
      break;
      
    default:
      // Minimal - just log it
      console.log('Conflict resolved automatically', conflictDetails);
  }
}

// Determine severity based on conflict analysis
function analyzeConflictSeverity(conflict) {
  // Check which fields were affected
  const criticalFields = ['score', 'status', 'disqualified', 'withdrawn'];
  const importantFields = ['handicap', 'tee_time', 'player_name'];
  
  // Check if any critical fields were affected
  const hasCriticalChanges = conflict.fields.some(field => 
    criticalFields.includes(field.name)
  );
  
  // Check for significant value differences
  const hasSignificantChanges = conflict.fields.some(field => {
    if (field.type === 'number') {
      // For numeric fields, check if difference is substantial
      const diff = Math.abs(field.localValue - field.serverValue);
      const percentage = field.serverValue ? (diff / field.serverValue) * 100 : 0;
      return diff > 3 || percentage > 20; // Arbitrary thresholds
    }
    return false;
  });
  
  // Determine severity based on analysis
  if (hasCriticalChanges || hasSignificantChanges) {
    return 'high';
  } else if (conflict.fields.some(field => importantFields.includes(field.name))) {
    return 'medium';
  } else {
    return 'low';
  }
}
```

### 2. Graceful Degradation for Low Bandwidth

Handling synchronization in severely constrained network environments:

```typescript
// Network-aware sync strategy
async function smartBandwidthSync() {
  // Check current network conditions
  const networkInfo = await getNetworkInformation();
  
  // Determine sync strategy based on network quality
  if (networkInfo.downlink < 0.5 || networkInfo.rtt > 1000) { // Slow connection
    return performLightweightSync();
  } else if (networkInfo.downlink < 2 || networkInfo.rtt > 500) { // Medium connection
    return performStandardSync();
  } else { // Good connection
    return performFullSync();
  }
}

// Lightweight sync for very poor connections
async function performLightweightSync() {
  console.log('Performing lightweight sync due to poor connection');
  
  // Get only the most critical pending changes
  const criticalChanges = await prioritizePendingChanges();
  const essentialChanges = criticalChanges.slice(0, 10); // Limit to 10 most important
  
  // Use minimal fields for sync
  const minimalChanges = essentialChanges.map(change => {
    // Keep only essential fields to reduce payload size
    const { id, entity_type, entity_id, updated_at } = change;
    
    // Extract only the critical fields from the data
    const criticalFields = extractCriticalFields(change.data, change.entity_type);
    
    return {
      id,
      entity_type,
      entity_id,
      updated_at,
      data: criticalFields
    };
  });
  
  // Use compressed format for sync
  const compressedPayload = await compressChanges(minimalChanges);
  
  // Send with timeout and retry options
  try {
    const result = await sendWithRetry(
      '/api/sync/minimal',
      compressedPayload,
      {
        timeout: 20000, // 20 seconds timeout for slow connections
        retries: 3,
        retryDelay: 5000 // 5 seconds between retries
      }
    );
    
    // Mark synced items
    await markAsSynced(result.successful_ids);
    
    return {
      success: true,
      syncedCount: result.successful_ids.length,
      strategy: 'lightweight'
    };
  } catch (error) {
    console.error('Lightweight sync failed', error);
    // Schedule retry for later
    scheduleBackgroundSync(60000); // Try again in 1 minute
    
    return {
      success: false,
      error,
      strategy: 'lightweight'
    };
  }
}

// Prioritize changes based on importance
async function prioritizePendingChanges() {
  const pendingChanges = await localDb.pendingChanges.toArray();
  
  // Score each change by importance
  const scoredChanges = pendingChanges.map(change => {
    let importance = 0;
    
    // Score by entity type
    switch (change.entity_type) {
      case 'score':
        importance += 10;
        break;
      case 'player_status':
        importance += 8;
        break;
      case 'tournament_status':
        importance += 9;
        break;
      default:
        importance += 5;
    }
    
    // Older changes get higher priority
    const ageInMinutes = (Date.now() - new Date(change.created_at).getTime()) / 60000;
    importance += Math.min(ageInMinutes / 60, 5); // Max 5 points for age (after 5 hours)
    
    // Size penalty
    const dataSize = JSON.stringify(change.data).length;
    importance -= Math.min(dataSize / 5000, 3); // Penalty for large changes, max 3 points
    
    return {
      ...change,
      importance
    };
  });
  
  // Sort by importance (highest first)
  return scoredChanges.sort((a, b) => b.importance - a.importance);
}
```

### 3. Handling Simultaneous Edits Gracefully

Real-time awareness of concurrent editing:

```typescript
// Listen for other users editing the same entity
function setupConcurrentEditAwareness(entityId, entityType) {
  // Subscribe to presence channel
  const presenceChannel = supabase.channel(`presence:${entityType}:${entityId}`);
  
  // Set up local user's presence
  presenceChannel.on('presence', { event: 'sync' }, () => {
    // Update UI with who else is editing
    updateConcurrentEditorsUI(presenceChannel.presenceState());
  });
  
  presenceChannel.on('presence', { event: 'join' }, ({ key, newPresences }) => {
    // Someone new started editing this entity
    notifyUserOfConcurrentEditor(newPresences[0]);
  });
  
  presenceChannel.on('presence', { event: 'leave' }, ({ key, leftPresences }) => {
    // Someone stopped editing
    updateConcurrentEditorsUI(presenceChannel.presenceState());
  });
  
  // Start tracking presence
  presenceChannel.subscribe(async (status) => {
    if (status === 'SUBSCRIBED') {
      // Track our presence
      await presenceChannel.track({
        user_id: currentUser.id,
        username: currentUser.username,
        editing_since: new Date().toISOString(),
        client_id: getDeviceId()
      });
    }
  });
  
  // Return function to clean up
  return () => {
    presenceChannel.unsubscribe();
  };
}

// Update UI to show concurrent editors
function updateConcurrentEditorsUI(presenceState) {
  const editors = [];
  
  // Convert presence state to list of editors
  Object.values(presenceState).forEach(presences => {
    presences.forEach(presence => {
      // Skip our own presence
      if (presence.user_id === currentUser.id) return;
      
      editors.push({
        userId: presence.user_id,
        username: presence.username,
        editingSince: new Date(presence.editing_since)
      });
    });
  });
  
  if (editors.length > 0) {
    // Show concurrent editing indicator
    document.getElementById('concurrent-editors-indicator').style.display = 'block';
    
    // Update badge count
    document.getElementById('editors-count').textContent = editors.length;
    
    // Update tooltip with editor names
    const editorNames = editors.map(e => e.username).join(', ');
    document.getElementById('concurrent-editors-indicator').setAttribute(
      'title', 
      `Also being edited by: ${editorNames}`
    );
    
    // If this is a new concurrent edit, show toast
    if (editors.length > previousEditorCount) {
      showToastNotification({
        message: `${editors[editors.length - 1].username} is also editing this item`,
        type: 'info',
        duration: 3000
      });
    }
  } else {
    // Hide indicator when no concurrent editors
    document.getElementById('concurrent-editors-indicator').style.display = 'none';
  }
  
  // Store current count
  previousEditorCount = editors.length;
}
```

### 4. Progressive Disclosure of Conflict Information

Layered approach to conflict communication:

```typescript
// Multi-level conflict information disclosure
function showConflictInfo(conflicts, initialLevel = 'basic') {
  // Create conflict UI container
  const container = document.createElement('div');
  container.className = 'conflict-info-container';
  
  // Create basic level info (always shown)
  const basicInfo = document.createElement('div');
  basicInfo.className = 'conflict-basic-info';
  basicInfo.innerHTML = `
    <div class="conflict-icon">⚠️</div>
    <div class="conflict-summary">
      <h4>Some changes were updated</h4>
      <p>${conflicts.length} field(s) had newer values on the server</p>
    </div>
  `;
  container.appendChild(basicInfo);
  
  // Create intermediate level (toggleable)
  const intermediateInfo = document.createElement('div');
  intermediateInfo.className = 'conflict-intermediate-info';
  intermediateInfo.style.display = initialLevel === 'basic' ? 'none' : 'block';
  
  // Add affected fields list
  const fieldsList = document.createElement('ul');
  conflicts.forEach(conflict => {
    const fieldItem = document.createElement('li');
    fieldItem.innerHTML = `
      <span class="field-name">${formatFieldName(conflict.field)}</span>
      <span class="resolution-icon">${getResolutionIcon(conflict.resolution)}</span>
    `;
    fieldsList.appendChild(fieldItem);
  });
  intermediateInfo.appendChild(fieldsList);
  
  // Add "Show details" / "Hide details" toggle
  const detailsToggle = document.createElement('button');
  detailsToggle.className = 'conflict-details-toggle';
  detailsToggle.textContent = initialLevel === 'basic' ? 'Show details' : 'Hide details';
  detailsToggle.addEventListener('click', () => {
    if (intermediateInfo.style.display === 'none') {
      intermediateInfo.style.display = 'block';
      detailsToggle.textContent = 'Hide details';
    } else {
      intermediateInfo.style.display = 'none';
      detailsToggle.textContent = 'Show details';
    }
  });
  basicInfo.appendChild(detailsToggle);
  
  // Create advanced level (separate dialog)
  const advancedButton = document.createElement('button');
  advancedButton.className = 'conflict-advanced-button';
  advancedButton.textContent = 'View complete changes';
  advancedButton.addEventListener('click', () => {
    showDetailedConflictDialog(conflicts);
  });
  intermediateInfo.appendChild(advancedButton);
  
  container.appendChild(intermediateInfo);
  
  return container;
}

// Show detailed conflict information in a dialog
function showDetailedConflictDialog(conflicts) {
  // Create modal dialog
  const dialog = document.createElement('dialog');
  dialog.className = 'conflict-detail-dialog';
  
  // Create dialog content
  dialog.innerHTML = `
    <div class="dialog-header">
      <h3>Detailed Change Information</h3>
      <button class="close-button">×</button>
    </div>
    <div class="dialog-content">
      <div class="conflict-explanation">
        <p>These fields had different values locally and on the server. 
        The most recent changes were applied using our conflict resolution strategy.</p>
      </div>
      <div class="conflicts-table-container">
        <table class="conflicts-table">
          <thead>
            <tr>
              <th>Field</th>
              <th>Your Value</th>
              <th>Server Value</th>
              <th>Applied Value</th>
              <th>Reason</th>
            </tr>
          </thead>
          <tbody>
            ${conflicts.map(conflict => `
              <tr>
                <td>${formatFieldName(conflict.field)}</td>
                <td>${formatValue(conflict.localValue)}</td>
                <td>${formatValue(conflict.serverValue)}</td>
                <td>${formatValue(conflict.resolution === 'LOCAL_NEWER' ? 
                  conflict.localValue : conflict.serverValue)}</td>
                <td>${formatResolutionReason(conflict.resolution)}</td>
              </tr>
            `).join('')}
          </tbody>
        </table>
      </div>
    </div>
    <div class="dialog-footer">
      <button class="close-dialog-button">Close</button>
    </div>
  `;
  
  // Add dialog to DOM
  document.body.appendChild(dialog);
  
  // Set up event listeners
  dialog.querySelector('.close-button').addEventListener('click', () => {
    dialog.close();
  });
  
  dialog.querySelector('.close-dialog-button').addEventListener('click', () => {
    dialog.close();
  });
  
  dialog.addEventListener('close', () => {
    dialog.remove();
  });
  
  // Show dialog
  dialog.showModal();
}
```

## Conclusion

These advanced conflict resolution edge cases ensure our Golf Tournament Organizer application gracefully handles complex synchronization challenges in real-world tournament scenarios. By implementing these specialized solutions, we create a robust system that maintains data integrity while providing a seamless user experience, even in challenging connectivity environments.

The strategies outlined in this document complement our core "last edit wins" approach with sophisticated handling for specific situations that require more nuanced resolution logic. These edge case handlers strengthen the overall conflict resolution system, making it more reliable and predictable for tournament organizers and participants.
