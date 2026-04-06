---
name: cosmosdb-event-store
description: 'Model Azure Cosmos DB as the event store for this project. Use when designing containers, partition keys, aggregate event schemas, RU-aware queries, event versioning, or local emulator-based data access for order events and projections.'
argument-hint: 'Describe the container, aggregate, event stream, or Cosmos DB design problem.'
---

# Cosmos DB Event Store

Use this skill for any work that designs or changes how Azure Cosmos DB is used in this repository.

## Repository Baseline

- `order-events` stores append-only events and uses `/aggregateId` as the partition key
- `orders-read` stores query models and uses `/userId`
- `users` and `recommendations` are user-partitioned containers
- Change Feed is the integration mechanism between write and read sides

## When To Use

- Designing event document shape or metadata
- Choosing or validating partition keys
- Defining new containers
- Optimizing RU consumption and query paths
- Preparing code for Azure Cosmos DB Emulator or cloud accounts

## Procedure

1. Start from the access pattern, not from a relational schema.
2. Keep events immutable and include aggregate id, event type, version, timestamp, and causation or correlation metadata.
3. Choose high-cardinality partition keys that align with dominant query and write paths.
4. Minimize cross-partition queries and avoid joins.
5. Reuse a singleton `CosmosClient`, prefer async APIs, and honor `429` retry guidance.
6. Capture diagnostics when latency or RU usage is higher than expected.

## Guardrails

- Do not treat Cosmos DB like a generic SQL CRUD store.
- Keep documents comfortably below the 2 MB item limit.
- Prefer denormalized projection containers for reads instead of expensive event-stream queries.
- Reassess partitioning before adding low-cardinality keys or fan-out queries.
