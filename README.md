# 🚀 Global Event-Driven Commerce Platform

## Cosmos Bootstrapper

The repository now includes a small .NET 8 bootstrapper at `src/CosmosBootstrapper` to provision the baseline Azure Cosmos DB resources.

The repository also includes a focused .NET 8 order command service at `src/OrderService` that models `order-events` as an append-only event stream backed by Azure Cosmos DB.

The bootstrapper is structured as a hosted application with:

- configuration and startup validation
- a dedicated provisioning service
- a container catalog for required resources
- a hosted entrypoint that runs once and exits cleanly

### What It Creates

- `order-events` with partition key `/aggregateId`
- `change-feed-leases` with partition key `/id`
- `orders-read` with partition key `/userId`
- `notifications` with partition key `/userId`
- `users` with partition key `/userId`
- `recommendations` with partition key `/userId`

### Configuration

- Non-secret defaults live in `src/CosmosBootstrapper/appsettings.json`
- Local secrets live in `src/CosmosBootstrapper/appsettings.Development.json`
- `appsettings.Development.json` is gitignored

### Run It

```bash
dotnet run --project src/CosmosBootstrapper
```

Set `DOTNET_ENVIRONMENT=Development` when you want the bootstrapper to load `appsettings.Development.json` for local emulator or local account settings.

Set `CosmosDb:VerifyWrite` to `true` if you also want the bootstrapper to upsert and read back a sample `order-created` event in the `order-events` container.

Set `CosmosDb:ResetDatabaseOnProvision` to `true` for practice environments when you want the bootstrapper to delete and recreate the database with shared throughput before creating containers.

## Order Service

`src/OrderService` is the first command-side service in the platform. It writes immutable order events into the `order-events` container using `/aggregateId` as the partition key and sequence-based event ids to preserve stream ordering.

### What It Does

- appends `order-created` events for new orders
- appends `order-confirmed` events for existing orders
- rebuilds aggregate state from the event stream before processing follow-up commands
- exposes a stream inspection endpoint for debugging and projection work

### Run It

```bash
dotnet run --project src/OrderService
```

The default `appsettings.json` targets the local Azure Cosmos DB Emulator and expects the bootstrapper to have already created the `commerce-platform` database and `order-events` container.

### API Surface

- `POST /orders` creates a new order stream
- `POST /orders/{orderId}/confirm` appends an `order-confirmed` event
- `GET /orders/{orderId}/events` returns the ordered event stream for an aggregate

## Reactive And Query Services

The repository now includes the rest of the baseline service scaffolding described in the architecture:

- `src/ChangeFeedProcessor` projects `order-events` into `orders-read`
- `src/ReadModelService` exposes user-centric order queries from `orders-read`
- `src/NotificationService` records deduplicated notifications from order events
- `src/RecommendationService` maintains recommendation profiles in `recommendations`
- `src/ApiGateway` provides a thin client-facing proxy over the command and query services

### Run Them

```bash
dotnet run --project src/ChangeFeedProcessor
dotnet run --project src/ReadModelService
dotnet run --project src/NotificationService
dotnet run --project src/RecommendationService
dotnet run --project src/ApiGateway
```

The reactive services expect the bootstrapper to have already created `change-feed-leases`, `orders-read`, `notifications`, and `recommendations` in the shared `commerce-platform` database.

### Standard Local Ports

- `OrderService`: `http://localhost:5080`
- `ReadModelService`: `http://localhost:5081`
- `NotificationService`: `http://localhost:5082`
- `RecommendationService`: `http://localhost:5083`
- `ApiGateway`: `http://localhost:5084`

### One-Command Startup In VS Code

The workspace now includes `.vscode/tasks.json` with two entrypoints:

- `bootstrap-cosmos` provisions the local emulator database and containers
- `bootstrap-and-start-commerce-platform` provisions Cosmos DB and then starts all long-running services with the standardized `http` launch profile

