# CivicFlow architecture decisions

## 1. Modular monolith

CivicFlow begins as a modular monolith. The project needs clear boundaries and maintainability, but its portfolio-sized workload does not justify distributed deployment, messaging infrastructure or microservice failure modes.

## 2. Controller-based ASP.NET Core API

Controllers make endpoint grouping, authorisation attributes, filters, response documentation and enterprise conventions visible to reviewers. Minimal APIs may still be used for isolated infrastructure endpoints such as health checks.

## 3. Domain-enforced state transitions

`ServiceRequest` owns its status changes. Controllers call domain methods rather than assigning arbitrary status values. This prevents invalid changes from alternative clients and provides a focused unit-test surface.

## 4. SQL Server and EF Core

The domain contains strongly related transactional data: users, teams, categories, cases, assignments, comments and audit events. A relational database is therefore the primary store. EF Core configurations live in Infrastructure so the Domain project remains independent of persistence.

## 5. Identity separation

ASP.NET Core Identity lives in Infrastructure. Business entities keep user identifiers but do not depend on Identity framework classes. JWT authentication and hashed rotating refresh tokens are exposed by the API.

## 6. UTC persistence

All server timestamps use UTC. The client is responsible for presenting dates in the user's local timezone.

## 7. Accessibility and privacy

The client targets WCAG 2.2 AA practices. Public comments and internal notes are filtered at the API boundary to reduce accidental disclosure risk.
