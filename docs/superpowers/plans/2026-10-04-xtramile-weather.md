# Xtramile Weather Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the Xtramile assessment with weather selection, persistent notes, React TypeScript, Docker, and offline tests.

**Architecture:** Keep the .NET 10 web project with Domain, Application, Infrastructure, and API folders. MediatR separates query and command handlers; application interfaces isolate EF Core and OpenWeatherMap. Build React independently in `frontend/` and serve its production assets from ASP.NET Core.

**Tech Stack:** .NET 10, MediatR, FluentValidation, EF Core SQLite/In-Memory, xUnit, React, TypeScript, Vite, Vitest, React Testing Library, Playwright, Docker Compose, GitHub Actions. Resolve compatible package releases at setup and commit exact package versions and the npm lockfile.

**Spec:** `docs/superpowers/specs/2026-10-04-xtramile-weather-design.md`

## Global Constraints

- React TypeScript source belongs in root-level `frontend/`; use strict TypeScript configuration.
- Domain has zero external framework dependencies. Application must not reference concrete HTTP clients or DbContext types.
- API routing sends requests through MediatR. Reads never mutate state; EF Core reads explicitly use `AsNoTracking()`.
- Automated tests must run offline with zero live network calls after setup installs dependencies and browser binaries.
- SQLite is runtime storage; command tests use isolated EF Core In-Memory stores, and relational/API persistence tests use isolated SQLite.
- Never commit credentials or display simulated readings as live weather.
- Keep the required routes, both temperature units, all weather metrics, and UTC timestamps.
- Docker preserves SQLite through a named volume and supplies credentials only at runtime.
- Cover assessment sections 4, 5, and 6 with tests, root AI instructions, README, and repository-ready files.

## Review Focus

- Switching country/city during pending requests must not render stale weather or save a note for an old selection: Task 4.
- Malformed, missing, or impossible upstream metrics must produce a controlled upstream failure: Task 2.
- Zero humidity must not emit NaN/Infinity for calculated dew point: Task 2.
- Replacing an application container must preserve notes; SQLite paths must remain writable: Task 5.
- Unknown `/api` routes must return JSON errors rather than the SPA page: Task 3.

## Shared contracts

Use camelCase JSON and these C# contracts in Application:

- `CountryDto(string Code, string Name)` and `CityDto(string Name, string CountryCode)`.
- `WeatherDto(string City, string CountryCode, DateTimeOffset ObservedAtUtc, double WindSpeedMetersPerSecond, int WindDirectionDegrees, int VisibilityMeters, double PressureHpa, string SkyConditions, double TemperatureFahrenheit, double TemperatureCelsius, double? DewPointCelsius, bool DewPointIsCalculated, int RelativeHumidityPercent)`.
- `CreatedNoteDto(Guid Id, DateTimeOffset CreatedAtUtc)`.
- `ILocationCatalog.GetCountries()` returns `IReadOnlyList<CountryDto>`; `GetCities(string countryCode)` returns `IReadOnlyList<CityDto>`; `Contains(string countryCode, string cityName)` returns `bool`. Compare country codes and city names case-insensitively and return canonical names.
- `IWeatherService.GetCurrentAsync(string cityName, string countryCode, CancellationToken cancellationToken)` returns `Task<WeatherDto>`.
- `IWeatherNoteRepository.AddAsync(WeatherNote note, CancellationToken cancellationToken)` returns `Task`; it saves changes before completion.
- `GetCountriesQuery : IRequest<IReadOnlyList<CountryDto>>`, `GetCitiesQuery(string CountryCode) : IRequest<IReadOnlyList<CityDto>>`, `GetWeatherQuery(string CityName, string CountryCode) : IRequest<WeatherDto>`, and `CreateWeatherNoteCommand(string CityName, string CountryCode, string Text) : IRequest<CreatedNoteDto>`.
- Note text is trimmed, required, and at most 2,000 characters. Country code is required and two letters. City is required and at most 100 characters. Unknown catalog locations return 404.
- GET weather requires `countryCode`; POST body contains `cityName`, `countryCode`, and `text`. Success returns 201 with `CreatedNoteDto`; do not advertise a resource URL without a corresponding GET route.

### Task 1: Domain and CQRS location queries

**Files:** Create `Domain/Temperature.cs`, `Domain/WeatherNote.cs`, `Application/Contracts/*.cs`, `Application/Abstractions/*.cs`, `Application/Countries/*.cs`, `Application/Cities/*.cs`, `Application/Common/*.cs`, and `Infrastructure/Locations/BundledLocationCatalog.cs` under `src/WeatherApp/`. Create backend `DomainTests.cs`, `LocationQueryTests.cs`, and `ValidationTests.cs`. Modify web/test project references, add `.gitignore` and root `AGENTS.md`, and remove `UnitTest1.cs`.

