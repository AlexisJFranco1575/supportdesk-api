# SupportDesk API

A support-ticket API built with **.NET 10**, **Clean Architecture**, **EF Core** and **SQL Server**.
It models a real ticket workflow (Open → In progress → Resolved → Closed, with reopening)
and keeps the business rules in a framework-free domain layer covered by unit tests.

## Status

Work in progress.

- [x] Milestone 1: domain model, unit tests, API skeleton, CI
- [x] Milestone 2: application layer, EF Core + SQL Server, migrations, ticket endpoints
- [ ] Milestone 3: integration tests (Testcontainers), JWT authentication, input validation, DB health check
- [ ] Milestone 4: Docker image, full CI, cloud deployment

## Architecture

```
src/
  SupportDesk.Domain/          Entities and business rules (no dependencies)
  SupportDesk.Application/     Use cases and ports (interfaces)
  SupportDesk.Infrastructure/  EF Core, SQL Server, repositories, migrations
  SupportDesk.Api/             ASP.NET Core minimal API, OpenAPI + Scalar docs
tests/
  SupportDesk.Domain.Tests/        Rules of the ticket workflow
  SupportDesk.Application.Tests/   Use cases against an in-memory repository
```

Dependencies point inwards: `Api → Infrastructure → Application → Domain`.

## Endpoints

| Method | Route | Description |
| --- | --- | --- |
| POST | `/api/tickets` | Create a ticket |
| GET | `/api/tickets` | List tickets (`status`, `priority`, `page`, `pageSize`) |
| GET | `/api/tickets/{id}` | Get a ticket |
| POST | `/api/tickets/{id}/start` | Assign and start working |
| POST | `/api/tickets/{id}/resolve` | Mark as resolved |
| POST | `/api/tickets/{id}/close` | Close a resolved ticket |
| POST | `/api/tickets/{id}/reopen` | Reopen a resolved or closed ticket |
| PATCH | `/api/tickets/{id}/priority` | Change priority |
| GET | `/health/live` | Liveness probe |

Errors follow RFC 9457 problem details: `404` for unknown tickets, `422` when a business rule is
violated (for example resolving a ticket that is not in progress).

## Run locally

Requirements: .NET 10 SDK and Docker.

```bash
# 1. Database
cp .env.example .env            # then set your own password
docker compose up -d

# 2. Connection string (kept out of the repo with user-secrets)
dotnet user-secrets init --project src/SupportDesk.Api
dotnet user-secrets set "ConnectionStrings:SupportDesk" "Server=localhost,1433;Database=SupportDesk;User Id=sa;Password=<your password>;TrustServerCertificate=True" --project src/SupportDesk.Api

# 3. Schema
dotnet tool restore
dotnet ef database update --project src/SupportDesk.Infrastructure --startup-project src/SupportDesk.Api

# 4. Run
dotnet run --project src/SupportDesk.Api
```

Open the printed URL followed by `/scalar` for the interactive API docs.

## Tests

```bash
dotnet test
```

## Design decisions

- Domain entities expose behaviour, not setters: every state change goes through a method
  that enforces the workflow.
- The Application layer owns the persistence interface; Infrastructure implements it.
- Time is injected (`TimeProvider`), which keeps the domain and use cases deterministic in tests.
- Enums are stored as text in the database and serialized as strings in JSON.
- No MediatR and no assertion library: plain services and xUnit assertions keep the dependency list small.