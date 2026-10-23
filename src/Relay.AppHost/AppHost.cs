using Aspire.Hosting.Azure;
using Aspire.Hosting.Foundry;

var builder = DistributedApplication.CreateBuilder(args);
var publishing = builder.ExecutionContext.IsPublishMode;

if (publishing)
{
    // Telemetry goes to Log Analytics; the hosted Aspire dashboard isn't needed and is one less public endpoint.
    builder.AddAzureContainerAppEnvironment("aca").WithDashboard(false);
}

// Azure SQL (free offer, Entra-only auth) in Azure; a SQL Server container locally.
// The subscription can't create SQL servers in canadaeast (where the model quota is), so SQL lives next door.
const string sqlLocation = "canadacentral";
var db = builder.AddAzureSqlServer("sql")
    .RunAsContainer(c => c.WithLifetime(ContainerLifetime.Persistent).WithDataVolume())
    .ConfigureInfrastructure(infra =>
    {
        foreach (var resource in infra.GetProvisionableResources())
        {
            if (resource is Azure.Provisioning.Sql.SqlServer server)
            {
                server.Location = new Azure.Core.AzureLocation(sqlLocation);
                // New name: Azure still reserves the default name for the failed canadaeast attempt.
                server.Name = Azure.Provisioning.Expressions.BicepFunction.Interpolate(
                    $"sqlcc-{Azure.Provisioning.Expressions.BicepFunction.GetUniqueString(Azure.Provisioning.Expressions.BicepFunction.GetResourceGroup().Id)}");
            }
            if (resource is Azure.Provisioning.Sql.SqlDatabase database) database.Location = new Azure.Core.AzureLocation(sqlLocation);
        }
    })
    .AddDatabase("relaydb");

var mcp = builder.AddProject<Projects.Relay_Mcp>("mcp")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health")
    .PublishAsAzureContainerApp((_, app) => Scale(app, max: 2));

var api = builder.AddProject<Projects.Relay_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WaitFor(mcp)
    .WithEnvironment("Mcp__Endpoint", ReferenceExpression.Create($"{mcp.GetEndpoint("http")}/mcp"))
    .WithHttpHealthCheck("/health")
    // One replica: the triage queue and nightly reset run in-process (Service Bus would lift this).
    .PublishAsAzureContainerApp((_, app) => Scale(app, max: 1));

if (publishing)
{
    var foundry = builder.AddFoundry("foundry");
    var chat = foundry.AddDeployment("chat", FoundryModel.OpenAI.Gpt5Mini)
        .WithProperties(d => { d.SkuName = "GlobalStandard"; d.SkuCapacity = 50; });
    var embedding = foundry.AddDeployment("embedding", FoundryModel.OpenAI.TextEmbedding3Small)
        .WithProperties(d => { d.SkuName = "GlobalStandard"; d.SkuCapacity = 50; });

    // The API gets its own identity so it can be granted the MCP server's app role.
    var apiIdentity = builder.AddAzureUserAssignedIdentity("api-identity");
    var mcpAuth = builder.AddBicepTemplate("mcp-auth", "../../infra/mcp-auth.bicep")
        .WithParameter("callerPrincipalId", apiIdentity.GetOutput("principalId"));

    var communication = builder.AddBicepTemplate("communication", "../../infra/communication.bicep")
        .WithParameter("principalId", apiIdentity.GetOutput("principalId"));

    // Placeholder matches no address, so the public demo sends no real email until you set one.
    var emailAllowlist = builder.AddParameter("emailAllowlist", "none", publishValueAsDefault: true);

    api.WithEnvironment("Communication__Endpoint", communication.GetOutput("endpoint"))
        .WithEnvironment("Communication__SenderAddress", communication.GetOutput("senderAddress"))
        .WithEnvironment("Communication__AllowedRecipients", emailAllowlist);

    api.WithAzureUserAssignedIdentity(apiIdentity)
        .WithReference(chat)
        .WithReference(embedding)
        .WithEnvironment("Mcp__Scope", mcpAuth.GetOutput("scope"))
        .WithEnvironment("Demo__DailyReset", "true");

    mcp.WithReference(embedding)
        .WithEnvironment("McpAuth__TenantId", mcpAuth.GetOutput("tenantId"))
        .WithEnvironment("McpAuth__ClientId", mcpAuth.GetOutput("appId"));
}
else
{
    // Output of infra/foundry.bicep. Set with: dotnet user-secrets set Parameters:foundry-endpoint <url> --project src/Relay.AppHost
    var foundryEndpoint = builder.AddParameter("foundry-endpoint");
    foreach (var service in new[] { api, mcp })
    {
        service.WithEnvironment("Foundry__Endpoint", foundryEndpoint)
            .WithEnvironment("Foundry__TraceSensitiveData", "true");
    }

    // Outputs of infra/communication.bicep deployed to the dev resource group (see README).
    api.WithEnvironment("Communication__Endpoint", builder.AddParameter("communication-endpoint"))
        .WithEnvironment("Communication__SenderAddress", builder.AddParameter("email-sender"))
        .WithEnvironment("Communication__AllowedRecipients", builder.AddParameter("email-allowlist"));
}

// ASPIREJAVASCRIPT001: AddNextJsApp is preview; it builds the standalone production container.
#pragma warning disable ASPIREJAVASCRIPT001
var web = builder.AddNextJsApp("web", "../../web")
    .WithEnvironment("RELAY_API_URL", api.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(api)
    .PublishAsAzureContainerApp((_, app) => Scale(app, max: 3));
#pragma warning restore ASPIREJAVASCRIPT001

// Links in emails point at the customer portal.
api.WithEnvironment("Communication__PortalBaseUrl", web.GetEndpoint("http"));

builder.Build().Run();

// Scale to zero when idle so the demo costs nothing between visits.
static void Scale(Azure.Provisioning.AppContainers.ContainerApp app, int max)
{
    app.Template.Scale.MinReplicas = 0;
    app.Template.Scale.MaxReplicas = max;
}
