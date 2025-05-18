# Conflict Resolution Flow Diagrams

This document provides visual representations of the conflict resolution process in the Golf Tournament Organizer application.

## Basic Conflict Resolution Flow

```
┌──────────────────────┐
│                      │
│ Local Data Modified  │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│                      │
│ Store Locally with   │
│ Timestamp            │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐      No      ┌──────────────────────┐
│                      │─────────────►│                      │
│ Online?              │              │ Queue for Background │
│                      │              │ Sync                 │
└──────────┬───────────┘              └──────────────────────┘
           │ Yes
           ▼
┌──────────────────────┐
│                      │
│ Send to Server       │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐      Yes     ┌──────────────────────┐
│                      │─────────────►│                      │
│ Server Has Newer     │              │ Fetch & Apply Server │
│ Version?             │              │ Version Locally      │
│                      │              │                      │
└──────────┬───────────┘              └──────────────────────┘
           │ No
           ▼
┌──────────────────────┐
│                      │
│ Server Updates with  │
│ Local Version        │
│                      │
└──────────────────────┘
```

## Detailed Conflict Detection Process

```
┌──────────────────────┐
│                      │
│ Sync Process Begins  │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│                      │
│ Get Local Changes    │
│ Since Last Sync      │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐      Empty   ┌──────────────────────┐
│                      │─────────────►│                      │
│ Check Changes Queue  │              │ Update Last Sync     │
│                      │              │ Time & Exit          │
└──────────┬───────────┘              └──────────────────────┘
           │ Has Changes
           ▼
┌──────────────────────┐
│                      │
│ Group by Entity Type │
│ & Sort by Priority   │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐      Error   ┌──────────────────────┐
│                      │─────────────►│                      │
│ Send Changes to      │              │ Add to Retry Queue   │
│ Server               │              │                      │
└──────────┬───────────┘              └──────────────────────┘
           │ Success
           ▼
┌──────────────────────┐      None    ┌──────────────────────┐
│                      │─────────────►│                      │
│ Check Response for   │              │ Update Local Sync    │
│ Conflicts            │              │ Status               │
└──────────┬───────────┘              └──────────────────────┘
           │ Has Conflicts
           ▼
┌──────────────────────┐
│                      │
│ Apply Resolution     │
│ Strategy             │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐      Yes     ┌──────────────────────┐
│                      │─────────────►│                      │
│ Need User Input?     │              │ Show Resolution UI   │
│                      │              │                      │
└──────────┬───────────┘              └──────────────────────┘
           │ No                                │
           │                                   │
           ▼                                   ▼
┌──────────────────────┐              ┌──────────────────────┐
│                      │              │                      │
│ Auto-Resolve Based   │              │ Apply User-Selected  │
│ on Rules             │              │ Resolution           │
│                      │              │                      │
└──────────┬───────────┘              └──────────┬───────────┘
           │                                     │
           └─────────────────┬─────────────────┘
                             │
                             ▼
                   ┌──────────────────────┐
                   │                      │
                   │ Log Resolution &     │
                   │ Update Local Store   │
                   │                      │
                   └──────────────────────┘
```

## Edge Case Resolution Flow

```
┌──────────────────────┐
│                      │
│ Conflict Detected    │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐      Yes     ┌──────────────────────┐
│                      │─────────────►│                      │
│ Is Official Scorer   │              │ Apply Official       │
│ Override?            │              │ Scorer Version       │
│                      │              │                      │
└──────────┬───────────┘              └──────────────────────┘
           │ No
           ▼
┌──────────────────────┐      Yes     ┌──────────────────────┐
│                      │─────────────►│                      │
│ Has Validation       │              │ Prioritize Valid     │
│ Issues?              │              │ Version              │
│                      │              │                      │
└──────────┬───────────┘              └──────────────────────┘
           │ No
           ▼
┌──────────────────────┐      Yes     ┌──────────────────────┐
│                      │─────────────►│                      │
│ Is Tournament Status │              │ Apply Status         │
│ Change?              │              │ Transition Rules     │
│                      │              │                      │
└──────────┬───────────┘              └──────────────────────┘
           │ No
           ▼
┌──────────────────────┐      Yes     ┌──────────────────────┐
│                      │─────────────►│                      │
│ Are Different Fields │              │ Apply Field-Level    │
│ Modified?            │              │ Merging              │
│                      │              │                      │
└──────────┬───────────┘              └──────────────────────┘
           │ No
           ▼
┌──────────────────────┐
│                      │
│ Apply Last-Edit Wins │
│ (Timestamp-Based)    │
│                      │
└──────────────────────┘
```

