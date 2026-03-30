---
name: read-model-cqrs
description: 'Design CQRS read models and query APIs for this event-driven commerce system. Use when creating projection schemas, denormalized read containers, query endpoints, projection versioning, rebuild workflows, or fast user-centric read paths from Cosmos DB-backed events.'
argument-hint: 'Describe the query scenario, projection, or read API you want to add or change.'
---

# Read Model CQRS

Use this skill when work belongs on the query side of the platform.

## When To Use

- Creating new read containers or views
- Shaping order summary or user-centric query models
- Designing query APIs behind the API Gateway
- Rebuilding or versioning projections
- Reviewing denormalization choices for performance

## Procedure

1. Start with the exact query or UI/API response shape.
2. Build a denormalized projection tailored to that access pattern.
3. Partition read models for dominant lookup paths, such as user-centric queries.
4. Keep projection versioning explicit so rebuilds can happen safely.
5. Make read APIs simple and fast; they should not reconstruct aggregates from events on every request.
6. Treat projections as disposable artifacts that can be regenerated from the event log.

## Guardrails

- Do not leak write-model invariants into read-side schemas without a reason.
- Avoid over-generalized read models that require expensive filters or joins.
- Separate projection rebuild logic from live incremental updates when possible.
- Optimize for query latency and RU efficiency, not strict normalization.
