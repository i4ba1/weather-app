# Xtramile weather app — recovery design

Status: Approved in conversation on 2026-10-04 after the React TypeScript, Docker, and SQLite discussion. This document is the recovered design used for implementation.

Confirmed user requirement: use React TypeScript in a separate root-level `frontend/` directory. Explicitly cover assessment sections 4, 5, and 6, CQRS decoupling, and the automated implementation and verification tools.

## Purpose and success criteria

Deliver the Xtramile practical assessment: a production-oriented ASP.NET Core API and lightweight browser client that let a user select a country and city, inspect current weather, and save a weather note. Success means the PDF requirements are implemented, documented, and verified by automated tests that make no live network calls.

## Requirements from the assessment

- Use folders for Onion Architecture: Domain, Application, Infrastructure, and API.
- Keep Domain free of external framework dependencies.
- Implement commands and queries with MediatR, DTOs, interfaces, and FluentValidation in Application. API endpoints dispatch through MediatR.
- Expose GET /api/countries, GET /api/countries/{countryCode}/cities, GET /api/weather/{cityName}, and a POST endpoint for notes or favorites.
- Persist writes using EF Core with SQLite or the In-Memory provider. Apply AsNoTracking explicitly to EF Core read queries.
- Hide OpenWeatherMap behind IWeatherService. Convert upstream Fahrenheit temperatures to Celsius in Domain or Application.
- Return city, country, UTC time, wind speed and direction, visibility, pressure, sky conditions, Fahrenheit and Celsius temperatures, dew point, and relative humidity.
- Provide country and city dropdowns, weather rendering, and feedback for the POST action.
- Test temperature edge cases, isolated query handlers, command persistence, and API integration entirely offline.
- Add root AI instructions and a README describing architecture, setup, execution, persistence, and design decisions. Prepare the code for a GitHub repository.

## Proposed implementation choices

The following choices define the implementation scope.

### Structure and storage

Retain the existing .NET 10 web project and xUnit test project. Use feature folders within Application for countries, cities, weather, and notes. Domain owns temperature conversion and note rules; Infrastructure owns HTTP integration, a location catalog, and EF Core persistence. API owns routing, exception translation, and dependency registration.

Use SQLite for normal execution so notes survive restarts. Use an isolated EF Core In-Memory database for command tests and isolated persistence for API tests. Store city, country code, note text, identifier, and UTC creation time. Select notes rather than favorites; return the created identifier from POST /api/weather/notes.

### CQRS decoupling and layer boundaries

The read path is `HTTP GET -> MediatR query -> query handler -> application interface -> infrastructure implementation -> response DTO`. Countries, cities, and weather each have a query and handler. Query handlers never mutate state; every EF Core read uses `AsNoTracking()` explicitly.

The write path is `HTTP POST -> MediatR command -> validation behavior -> command handler -> persistence interface -> EF Core -> created resource identifier`. Saving a weather note uses a separate command and handler. Validation failures prevent the persistence call; successful writes call `SaveChangesAsync` before returning the identifier.

API endpoints depend on MediatR and request/response contracts; they contain no weather-provider calls or database logic. Application depends on Domain and its own interfaces, not concrete HTTP clients or DbContext types. Infrastructure implements the interfaces and is wired through dependency injection at the composition root. Domain depends on neither Application nor Infrastructure. Tests can replace weather and persistence dependencies without changing handlers or routes.

CQRS here separates request types and execution responsibilities while allowing one SQLite database. It does not require separate read/write databases, event sourcing, or a message broker. README.md will explain these boundaries and trace one weather query and one note command through them.

### Location selection and weather

Use a documented bundled country/city catalog so the selection workflow is deterministic and available without another service key. Country and city endpoints return all entries available in that catalog; the catalog is not a claim of exhaustive worldwide city coverage. Initial coverage is Indonesia (Jakarta, Bandung, Surabaya), Australia (Sydney, Melbourne, Brisbane), Singapore (Singapore), Japan (Tokyo, Osaka, Kyoto), and United Kingdom (London, Manchester, Birmingham). Document this bounded catalog and how to extend it.

Keep the required weather route and accept a countryCode query parameter to distinguish cities with identical names. Validate the selection against the catalog. Request OpenWeatherMap data in imperial units and translate it into an explicitly documented response with UTC observation time and units. Calculate dew point from Celsius temperature and relative humidity when the upstream current-weather payload lacks it; label this value as calculated.

Configure the API key through environment variables or development secrets. Do not commit credentials. Missing configuration must produce a clear weather-service error; the client must never present simulated readings as live weather.

### UI and errors

Build a React TypeScript application with Vite in root-level `frontend/`, separate from `src/WeatherApp/`. Give it its own package.json, lockfile, strict TypeScript configuration, source files, test configuration, and build output. Use focused components for location selection, weather metrics, and the note form, with an API client module handling requests and Problem Details responses. Define typed API DTOs and component props; run TypeScript type checking as part of the frontend build.

Provide accessible labeled dropdowns, loading and error messages, a weather metric panel, and a note form. Reset city and weather state when the country changes. Ignore stale responses when selections change quickly. Disable note submission until a valid location is selected and prevent duplicate submissions while a save is pending.

During development, use the Vite `/api` proxy to reach ASP.NET Core. For the documented production flow, serve the compiled React assets from ASP.NET Core and reserve `/api` for the API; unknown API routes must return API errors rather than the SPA document. Keep frontend source and tooling in `frontend/` even when build output is copied for hosting.

Use a MediatR validation behavior and consistent Problem Details responses. Distinguish invalid input, unknown locations, upstream failures, and unexpected server errors. Propagate request cancellation and apply a bounded upstream timeout. Avoid exposing upstream credentials or internal exception details.

## Assessment section 4: automated testing

