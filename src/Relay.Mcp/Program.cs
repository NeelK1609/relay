using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// In Azure, callers need an Entra token for the Relay MCP app with the Crm.Tools app role.
var tenantId = builder.Configuration["McpAuth:TenantId"];
var clientId = builder.Configuration["McpAuth:ClientId"];
var authEnabled = !string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(clientId);
if (authEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
            o.TokenValidationParameters.ValidAudience = clientId;
            o.MapInboundClaims = false;
        });
    builder.Services.AddAuthorization(o => o.AddPolicy("CrmTools", p => p
        .RequireAuthenticatedUser()
        .RequireClaim("roles", "Crm.Tools")));
}

builder.Services.AddMcpServer(o => o.ServerInfo = new() { Name = "relay-crm", Title = "Relay CRM", Version = "1.0.0" })
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<CrmTools>();

var app = builder.Build();

// The tools read and change customer data, so the server never runs unauthenticated outside a dev box.
if (!authEnabled && !app.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Relay.Mcp requires McpAuth:TenantId and McpAuth:ClientId outside Development.");
}

app.MapDefaultEndpoints();

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapMcp("/mcp").RequireAuthorization("CrmTools");
}
else
{
    app.MapMcp("/mcp");
}

app.Run();
