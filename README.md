# SupportDesk API

A support-ticket API built with **.NET 10**, **Clean Architecture** and **SQL Server**.
It models a real ticket workflow (Open → In progress → Resolved → Closed, with reopening)
and keeps the business rules in a framework-free domain layer covered by unit tests.

## Status

Work in progress. Milestone 1 (domain model, tests, API skeleton, CI) is done.

## Architecture

```
src/
  SupportDesk.Domain/     Entities and business rules (no dependencies)
  SupportDesk.Api/        ASP.NET Core minimal API, OpenAPI + Scalar docs
tests/
  SupportDesk.Domain.Tests/   xUnit tests for the domain rules
```

Planned: Application layer (use cases), Infrastructure (EF Core + SQL Server),
JWT authentication, integration tests, Docker and cloud deployment.

## Run locally

Requirements: .NET 10 SDK.

```bash
dotnet test
dotnet run --project src/SupportDesk.Api
```

Then open the URL printed in the console, followed by `/scalar` for the interactive API docs.

## Design decisions

- Domain entities expose behaviour, not setters: every state change goes through a method
  that enforces the workflow.
- Time is passed in explicitly, which keeps the domain deterministic and easy to test.
- Plain xUnit assertions, no assertion library, to keep the dependency list small.