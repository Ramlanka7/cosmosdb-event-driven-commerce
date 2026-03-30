---
name: commerce-architecture
description: 'Design and refine the event-driven commerce platform architecture for this repository. Use when planning service boundaries, event flows, CQRS responsibilities, API gateway integration, notifications, recommendations, or eventual consistency across .NET microservices.'
argument-hint: 'Describe the feature, service, or architectural decision you want to design or review.'
---

# Commerce Architecture

Use this skill when work affects the overall design of the platform described in the repository README.

## When To Use

- Adding a new microservice or changing service ownership
- Defining how commands, events, projections, and APIs interact
- Reviewing whether a feature belongs in write side, read side, or asynchronous processing
- Planning notification, recommendation, or gateway integration
- Validating eventual consistency tradeoffs

## Architecture Baseline

- Order Service writes append-only order events
- Azure Cosmos DB is the event backbone and source of truth
- Change Feed drives asynchronous processing
- Read Model Service builds query-optimized views
- API Gateway exposes client-facing endpoints
- Notification and Recommendation services react to domain events

## Procedure

1. Identify the command source, aggregate, and event stream affected by the change.
2. Decide which service owns write operations and which services only react to published events.
3. Keep the write model append-only; do not couple command handling to read model updates.
4. Define the change feed consumers, side effects, retries, and idempotency boundaries.
5. Decide which read models need denormalized data for fast queries.
6. Document consistency expectations and failure handling before writing code.

## Guardrails

- Prefer asynchronous reactions over direct service-to-service orchestration.
- Treat read models as rebuildable projections, not primary state.
- Keep service boundaries aligned to business capabilities, not technical layers.
- Avoid CRUD-style designs that bypass events as the system record.