## 📌 Repository Name (Recommended)

👉 **cosmosdb-event-driven-commerce**

Alternative options:

* cosmosdb-distributed-commerce
* dotnet-cosmos-event-platform
* cloud-native-commerce-cosmos

---

# 📖 Overview

This project is a **production-grade, event-driven distributed system** built using:

* .NET (Microservices)
* Azure Cosmos DB
* Change Feed (Event Processing Backbone)

It demonstrates how to design **enterprise-scale systems** using:

* Event Sourcing
* CQRS (Command Query Responsibility Segregation)
* Distributed architecture patterns

---

# 🧠 Why This Project?

Most developers use Cosmos DB like SQL ❌

This project shows how to use Cosmos DB as:

* A **distributed event store**
* A **real-time processing engine**
* A **globally scalable database**

---

# 🏗️ Architecture

## Core Principles

* Write → Events (append-only)
* Read → Optimized models
* React → Change Feed

---

# 🧩 Services

## 1. Order Service

* Handles order commands
* Writes append-only events to Cosmos DB

## 2. Change Feed Processor

* Listens to Cosmos DB changes
* Drives the entire system

## 3. Read Model Service

* Builds query-friendly views
* Enables fast UI/API reads

## 4. Notification Service

* Records and exposes deduplicated notification messages

## 5. Recommendation Service

* Generates deterministic SKU suggestions from prior orders

## 6. API Gateway

* Entry point for all clients

---

# 🧱 Cosmos DB Design

| Container       | Purpose         | Partition Key |
| --------------- | --------------- | ------------- |
| order-events    | Event store     | /aggregateId  |
| change-feed-leases | Change feed coordination | /id |
| orders-read     | Read model      | /userId       |
| notifications   | Notification log | /userId      |
| users           | Profiles        | /userId       |
| recommendations | Personalization | /userId       |

---

# ⚙️ Key Concepts Demonstrated

## 🔥 Change Feed

* Event-driven processing
* Decoupled microservices

## ⚡ RU (Request Units)

* Performance and cost control

## 🧠 Partitioning

* High scalability design

## 🔁 Event Sourcing

* Store events, not state

## 📊 CQRS

* Separate read/write models

---

# 💻 Tech Stack

* .NET 8 Web API / Worker Services
* Azure Cosmos DB
* Background services driven by Cosmos DB Change Feed
* Docker (optional)

---

# 🚀 Getting Started

## Prerequisites

* .NET 8 SDK
* Azure Cosmos DB account (or Emulator)
* Docker (optional)

---

## Setup Steps

1. Clone the repository
2. Run the bootstrapper to create the required containers
3. Configure per-service `appsettings.json` values if you are not using the local emulator defaults
4. Run services:

```bash
dotnet run --project src/CosmosBootstrapper
dotnet run --project src/OrderService
dotnet run --project src/ChangeFeedProcessor
dotnet run --project src/ReadModelService
dotnet run --project src/NotificationService
dotnet run --project src/RecommendationService
dotnet run --project src/ApiGateway
```

---

# 🧪 Learning Outcomes

After completing this project, you will:

* Understand Cosmos DB deeply
* Design distributed systems
* Implement event-driven architecture
* Optimize performance using RU
* Build scalable cloud-native systems

---

# ⚠️ Important Notes

* This is NOT a CRUD application
* Data is modeled for **scale and performance**
* System is **event-first**, not state-first

---

# 🧭 Future Enhancements

* Multi-region deployment
* CI/CD with Azure DevOps
* Observability (App Insights)
* Advanced retry & resiliency

---

# 💡 Philosophy

> "Don’t build APIs that call systems. Build systems that react to data."

---

# 🤝 Contribution

This project is designed for learning and experimentation.

Feel free to extend with:

* Fraud detection
* Analytics pipelines
* Real-time dashboards

---

🚀 Build like an architect. Not just a developer.
