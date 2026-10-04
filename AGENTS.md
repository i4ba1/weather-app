# Weather app engineering instructions

Use the approved design and plan under docs/superpowers/. Keep backend source in src/WeatherApp/ and React TypeScript source in frontend/.

## Architecture and CQRS

- Domain: framework-free entities and rules; no EF Core, MediatR, HTTP, or UI dependencies.
- Application: commands, queries, handlers, DTOs, interfaces, FluentValidation validators and MediatR validation behavior. Depends on Domain, never concrete Infrastructure or API types.
- Infrastructure: EF Core, SQLite, location catalog and HTTP clients implementing Application interfaces.
- API: thin routes dispatching through MediatR; exception translation and dependency registration at the composition root.
- Queries do not write. Use AsNoTracking explicitly for EF Core reads. Commands validate first and await SaveChangesAsync before reporting persistence.
- Inject IWeatherService, ILocationCatalog and IWeatherNoteRepository; do not call OpenWeatherMap or DbContext from endpoints or query handlers.

## Conventions

Use PascalCase C# types; Query, Command, Handler, Validator, and Dto suffixes; I-prefixed interfaces. Organize Application by feature. Use PascalCase .tsx React components and .ts utility modules, strict TypeScript, typed API contracts, and accessible labels.

## Verification

Use Superpowers executing-plans and test-driven-development, record progress in the plan ledger, and verify before completion. All automated tests must use fake HTTP or isolated databases and make no live external network calls. Never commit secrets or represent synthetic weather as live readings.

Run dotnet build WeatherApp.slnx -c Release and dotnet test WeatherApp.slnx -c Release. In frontend/, run npm run lint, npm run test -- --run, npm run build, and npm run test:e2e. The final scripts/verify.ps1 runs these checks. Docker delivery uses docker compose up --build and a persistent SQLite volume. Do not merge or publish without authorization.