## Offline Synchronization Flow

```
┌──────────────────────┐
│                      │
│ App Goes Offline     │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│                      │
│ User Makes Changes   │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│                      │
│ Store in IndexedDB   │
│ with Metadata        │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐      Yes     ┌──────────────────────┐
│                      │─────────────►│                      │
│ Is ServiceWorker     │              │ Register Background  │
│ Available?           │              │ Sync                 │
│                      │              │                      │
└──────────┬───────────┘              └──────────────────────┘
           │ No
           ▼
┌──────────────────────┐
│                      │
│ Add to Manual Sync   │
│ Queue                │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐      Yes     ┌──────────────────────┐
│                      │─────────────►│                      │
│ Connection Restored? │              │ Execute Sync Process │
│                      │              │                      │
└──────────────────────┘              └──────────┬───────────┘
                                                 │
                                                 ▼
                                       ┌──────────────────────┐
                                       │                      │
                                       │ Process Sync Results │
                                       │                      │
                                       └──────────┬───────────┘
                                                  │
                                                  ▼
                                       ┌──────────────────────┐      Yes    ┌──────────────────────┐
                                       │                      │─────────────►│                      │
                                       │ Conflicts Detected?  │              │ Apply Conflict       │
                                       │                      │              │ Resolution           │
                                       └──────────┬───────────┘              └──────────────────────┘
                                                  │ No
                                                  ▼
                                       ┌──────────────────────┐
                                       │                      │
                                       │ Update Local State   │
                                       │                      │
                                       └──────────────────────┘
```

## User Notification Decision Tree

```
┌──────────────────────┐
│                      │
│ Conflict Detected    │
│                      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│                      │
│ Analyze Severity     │
│                      │
└──────────┬───────────┘
           │
           ▼
           │
       ┌───┴───────────────────────────┐
       │                               │
       ▼                               ▼
┌──────────────────┐            ┌──────────────────┐
│                  │            │                  │
│ Critical Conflict│            │ Non-Critical     │
│                  │            │ Conflict         │
└──────────┬───────┘            └──────────┬───────┘
           │                               │
           ▼                               ▼
┌──────────────────┐            ┌──────────────────┐      Yes     ┌──────────────────┐
│                  │            │                  │─────────────►│                  │
│ Show Modal       │            │ Affects Current  │              │ Show Toast       │
│ Dialog           │            │ View?            │              │ Notification     │
│                  │            │                  │              │                  │
└──────────┬───────┘            └──────────┬───────┘              └──────────┬───────┘
           │                               │ No                              │
           │                               ▼                                 │
           │                     ┌──────────────────┐                        │
           │                     │                  │                        │
           │                     │ Add to           │                        │
           │                     │ Notification     │                        │
           │                     │ Center           │                        │
           │                     │                  │                        │
           │                     └──────────┬───────┘                        │
           │                                │                                │
           └────────────────────┬───────────┴────────────────────────────────┘
                                │
                                ▼
                      ┌──────────────────────┐
                      │                      │
                      │ Log to Analytics     │
                      │                      │
                      └──────────────────────┘
```

## Data Structure Relationships

```
┌───────────────────────┐         ┌───────────────────────┐
│                       │         │                       │
│  Local Record         │         │  Server Record        │
│  ---------------      │         │  ---------------      │
│  id                   │         │  id                   │
│  data fields          │         │  data fields          │
│  updated_at           │         │  updated_at           │
│  last_modified_by     │         │  last_modified_by     │
│  last_modified_device │         │  last_modified_device │
│  client_version       │         │  server_version       │
│                       │         │                       │
└─────────┬─────────────┘         └─────────┬─────────────┘
          │                                 │
          └─────────────┬───────────────────┘
                        │
                        ▼
              ┌───────────────────────┐
              │                       │
              │  Resolution Record    │
              │  ---------------      │
              │  id                   │
              │  data fields          │
              │  updated_at           │
              │  last_modified_by     │
              │  last_modified_device │
              │  merged_version       │
              │  resolution_type      │
              │  conflict_fields      │
              │                       │
              └───────────────────────┘
```