**Interfaces:** Produces temperature conversion `Temperature.ToCelsius(double fahrenheit)`, domain notes, catalog contracts, query DTOs/handlers, and `ValidationBehavior<TRequest,TResponse>`.

- [x] Write domain theories asserting `ToCelsius(32)==0`, `ToCelsius(212)==100`, `ToCelsius(-40)==-40`, `ToCelsius(-4)==-20`, and `ToCelsius(98.6)` approximately 37. Test note invariants and trimming. Write isolated location-handler tests against fake catalog results and invalid/unknown country handling.
- [x] Run `dotnet test WeatherApp.slnx --filter "FullyQualifiedName~DomainTests|FullyQualifiedName~LocationQueryTests|FullyQualifiedName~ValidationTests"`. Expected: missing domain/handler types or failing assertions before implementation.
- [x] Implement contracts and handlers, validation pipeline, and deterministic catalog specified by the spec. Add MediatR/FluentValidation references and register no product HTTP behavior yet. `AGENTS.md` records boundaries, CQRS conventions, naming, security rules, and verification commands from this plan.
- [x] Run the same filtered tests. Expected: all pass, with no live network calls.
- [x] Commit only this task's source/tests/configuration and ledger result: `feat: add domain and location CQRS queries`.

### Task 2: Current weather query and provider adapter

**Files:** Create `Application/Weather/*.cs`, `Application/Common/WeatherServiceException.cs`, `Infrastructure/Weather/OpenWeatherMapService.cs`, `Infrastructure/Weather/OpenWeatherMapOptions.cs`, and backend `WeatherQueryTests.cs`, `WeatherAdapterTests.cs`.

**Interfaces:** Consumes catalog, DTOs, validators, temperature conversion. Produces weather query handler and `IWeatherService` implementation using a typed HttpClient.

- [x] Write handler tests using fake IWeatherService: canonical selected location reaches the provider, missing selection returns 404, dependency errors propagate, and cancellation reaches the dependency. Adapter tests use a fake HttpMessageHandler and synthetic JSON only. Assert 68 F -> 20 C, observation time from Unix seconds with zero UTC offset, country/city mapping, mph -> m/s, and every payload metric.
- [x] Add adapter cases for missing temperature, invalid humidity outside 0..100, malformed JSON, provider 404/401/429/500, timeout, and cancellation. At 20 C and 50% humidity calculated dew point is approximately 9.26 C; at 0% humidity it is null and never NaN/Infinity.
- [x] Run `dotnet test WeatherApp.slnx --filter "FullyQualifiedName~WeatherQueryTests|FullyQualifiedName~WeatherAdapterTests"`. Expected: failure before implementation.
- [x] Implement OpenWeatherMap current-weather requests over HTTPS with imperial units, safely encoded city/country, runtime configuration, 10-second timeout, and validated payload mapping. Use Magnus dew-point calculation with constants 17.62 and 243.12; preserve request cancellation. Provider/configuration errors must not expose secrets. The API layer later translates errors into 503 for unavailable configuration, 504 for timeout, and 502 for upstream failures.
- [x] Run the filtered tests. Expected: all pass offline.
- [x] Commit: `feat: add validated weather provider and query`.

### Task 3: Note persistence and API integration

**Files:** Create `Application/Notes/*.cs`, `Infrastructure/Persistence/WeatherDbContext.cs`, `Infrastructure/Persistence/WeatherNoteRepository.cs`, `Infrastructure/DependencyInjection.cs`, `API/Endpoints.cs`, `API/ApiExceptionHandler.cs`, and backend `NoteCommandTests.cs`, `ApiTests.cs`, `WeatherApiFactory.cs`. Modify `Program.cs`, configuration, and project packages.

**Interfaces:** Consumes all prior CQRS contracts. Produces POST note command handler, durable SQLite repository, required GET/POST routes, Problem Details, and `public partial class Program` for WebApplicationFactory.

- [x] Write command tests with isolated EF In-Memory stores: valid Jakarta/ID note gets a nonempty Guid and UTC timestamp; a fresh context finds the trimmed text and canonical location. Blank/overlong notes and unknown cities produce no insert. Use TimeProvider injection for deterministic timestamps.
- [x] Write API tests substituting fake weather and isolated SQLite: all required GETs return expected DTOs; invalid input returns 400, unknown catalog location 404, provider failure 502, missing configuration 503, timeout 504, POST returns 201 and a persisted identifier. Verify `/api/missing` is JSON 404 and never SPA content. Cover `/api/weather/Jakarta` without countryCode and malformed POST JSON.
- [x] Run `dotnet test WeatherApp.slnx --filter "FullyQualifiedName~NoteCommandTests|FullyQualifiedName~ApiTests"`. Expected: failure before implementation.
- [x] Implement thin routes through ISender, validation/error translation, scoped persistence, startup initialization, and cancellation. Initialize the bounded SQLite schema with EnsureCreated; document that future schema evolution needs a migration strategy. Default DB file is `data/weather.db`; create its directory. Static frontend hosting must preserve API errors. Explicitly use AsNoTracking in persistence read paths.
- [x] Run filtered tests and `dotnet test WeatherApp.slnx -c Release`. Expected: all backend tests pass offline.
- [x] Commit: `feat: persist notes and expose weather API`.

