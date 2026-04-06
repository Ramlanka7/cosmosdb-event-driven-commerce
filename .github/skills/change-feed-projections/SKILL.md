---
name: change-feed-projections
description: 'Implement and review Azure Cosmos DB Change Feed processing for this platform. Use when building projection handlers, background services, Azure Functions, idempotent event consumers, lease management, retries, dead-letter handling, or downstream side effects from order events.'
argument-hint: 'Describe the event consumer, projection, or side effect you need to implement.'
---

# Change Feed Projections

Use this skill for any workflow driven by Azure Cosmos DB Change Feed.

## When To Use

- Building the Change Feed Processor service
- Implementing Azure Functions or background services that react to events
- Updating read models from event streams
- Triggering notifications or recommendations from order activity
- Designing retry, checkpoint, and replay behavior

## Procedure

1. Define the event types the processor should handle and the projection or side effect each event causes.
2. Make handlers idempotent so replay or duplicate delivery does not corrupt state.
3. Separate projection updates from external side effects when failure handling differs.
4. Use lease-based processing and preserve ordering assumptions only within a partition.
5. Record enough metadata to support replay, diagnostics, and poison-event investigation.
6. Design for rebuildability: projections can be rehydrated from the event stream.

## Guardrails

- Do not assume global ordering across partitions.
- Avoid long-running handlers that block partition progress.
- Keep retry behavior explicit when calling external systems like email or SMS.
- Prefer deterministic projection logic with side effects isolated behind clear boundaries.
