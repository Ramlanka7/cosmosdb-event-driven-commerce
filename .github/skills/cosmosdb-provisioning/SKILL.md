---
name: cosmosdb-provisioning
description: 'Provision Azure Cosmos DB for this repository. Use when creating the database, containers, partition keys, throughput settings, indexing policy, TTL rules, emulator initialization, seed data, or startup checks for order-events, orders-read, users, and recommendations.'
argument-hint: 'Describe whether you are provisioning locally or in Azure, and which containers or policies need to be created.'
---

# Cosmos DB Provisioning

Use this skill when the task is to create or validate the database layer required by this repository.

## Important Terminology

- In Azure Cosmos DB NoSQL, the main resources are databases and containers.
- Traditional SQL-style tables are not used here.
- Container design, partition keys, and throughput choices are part of the schema design.

## Repository Baseline

Create one Cosmos DB database for the platform, then provision these containers:

| Container | Purpose | Partition Key |
|---|---|---|
| `order-events` | Append-only event store | `/aggregateId` |
| `orders-read` | Query-optimized order view | `/userId` |
| `users` | User profiles | `/userId` |
| `recommendations` | Personalized recommendations | `/userId` |

## When To Use

- Creating the database for the first time
- Initializing the Azure Cosmos DB Emulator for local development
- Adding startup provisioning code in .NET services
- Defining partition keys, throughput, indexing, TTL, or unique key policies
- Validating that required containers exist before service startup
- Creating bootstrap scripts or infrastructure code for Cosmos DB setup

## Provisioning Procedure

1. Decide whether provisioning runs locally against the emulator or against an Azure Cosmos DB account.
2. Create the database if it does not already exist.
3. Create each required container with the correct partition key path.
4. Choose throughput deliberately:
   - Start simple for local development.
   - Use autoscale or provisioned throughput in Azure based on workload.
5. Keep indexing enabled by default, then narrow it only when real query patterns justify changes.
6. Add service startup checks or a dedicated bootstrap path so missing containers fail fast or are created intentionally.
7. Validate the setup by writing an order event and confirming downstream projections can read from the required containers.

## Recommended Defaults

- Database name: keep it environment-specific and configurable
- `order-events`: optimized for ordered event writes per aggregate
- `orders-read`: optimized for user-centric queries and dashboards
- `users`: simple profile lookups by user id
- `recommendations`: user-centric personalized data with fast point reads
- Local development: prefer emulator-based provisioning
- Cloud deployment: keep names, throughput, and keys in configuration or IaC

## Guardrails

- Do not invent SQL-style tables for Cosmos DB NoSQL.
- Do not use low-cardinality partition keys like status or type.
- Do not query the event store directly for every UI request; create or update projections instead.
- Do not hardcode production keys or endpoints in source files.
- Do not over-customize indexing before measuring actual RU or latency problems.

## Implementation Guidance

When generating code or scripts for this repository:

1. Use idempotent create-if-not-exists behavior for the database and containers.
2. Keep the container list centralized so every service shares the same names and partition keys.
3. Separate provisioning from normal request handling when possible.
4. Make local and cloud connection settings configurable via `appsettings.json` and environment variables.
5. If a new feature needs another container, define its access pattern and partition key before adding it.

## Expected Outputs

This skill should help produce one or more of the following:

- .NET startup or bootstrap code to create the Cosmos DB database and containers
- Local setup documentation for emulator initialization
- IaC or scripting inputs for Azure provisioning
- Validation logic that confirms all required containers exist before services run