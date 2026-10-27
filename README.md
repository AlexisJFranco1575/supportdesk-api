# SupportDesk API

[![CI](https://github.com/AlexisJFranco1575/supportdesk-api/actions/workflows/ci.yml/badge.svg)](https://github.com/AlexisJFranco1575/supportdesk-api/actions/workflows/ci.yml)

A support-ticket API built with **.NET 10**, **Clean Architecture**, **EF Core** and **SQL Server**.
It models a real ticket workflow (Open → In progress → Resolved → Closed, with reopening), secures it with
**JWT authentication, role-based authorization and per-user ownership**, and keeps the business rules in a
framework-free domain layer. The HTTP surface is tested end to end against a real SQL Server started with
Testcontainers, and the API runs as a container on **Azure Container Apps** with **Azure SQL Database**.

**Live demo:** https://supportdesk-api.niceground-5b163a4e.westus3.azurecontainerapps.io

**Web app (Angular):** https://alexisjfranco1575.github.io/supportdesk-web/ ([source](https://github.com/AlexisJFranco1575/supportdesk-web))

> The demo runs on free-tier infrastructure that scales to zero: the first request after a quiet period can take
> 20-40 seconds while the container starts and the database resumes. Create an account with
> `POST /api/auth/register`, log in, then choose **Bearer Token** in the Authentication panel and paste the token.

## Status

- [x] Milestone 1: domain model, unit tests, API skeleton, CI
- [x] Milestone 2: application layer, EF Core + SQL Server, migrations, ticket endpoints
- [x] Milestone 3: integration tests (Testcontainers), readiness probe, dependency audit in CI
- [x] Milestone 4: users, JWT authentication, roles, validation, ownership rules
- [x] Milestone 5: Docker image, image published from CI, deployment on Azure

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

```
GitHub Actions ──build, test, audit──▶ image on ghcr.io ──▶ Azure Container Apps ──▶ Azure SQL Database
```

## Security model

| Role | Can do |
| --- | --- |
| Anonymous | Register, log in, health probes |
| Customer | Create tickets and read **only their own** |
| Agent | Read every ticket; assign, resolve, close, reopen and re-prioritise them |
| Admin | Everything an agent can, plus list users and change their roles (never their own) |

- Registration always creates a `Customer`; only an admin can promote a user.
- Passwords are hashed with PBKDF2 (ASP.NET Core Identity hasher, 210,000 iterations); plain passwords are never stored or returned.
- Access tokens are short-lived JWTs (30 minutes) signed with HMAC-SHA256. The role travels in the token, so a role change applies at the next login.
- A wrong password and an unknown email produce the same `401` response, and the same amount of hashing work, so accounts cannot be enumerated.
- A ticket's requester is taken from the validated token, never from the request body.
- Customers asking for someone else's ticket get `404`, not `403`, so the ticket's existence is not revealed. The "own tickets only" restriction is applied inside the database query.
- Working on tickets is protected twice: by an endpoint policy and by a rule inside the service.
- Secrets (database connection, signing key, first admin) come from environment variables or user-secrets, never from the repository or the image.
- The container runs as a non-root user.

## Endpoints

| Method | Route | Access | Description |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | Anonymous | Create a customer account |
| POST | `/api/auth/login` | Anonymous | Exchange credentials for a JWT |
| GET | `/api/auth/me` | Authenticated | Identity carried by the token |
| GET | `/api/users` | Admin | List user accounts (`page`, `pageSize`), without password data |
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

Open the printed URL followed by `/scalar` for the interactive API docs.

## Run with Docker

With the database from step 1 above running:

```bash
docker build -t supportdesk-api:local .

docker run --rm -p 8080:8080 \
  -e "ConnectionStrings__SupportDesk=Server=host.docker.internal,1433;Database=SupportDeskDocker;User Id=sa;Password=<your password>;TrustServerCertificate=True" \
  -e "Jwt__SigningKey=<at least 32 random characters>" \
  -e "Database__MigrateOnStartup=true" \
  -e "Docs__Enabled=true" \
  -e "Seed__AdminEmail=admin@supportdesk.local" \
  -e "Seed__AdminPassword=<a strong password>" \
  supportdesk-api:local
```

Configuration switches:

| Setting | Effect |
| --- | --- |
| `Database__MigrateOnStartup=true` | Applies pending EF Core migrations when the app starts |
| `Docs__Enabled=true` | Serves `/scalar` and `/openapi/v1.json` outside Development |
| `Seed__AdminEmail` / `Seed__AdminPassword` | Creates the first administrator if it does not exist |

## Deployment

Every push to `main` runs the build, tests and dependency audit, then publishes the image to
`ghcr.io/alexisjfranco1575/supportdesk-api`. The live demo runs that image on Azure Container Apps
(consumption plan, scale to zero) with an Azure SQL Database on the free offer. Secrets are stored as
Container Apps secrets and injected as environment variables.

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

## Known limitations and next steps

- No rate limiting or account lockout on login yet.
- Access tokens are not revocable before they expire, and there are no refresh tokens.
- The demo database firewall allows Azure services; a production setup would use a private network and managed identity.
- Deployment to Azure is done by hand with the Azure CLI; automating it from CI is the natural next step.

## License

[MIT](LICENSE)