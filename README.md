# Relay

An AI-assisted customer service hub, built as an enterprise reference app on Azure. The demo customer is **Highland Energy**, a fictional utility whose customers report outages, ask billing questions and move service.

- **Customer portal** (Next.js): help centre, start a request, follow the conversation with support.
- **Agent workspace** (Next.js): case queue with SLA tracking, case detail, replies and internal notes, assignment, customer 360.
- **CRM API** (ASP.NET Core, .NET 10): cases, customers, staff and knowledge articles behind an `ICrmStore` interface. Azure SQL today; a Dataverse implementation plugs in later.
- **AI agents** (Microsoft Agent Framework + Microsoft Foundry, `gpt-5-mini`):
  - *Triage* classifies every new case (category, priority, sentiment, safety risk) and applies it when confident and nobody has touched the case yet. Every decision is logged as an internal note.
  - *Assistant* answers customers directly when the help centre fully covers the question, using semantic search over articles (`text-embedding-3-small`). Replies are labelled as AI.
  - *Drafter* writes grounded reply drafts for staff, with `[placeholders]` instead of invented details.
  - *Summarizer* keeps a one-line case summary and sentiment for the workspace.
- **MCP server** (`Relay.Mcp`, Model Context Protocol over streamable HTTP): CRM tools any MCP client can use — VS Code / GitHub Copilot, Claude, or Foundry agents. Relay's own **Ask Relay** assistant in the workspace reaches the CRM only through this server.
- **Orchestration** (Aspire 13): runs SQL Server, the API and the web app together locally, and deploys them to Azure Container Apps.

## Architecture

```
web (Next.js, server components + server actions)
  └─► api (ASP.NET Core minimal APIs)
        ├─ Relay.Domain   — case rules: status transitions, SLA deadlines, first response, AI triage rules
        ├─ Relay.Crm.Sql  — EF Core + SQL Server implementation of ICrmStore
        └─ Relay.Agents   — Agent Framework agents, knowledge vector index, background triage worker
             │                └─► Microsoft Foundry (keyless, Entra ID)
             └─ Ask Relay ──MCP──► mcp (Relay.Mcp) ──► ICrmStore
                                     ▲
              VS Code / Copilot / Claude ──MCP──┘
```

AI is optional: with no Foundry endpoint configured, the app runs without the AI features.

The browser never calls the API directly; the Next.js server does, so the API can stay on an internal network.

## Run locally

Requires the .NET 10 SDK, Node 20+, and Docker Desktop running.

```bash
cd web && npm install && cd ..
dotnet run --project src/Relay.AppHost
```

### Enable the AI agents (optional)

Deploy the Foundry resources (about a minute; models are pay-per-use):

```bash
az group create -n rg-relay-dev -l canadaeast
az deployment group create -g rg-relay-dev -f infra/foundry.bicep \
  -p principalId=$(az ad signed-in-user show --query id -o tsv)
dotnet user-secrets set Parameters:foundry-endpoint <endpoint output> --project src/Relay.AppHost
```

Local runs authenticate with your `az login`; no keys are created (`disableLocalAuth: true`). Agent traces, including token usage, appear in the Aspire dashboard.

Open the Aspire dashboard link printed in the terminal, then the `web` endpoint. The database is migrated and seeded with demo data on first start.

- Portal: sign in as any sample customer from **Sign in**.
- Workspace: `/workspace`, then pick a staff member at the bottom of the sidebar.

Sign-in is a demo cookie for now; Entra External ID (customers) and Entra ID (staff) replace it in the Azure phase.

## MCP server

`Relay.Mcp` exposes the CRM as tools at `http://localhost:5134/mcp`:

| Tool | What it does | Changes data |
|---|---|---|
| `search_cases` | Filter by text, status, priority, category, owner | No |
| `get_case` | Full conversation, internal notes, customer, AI summary | No |
| `find_customer` | Look up by name, email or account number, with recent cases | No |
| `search_help_centre` | Semantic search over help articles | No |
| `get_queue_stats` | Open/new/waiting counts, SLA breaches, by category | No |
| `add_internal_note` | Staff-only note on a case | Yes |
| `update_case` | Status, priority, category, owner (marked destructive) | Yes |

There is deliberately no tool that messages customers: anything a customer sees goes through a person. Case rules (allowed status changes, closed cases) are enforced by the domain, and every change is logged on the case with the client's name.

**Use it from VS Code:** with the app running, open this folder in VS Code; `.vscode/mcp.json` registers the `relay-crm` server for Copilot agent mode.

The server currently runs only in Development; Entra ID (OAuth) protection is added with the Azure deployment.

## Tests

```bash
dotnet test
```

## Roadmap

1. ~~Local foundation: CRM domain, API, portal and workspace~~
2. ~~AI agents on Microsoft Foundry: triage, knowledge-grounded answers, reply drafts, summaries~~
3. ~~MCP server exposing CRM tools; Ask Relay assistant built on it~~
4. Azure deployment with `azd`: Container Apps, Azure SQL, Entra External ID, API Management AI gateway, budget alerts
5. Channels: email, chat and SMS via Azure Communication Services; voice agent via Voice Live
6. Dataverse implementation of `ICrmStore`
