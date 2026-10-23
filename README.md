# Relay

An AI-assisted customer service hub, built as an enterprise reference app on Azure. The demo customer is **Highland Energy**, a fictional utility whose customers report outages, ask billing questions and move service.

- **Customer portal** (Next.js): help centre, start a request, follow the conversation with support.
- **Agent workspace** (Next.js): case queue with SLA tracking, case detail, replies and internal notes, assignment, customer 360.
- **CRM API** (ASP.NET Core, .NET 10): cases, customers, staff and knowledge articles behind an `ICrmStore` interface. Azure SQL today; a Dataverse implementation plugs in later.
- **Orchestration** (Aspire 13): runs SQL Server, the API and the web app together locally, and deploys them to Azure Container Apps.

## Architecture

```
web (Next.js, server components + server actions)
  └─► api (ASP.NET Core minimal APIs)
        ├─ Relay.Domain   — case rules: status transitions, SLA deadlines, first response
        └─ Relay.Crm.Sql  — EF Core + SQL Server implementation of ICrmStore
```

The browser never calls the API directly; the Next.js server does, so the API can stay on an internal network.

## Run locally

Requires the .NET 10 SDK, Node 20+, and Docker Desktop running.

```bash
cd web && npm install && cd ..
dotnet run --project src/Relay.AppHost
```

Open the Aspire dashboard link printed in the terminal, then the `web` endpoint. The database is migrated and seeded with demo data on first start.

- Portal: sign in as any sample customer from **Sign in**.
- Workspace: `/workspace`, then pick a staff member at the bottom of the sidebar.

Sign-in is a demo cookie for now; Entra External ID (customers) and Entra ID (staff) replace it in the Azure phase.

## Tests

```bash
dotnet test
```

## Roadmap

1. ~~Local foundation: CRM domain, API, portal and workspace~~
2. AI agents on Microsoft Foundry: triage, knowledge-grounded answers, reply drafts, hand-off summaries; MCP server for CRM tools
3. Azure deployment with `azd`: Container Apps, Azure SQL, Entra External ID, API Management AI gateway, budget alerts
4. Channels: email, chat and SMS via Azure Communication Services; voice agent via Voice Live
5. Dataverse implementation of `ICrmStore`