## Advanced Field-Level Merging Logic

```
┌───────────────────────────────────────────────────────────┐
│ Field-Level Conflict Resolution                           │
└───────────────────────────────────────────────────────────┘
┌───────────────┐  ┌───────────────┐  ┌───────────────┐
│ Local Record  │  │ Field Modified│  │ Server Record │
│ {             │  │ Timestamps    │  │ {             │
│   id: "123",  │  │ {             │  │   id: "123",  │
│   name: "John"│  │   name: 10:15,│  │   name: "John"│
│   score: 5,   │  │   score: 10:20│  │   score: 3,   │
│   notes: "x"  │  │   notes: null │  │   notes: "y"  │
│ }             │  │ }             │  │ }             │
└───────┬───────┘  └───────┬───────┘  └───────┬───────┘
        │                  │                  │
        │                  │                  │
        └──────────────────┼──────────────────┘
                           │
                           ▼
                 ┌───────────────────┐
                 │ Compare Fields    │
                 └─────────┬─────────┘
                           │
         ┌─────────────────┼────────────────┐
         │                 │                │
         ▼                 ▼                ▼
┌────────────────┐ ┌───────────────┐ ┌──────────────┐
│ Local Modified │ │Server Modified│ │Neither Edited│
│ name: SAME     │ │name: SAME     │ │(Keep Either) │
│ score: 10:20   │ │score: 10:05   │ │              │
│ notes: null    │ │notes: 10:10   │ │              │
└────────┬───────┘ └───────┬───────┘ └──────────────┘
         │                 │
         ▼                 ▼
┌────────────────┐ ┌───────────────┐
│ Keep Local:    │ │Keep Server:   │
│ - score: 5     │ │- notes: "y"   │
└────────────────┘ └───────────────┘
                           │
                           ▼
                 ┌───────────────────┐
                 │ Merged Result     │
                 │ {                 │
                 │   id: "123",      │
                 │   name: "John",   │
                 │   score: 5,       │
                 │   notes: "y"      │
                 │ }                 │
                 └───────────────────┘
```

## Version Vector Conflict Detection

```
┌────────────────────────────────────────────────────────────────┐
│ Version Vector-Based Conflict Detection                        │
└────────────────────────────────────────────────────────────────┘
   ┌───────────────────┐              ┌───────────────────┐
   │ Local Version     │              │ Server Version    │
   │ Vector            │              │ Vector            │
   │ ---------------   │              │ ---------------   │
   │ DeviceA: 3        │              │ DeviceA: 2        │
   │ DeviceB: 1        │              │ DeviceB: 2        │
   │ DeviceC: 0        │              │ DeviceC: 1        │
   └─────────┬─────────┘              └─────────┬─────────┘
             │                                  │
             └──────────────┬───────────────────┘
                            │
                            ▼
                  ┌───────────────────┐
                  │ Compare Vectors   │
                  └─────────┬─────────┘
                            │
              ┌─────────────┴──────────────┐
              │                            │
              ▼                            ▼
     ┌────────────────┐           ┌────────────────┐
     │ DeviceA: 3 > 2 │           │ DeviceB: 1 < 2 │
     │ LOCAL_HIGHER   │           │ SERVER_HIGHER  │
     └────────┬───────┘           └────────┬───────┘
              │                            │
              └─────────────┬──────────────┘
                            │
                            ▼
                 ┌─────────────────────┐
                 │ Vectors Incomparable│
                 │ (CONCURRENT EDITS)  │
                 └─────────┬───────────┘
                           │
                           ▼
                 ┌───────────────────┐
                 │ Need Field-Level  │
                 │ Conflict Resolution│
                 └───────────────────┘
```

These diagrams provide visual representations of the conflict resolution strategies implemented in the Golf Tournament Organizer application, from basic "last edit wins" resolution to sophisticated field-level merging and version vector conflict detection.
