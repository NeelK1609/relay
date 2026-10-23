using Relay.Agents;
using Relay.Crm.Sql;
using Relay.Domain;
using Relay.Mcp;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<RelayDbContext>("relaydb");

builder.Services.AddScoped<ICrmStore, SqlCrmStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddRelayFoundry(builder.Configuration);

builder.Services.AddMcpServer(o => o.ServerInfo = new() { Name = "relay-crm", Title = "Relay CRM", Version = "1.0.0" })
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<CrmTools>();

var app = builder.Build();

// The tools read and change customer data, so the server must never run unauthenticated outside a dev box.
// Entra ID (OAuth) protection is added with the Azure deployment.
if (!app.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Relay.Mcp has no authentication configured and only runs in Development.");
}

app.MapDefaultEndpoints();
app.MapMcp("/mcp");

app.Run();