### Task 4: React TypeScript client

**Files:** Create `frontend/package.json`, lockfile, TypeScript/Vite/ESLint/Vitest configuration, `index.html`, `src/main.tsx`, `src/App.tsx`, `src/api/client.ts`, `src/api/types.ts`, `src/components/LocationSelector.tsx`, `WeatherDetails.tsx`, `NoteForm.tsx`, CSS, test setup, component tests, Playwright configuration, and `e2e/weather.spec.ts`.

**Interfaces:** Consumes Task 3 camelCase HTTP contracts; typed frontend interfaces mirror shared DTOs. Vite proxies `/api` to `http://localhost:5000`. Scripts: dev, lint, test, build (type check then Vite), and test:e2e.

- [x] Establish the toolchain and write React tests with mocked fetch: load/select country and city, render every weather metric, save note with exact POST shape, show returned identifier, render API failures, reset selections, and suppress stale requests and duplicate saves. Test changing location during note saving so old feedback does not appear for the new location.
- [x] Run `npm run test -- --run` from frontend. Expected: missing/unimplemented components cause failure.
- [x] Implement accessible responsive components, API parsing, AbortController/request identity handling, disabled states, and honest missing-weather/error display. Keep API types explicit and no secret in the frontend. Show null dew point as unavailable and mark calculated values.
- [x] Add Playwright browser workflow tests with route-intercepted API payloads for success, provider failure, and invalid note feedback. Start Vite through Playwright webServer; no backend/provider access is needed.
- [x] Run frontend lint, unit tests, build, and browser tests. Expected: all pass; TypeScript build succeeds; e2e makes no external calls.
- [x] Commit: `feat: add React TypeScript weather client`.

### Task 5: Docker, CI, documentation, and final verification

**Files:** Create `Dockerfile`, `.dockerignore`, `compose.yaml`, `.env.example`, `.github/workflows/ci.yml`, `README.md`, and `scripts/verify.ps1`. Update ignore rules and AGENTS.md only as needed to match final commands.

**Interfaces:** Consumes published .NET app and frontend/dist. Produces one runtime container on port 8080, frontend at `/`, SQLite at `/app/data/weather.db`, and named volume `weather-data` mounted at `/app/data`. Compose maps host 8080 to container 8080 and supplies OpenWeatherMap__ApiKey from the environment.

- [x] Implement a multi-stage Docker build using Node 22 and .NET 10 SDK/runtime images, copying frontend/dist to published wwwroot. Run under the image's nonroot app user with a writable DB directory. Never bake credentials into images.
- [x] Write README sections for assessment 4/5/6, Onion/CQRS diagrams and traces, exact setup/build/test/run commands, API payloads, catalog coverage, units/calculated dew point, persistence/EnsureCreated limitations, runtime configuration, Docker, and AGENTS.md.
- [x] Implement GitHub Actions with dependency setup, .NET Release build/tests, npm ci, lint/unit/build/browser checks, and Chromium installation. Implement scripts/verify.ps1 to run the same local checks and stop on failures. CI does not require a weather key.
- [x] Run `dotnet build WeatherApp.slnx -c Release` and `dotnet test WeatherApp.slnx -c Release`; run frontend `npm run lint`, `npm run test -- --run`, `npm run build`, `npm run test:e2e`. Expected: every check passes.
- [ ] Run `docker compose config`, `docker compose up --build -d`, and a local smoke check: `/` serves React; `/api/countries` returns the catalog; create a note, replace the container without deleting the named volume, and verify the row persists using EF/SQLite inspection. Without a key weather returns the documented configuration error. Expected: build/startup and persistence succeed. Report Docker daemon unavailability if the environment prevents this check; do not claim it passed.
- [x] Commit: `build: add Docker delivery CI and assessment documentation`.
- [x] Perform one fresh whole-change code review with Superpowers requesting-code-review, address important findings with regression tests, and rerun affected checks. Record evidence in the progress ledger.
- [x] Leave the implementation on `feature/xtramile-weather` for user review. No merge or GitHub publication is authorized by this plan. Obtain the target repository when publishing is requested.

## Execution handoff

Recommended method: native execution using Superpowers executing-plans, with one fresh reviewer at the end. Tasks have sequential shared contracts, so inline implementation avoids repetitive context setup. Review this saved plan before product implementation; retain it and the execution ledger as the recoverable record.
