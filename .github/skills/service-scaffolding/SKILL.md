---
name: service-scaffolding
description: 'Scaffold and extend the .NET services in this repository. Use when creating or structuring the Order Service, Read Model Service, Notification Service, Recommendation Service, or API Gateway, and when defining shared contracts, configuration, and service responsibilities.'
argument-hint: 'Name the service or feature to scaffold and the responsibilities it should own.'
---

# Service Scaffolding

Use this skill when creating the application structure described in the README.

## Target Services

- Order Service
- Change Feed Processor
- Read Model Service
- Notification Service
- Recommendation Service
- API Gateway

## Procedure

1. Confirm the service purpose and whether it belongs on the command side, query side, or reactive processing side.
2. Create a focused .NET project with only the dependencies that support that responsibility.
3. Keep event contracts explicit and versionable.
4. Isolate infrastructure code for Cosmos DB, messaging, and external integrations.
5. Add configuration for local emulator and cloud deployment separately.
6. Define tests around business behavior, projection logic, and contract mapping.

## Guardrails

- Do not merge unrelated responsibilities into a single service just because the repo is small initially.
- Keep the API Gateway thin; business workflows belong in domain or processing services.
- Prefer shared contracts packages only for stable integration types, not domain shortcuts.
- Add background processing only where asynchronous reactions are required.
