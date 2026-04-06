---
name: local-setup-and-emulator
description: 'Set up local development for this repository. Use when configuring .NET 8, Azure Cosmos DB Emulator, appsettings, required containers, optional Docker assets, or first-run instructions for Order Service and related services in the event-driven commerce platform.'
argument-hint: 'Describe the local setup problem, missing dependency, or service you need to run.'
---

# Local Setup And Emulator

Use this skill when bootstrapping or troubleshooting local development.

## Local Baseline

- .NET 8 SDK is required
- Azure Cosmos DB account or Azure Cosmos DB Emulator is required
- Docker is optional
- Services are expected to run independently during development

## Procedure

1. Verify the required SDKs, emulator, and optional Docker tooling are installed.
2. Configure `appsettings.json` or environment variables for local Cosmos DB connectivity.
3. Create the required containers before starting services.
4. Start the write-side service first, then any change feed or projection services.
5. Validate that events are written, projections update, and read APIs reflect the new state.
6. Keep local settings isolated from cloud credentials and deployment configuration.

## Guardrails

- Do not hardcode production connection strings or keys.
- Keep container names, partition keys, and seed assumptions aligned with the repository architecture.
- Prefer emulator-based development for fast iteration and lower cost.
- Document any required startup order when adding new services.