# Weather refresh implementation plan

> **For agentic workers:** Use superpowers:executing-plans task by task. User authorized implementation of the proposed channel workflow; execute inline in the existing workspace, preserving uncommitted user changes.

**Goal:** Persist, queue and process multi-city refresh jobs with parallel workers and restart recovery.

**Architecture:** MediatR commands persist per-city work; queries read snapshots. Infrastructure dispatches persisted pending IDs through a bounded Channel to scoped workers. React submits and polls.

**Tech Stack:** Existing .NET 10, EF Core SQLite, MediatR, FluentValidation, React TypeScript, xUnit/Vitest/Playwright; no new product packages.

**Spec:** ../specs/2026-10-12-weather-refresh-design.md

## Global constraints

- Preserve existing Notes and user Program.cs commentary.
- Two workers, capacity 20, three attempts; configurable global provider pacing.
- Offline tests only; one app instance per SQLite database; no publication.

## Review focus

- Existing database upgrade preserves notes.
- Shutdown and restart recover interrupted attempts without resetting completed snapshots.
- Queue saturation does not lose persisted work; duplicate IDs cannot cause duplicate concurrent execution.
- Partial provider failures do not prevent other cities completing.
- Browser reload resumes status and selection changes cannot overwrite unrelated weather.

## Task 1: Durable API and domain

- [x] Write API tests for 202 persistence, input rejection and unknown ID; run RED.
- [x] Add Domain/WeatherRefreshItem.cs; Application/RefreshJobs contracts, validators and handlers; IWeatherRefreshRepository.
- [x] Add EF repository and schema initializer; routes; run GREEN including existing tests.

## Task 2: Queue, workers and recovery

- [x] Write file-backed tests for parallel weather calls, retries, recovery, preservation and bounded queue; run RED.
- [x] Add Infrastructure/RefreshJobs queue/options/hosted service and provider transient classification.
- [x] Use separate scopes, atomic claims, persisted retry times, startup recovery and provider pacing; run GREEN.

## Task 3: React and delivery

- [x] Write frontend and Playwright refresh workflow tests; run RED.
- [x] Add typed API calls and WeatherRefreshPanel; submit selected-country cities, poll/resume, render safe results/errors.
- [x] Update README, Postman and architecture docs; run scripts/verify.ps1; review changes and address findings.

## Progress ledger

- 2026-10-12: approved recommendation recorded; current workspace retained to preserve existing user edits. Read-only exploration found EnsureCreated schema limitation. No product code written before API RED.

- Final verification: scripts/verify.ps1 passed: Release build (0 warnings/errors), 68 backend tests, lint, 9 React tests, strict TypeScript/Vite build and 4 Playwright tests. All automated API/provider tests use fake services or fake HTTP and isolated SQLite files. Official Postman schema validates 12 requests/13 scripts and two environments.
- Independent review found pacing-before-claim defect. Delayed-claim regression failed on old code; gate now covers claim and actual provider-task start, with response awaited outside gate. Worker regression suite passed; follow-up reviewer reports no blockers.
- Docker compose up --build -d succeeded after final code fix. Final smoke: React/catalog HTTP 200, empty refresh batch HTTP 400. No live provider calls or production job writes used for verification. Existing notes volume retained. Changes remain uncommitted; no merge/publication.

- Headed recording follow-up: real Kestrel API and production provider adapter with fake outbound HTTP, isolated per-run SQLite, React on dedicated port. npm run test:e2e:channels passed (1 headed test, 23.1 seconds). Verified one note and three-city persisted job independently. Converted 20.88-second MP4 and 2,846,208-byte GIF, embedded docs/media/weather-refresh-workflow.gif in README with fixture/reproduction instructions. Full verify.ps1 passed again: 68 backend, 9 React, 4 default Playwright, lint/build. No live external calls, commit or publication.

- Final presentation rerun supersedes the first export: headed test passed in 23.0 seconds with an explicit failed-city viewport assertion. SQLite verification passed again. Final MP4 is 21.16 seconds / 711,132 bytes; embedded GIF is 3,332,084 bytes and fully decodes. Inspected an actual recorded frame: all three outcomes and retry counts visible. Lint/strict build passed after scrolling-only test adjustment. No commit.

- Release authorized 2026-10-13: user requested commit and push to existing origin/main. Fresh verify.ps1 passed (68 backend, 9 React, 4 Playwright, lint/build); Postman schema/scripts and embedded GIF validation passed. Exact staged files passed credential/artifact checks. No API key, database, MP4, raw trace or build output is included. Existing user Program.cs comments retained.
