# Background multi-city weather refresh

User approved the channel recommendation on 2026-10-12. Purpose: demonstrate useful bounded parallel external API work in the existing weather app, with observable progress and restart recovery.

## Contract and architecture

POST `/api/weather/refresh-jobs` accepts `{ cities: [{ cityName, countryCode }] }`, one to thirteen distinct supported locations, validates through MediatR, canonicalizes catalog names, and saves all work before returning 202 `{ id }` and a Location header. GET `/api/weather/refresh-jobs/{id}` reads an untracked persisted snapshot: id, createdAtUtc, status, and per-city status/attempts/weather/error. Unknown IDs return 404. Queries never write.

Domain owns per-city state transitions. Application owns commands, queries, DTOs and repository interfaces. Infrastructure owns EF Core, a singleton bounded Channel<Guid> of per-city work IDs, dispatcher and BackgroundService workers. Existing single-city API remains direct. React can refresh all cities in the selected country and poll job progress; a localStorage job ID lets a reload resume observation. Leaving the page cancels polling, not accepted jobs.

## Concurrency and recovery

Default channel capacity 20, workers 2, max attempts 3, dispatcher interval 1 second, global minimum provider start interval 1 second and exponential retry base 2 seconds; all configurable with validated limits. Channel full mode Wait. Queue IDs deduplicated while queued or executing. Each work item has its own DI scope/DbContext. Atomic pending-to-running claim guards duplicate deliveries. SQLite stores work before delivery; dispatcher continually discovers due pending items. On startup, reset interrupted Running items to Pending; Completed and Failed items are retained. Attempts count each actual provider attempt, including interrupted attempts. Exhausted interrupted work becomes Failed. This design supports one app instance per database, not distributed consumers.

Successful weather snapshots and observation times are persisted; failed cities expose safe errors, no invented readings. Timeout and explicitly transient provider errors retry; missing configuration, permanent HTTP errors, and invalid responses fail immediately. Shutdown cancellation leaves work recoverable. Backoff dates are stored as Unix milliseconds for SQLite comparisons. In-memory channel is a delivery optimization, not durable storage. Accepted jobs are at-least-once; a restart may repeat a read-only provider call.

## Storage upgrade and operations

Existing Notes must survive upgrade. Startup EnsureCreated handles a new database; an idempotent CREATE TABLE IF NOT EXISTS adds RefreshJobs/RefreshItems to existing SQLite files. No destructive migration or automatic cleanup. Persisted jobs/results accumulate; retention remains a documented future operational decision. Worker infrastructure faults are logged without provider URIs and retried by dispatcher; the host must not silently strand Running work.

## Verification

Offline API tests: validation, persistence before acknowledgement, unknown job, complete multi-city snapshots, permanent/transient errors. SQLite file-based integration tests: pre-existing Notes schema upgrade, restart recovery, read-only status, actual two-call concurrency, queue backpressure/deduplication, shutdown. React tests: submission, status and partial failure, restored job and polling errors. Playwright: mocked multi-city UI workflow, no live provider calls. Final scripts/verify.ps1 plus Docker build/start checks where available. No merge or publication.
