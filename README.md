# SupportDesk API

[![CI](https://github.com/AlexisJFranco1575/supportdesk-api/actions/workflows/ci.yml/badge.svg)](https://github.com/AlexisJFranco1575/supportdesk-api/actions/workflows/ci.yml)

A support-ticket API built with **.NET 10**, **Clean Architecture**, **EF Core** and **SQL Server**.
It models a real ticket workflow (Open → In progress → Resolved → Closed, with reopening),
secures it with **JWT authentication, role-based authorization and per-user ownership**, and keeps the
business rules in a framework-free domain layer. The HTTP surface is tested end to end against a real
SQL Server started with Testcontainers.

## Status

Work in progress.

- [x] Milestone 1: domain model, unit tests, API skeleton, CI
- [x] Milestone 2: application layer, EF Core + SQL Server, migrations, ticket endpoints
- [x] Milestone 3: integration tests (Testcontainers), readiness probe, dependency audit in CI
- [x] Milestone 4a: users, JWT authentication, roles, input validation
- [x] Milestone 4b: ownership rules (customers only see their own tickets)
- [ ] Milestone 5: Docker image and cloud deployment

## Architecture

```
src/
  SupportDesk.Domain/          Entities and business rules (no dependencies)
  SupportDesk.Application/     Use cases and ports (interfaces)
  SupportDesk.Infrastructure/  EF Core, SQL Server, JWT, password hashing, migrations
  SupportDesk.Api/             ASP.NET Core minimal API, OpenAPI + Scalar docs
tests/
  SupportDesk.Domain.Tests/          Rules of the domain
  SupportDesk.Application.Tests/     Use cases against in-memory fakes
  SupportDesk.Api.IntegrationTests/  Real HTTP + real SQL Server (Testcontainers)
```

Dependencies point inwards: `Api → Infrastructure → Application → Domain`.

## Security model

| Role | Can do |
| --- | --- |
| Anonymous | Register, log in, health probes |
| Customer | Create tickets and read **only their own** |
| Agent | Read every ticket; assign, resolve, close, reopen and re-prioritise them |
| Admin | Everything an agent can, plus change user roles |

- Registration always creates a `Customer`; only an admin can promote a user.
- Passwords are hashed with PBKDF2 (ASP.NET Core Identity hasher, 210,000 iterations); plain passwords are never stored or returned.
- Access tokens are short-lived JWTs (30 minutes) signed with HMAC-SHA256. The role travels in the token, so a role change applies at the next login.
- A wrong password and an unknown email produce the same `401` response, and the same amount of hashing work, so accounts cannot be enumerated.
- A ticket's requester is taken from the validated token, never from the request body.
- Customers asking for someone else's ticket get `404`, not `403`, so the ticket's existence is not revealed. The "own tickets only" restriction is applied inside the database query.
- Working on tickets is protected twice: by an endpoint policy and by a rule inside the service.
- The signing key and the first admin come from user-secrets / environment variables, never from the repository.

## Endpoints

| Method | Route | Access | Description |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | Anonymous | Create a customer account |
| POST | `/api/auth/login` | Anonymous | Exchange credentials for a JWT |
| GET | `/api/auth/me` | Authenticated | Identity carried by the token |
| PUT | `/api/users/{id}/role` | Admin | Change a user's role |
| POST | `/api/tickets` | Authenticated | Create a ticket |
| GET | `/api/tickets` | Authenticated | List tickets (`status`, `priority`, `page`, `pageSize`); customers see only their own |
| GET | `/api/tickets/{id}` | Authenticated | Get a ticket (customers: only their own) |
| POST | `/api/tickets/{id}/start` | Agent | Assign and start working |
| POST | `/api/tickets/{id}/resolve` | Agent | Mark as resolved |
| POST | `/api/tickets/{id}/close` | Agent | Close a resolved ticket |
| POST | `/api/tickets/{id}/reopen` | Agent | Reopen a resolved or closed ticket |
| PATCH | `/api/tickets/{id}/priority` | Agent | Change priority |
| GET | `/health/live` | Anonymous | Liveness probe (process is up) |
| GET | `/health/ready` | Anonymous | Readiness probe (database reachable) |

Errors follow RFC 9457 problem details:

| Status | Meaning |
| --- | --- |
| 400 | Invalid input (with per-field errors) or malformed request |
| 401 | Missing/invalid token or wrong credentials |
| 403 | Authenticated, but the role is not allowed |
| 404 | Unknown resource (or one the caller may not see) |
| 409 | Conflict (for example, email already registered) |
| 422 | A business rule was violated (for example, resolving a ticket that is not in progress) |

## Run locally

Requirements: .NET 10 SDK and Docker.

```bash
# 1. Database
cp .env.example .env            # then set your own password
docker compose up -d

# 2. Secrets (kept out of the repo with user-secrets)
P=src/SupportDesk.Api
dotnet user-secrets init --project $P
dotnet user-secrets set "ConnectionStrings:SupportDesk" "Server=localhost,1433;Database=SupportDesk;User Id=sa;Password=<your password>;TrustServerCertificate=True" --project $P
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>" --project $P
dotnet user-secrets set "Seed:AdminEmail" "admin@supportdesk.local" --project $P
dotnet user-secrets set "Seed:AdminPassword" "<a strong password>" --project $P

# 3. Schema
dotnet tool restore
dotnet ef database update --project src/SupportDesk.Infrastructure --startup-project src/SupportDesk.Api

# 4. Run
dotnet run --project src/SupportDesk.Api
```

Open the printed URL followed by `/scalar` for the interactive API docs. Log in through
`POST /api/auth/login`, then choose **Bearer Token** in the Authentication panel and paste the token.

## Tests

```bash
dotnet test
```

The integration tests start a disposable SQL Server container (Docker required), apply the real
migrations and call the API in memory. Nothing needs to be configured.

## Design decisions

- Domain entities expose behaviour, not setters: every state change goes through a method
  that enforces the invariants.
- The Application layer owns the persistence, hashing, token and current-user interfaces;
  Infrastructure and the API implement them.
- Time is injected (`TimeProvider`), which keeps the domain and use cases deterministic in tests.
- Enums are stored as text in the database and serialized as strings in JSON.
- Unique indexes are the source of truth for uniqueness; the repository turns a violated index into a
  `409`, so concurrent requests (or two instances starting at once) cannot corrupt the data.
- Separate liveness and readiness probes, so a platform can tell "restart me" from "stop sending traffic".
- CI fails when a dependency has a known vulnerability, so security fixes are never silently skipped.
- No MediatR and no assertion library: plain services and xUnit assertions keep the dependency list small.