Replace the placeholder test with meaningful coverage:

- Domain tests with xUnit: 32 F -> 0 C, 212 F -> 100 C, -40 F -> -40 C, and additional negative/fractional values with explicit tolerances.
- Isolated CQRS query-handler tests: use mocked or fake application interfaces to verify location results, weather mapping, missing locations, provider errors, and cancellation without HTTP or a live database.
- Command-handler tests: use an isolated EF Core In-Memory database, execute a valid note command, then read from a fresh DbContext against the same test database to verify the identifier and persisted fields. Verify invalid commands create no records.
- API integration tests: use ASP.NET Core WebApplicationFactory with fake weather dependencies and isolated persistence. Exercise country/city/weather GET routes and the note POST route, payload fields, validation errors, unknown locations, provider failures, and created resource identifiers.
- HTTP-adapter tests: use a fake HttpMessageHandler and synthetic provider payloads to cover parsing, Fahrenheit/Celsius conversion, calculated dew point, malformed responses, provider status failures, and timeout behavior.
- React tests: use Vitest and React Testing Library with mocked fetch to verify country/city loading, metric rendering, selection resets, stale response handling, note-save feedback, and error states.
- Browser tests: use Playwright with intercepted API responses to verify the complete selection/weather/note workflow and errors. No automated test may call live OpenWeatherMap or any other external service.

Automated acceptance commands will include `dotnet build WeatherApp.slnx -c Release`, `dotnet test WeatherApp.slnx -c Release`, and, from `frontend/`, `npm run lint`, `npm run test -- --run`, `npm run build`, and `npm run test:e2e`. Dependency installation is a setup step; after dependencies and browser binaries are installed, test execution must work without external network access.

## Assessment section 5: developer AI tooling and context configuration

Add a root-level `AGENTS.md` as the project AI instructions file. It must define folder responsibilities, Domain's framework independence, allowed dependency directions, application-interface ownership, thin API routing through MediatR, commands versus queries, FluentValidation placement, `AsNoTracking()` for reads, persistence rules, and offline-test requirements.

Document naming conventions: PascalCase C# types; `*Query`, `*Command`, `*Handler`, `*Validator`, and `*Dto` suffixes; `I*` application interfaces; React components in PascalCase `.tsx` files and non-component TypeScript modules in `.ts` files; and feature-based organization. Include the exact build/test commands and rules against committing credentials or presenting fake readings as live weather.

Use Superpowers for execution: write an implementation plan with exact interfaces and acceptance tests, implement tasks with the executing-plans workflow and TDD, keep a progress ledger, run verification commands, and perform a final code review. Codex edits the backend, frontend, tests, documentation, and CI configuration using filesystem and shell tools. These tools run during implementation; a planning document alone is not the completed deliverable. Execution has not started yet.

## Assessment section 6: documentation and deliverables

Deliver the backend under `src/WeatherApp/`, React source under `frontend/`, backend tests under `tests/WeatherApp.Tests/`, frontend and browser tests under `frontend/`, root `AGENTS.md`, root `README.md`, and automated CI configuration under `.github/workflows/`.

README.md must include:

- An Onion Architecture overview and dependency diagram, plus the CQRS query and command traces described above and how interfaces decouple handlers from infrastructure.
- Required .NET and Node tooling; backend/frontend dependency installation; API-key setup; database configuration; development startup; production frontend build and hosting; and all automated test commands.
- EF Core provider choices, SQLite file location and initialization, note state persistence across restarts, isolated test databases, and `AsNoTracking()` reads.
- API routes and example request/response payloads, returned weather units, location-catalog coverage, calculated dew point, validation and upstream error behavior.
- Design decisions and limitations, AI instructions in `AGENTS.md`, the Superpowers plan/verification workflow, and how to run the same checks locally and in CI.

Add ignore rules for .NET and frontend build outputs, dependencies, browser artifacts, local database files, and secrets. Add a GitHub Actions workflow to restore/install dependencies, build both applications, and execute backend, React, and browser tests with fake providers and no weather API key. CI setup may download dependencies; test execution itself makes no live external calls.

The deliverable includes a GitHub repository containing backend, frontend, tests, and documentation. Prepare all files locally. Publishing requires the target repository and authorization; record the repository URL and verify the uploaded contents once publishing is authorized.

## Alternatives and trade-offs

### Docker recommendation

Propose Docker as a reproducible delivery option while retaining native development commands. Use a multi-stage Dockerfile to build the React TypeScript frontend and publish the .NET application, then run the backend serving the compiled frontend in one runtime container. Add compose.yaml with a named volume mounted at the SQLite data directory so notes survive container replacement. Supply the weather API key at runtime rather than embedding it in the image. Document `docker compose up --build`, configuration, persistence, and a container smoke check. Keep frontend source in `frontend/`; a separate source directory does not require a separate production container.

### Database recommendation

Retain SQLite for this assessment: the PDF explicitly permits EF Core In-Memory or SQLite, and the required persistent state is a small weather-note store. SQL Server adds a database service and operating requirements beyond that stated scope. Consider SQL Server for a later deployment requiring multiple application replicas, sustained concurrent writes, or SQL Server-specific capabilities. Such a migration requires provider-specific migrations and database integration tests; application interfaces reduce coupling but do not make database-provider behavior identical.

- SQLite plus React is the proposed approach: durable notes with the user's required separate frontend.
- EF Core In-Memory plus React reduces database setup but loses notes on restart.
- A separate frontend deployment remains possible, but same-origin hosting of the React build keeps the documented production setup smaller.

## Recovery checkpoint

The recovered design is approved. The next artifact is the implementation plan with exact interfaces, test cases, and execution steps. Use SQLite, weather notes, the documented initial catalog, calculated dew point, React TypeScript in `frontend/`, and Docker Compose.
