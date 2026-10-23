using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Relay.Agents;
using Relay.Api;
using Relay.Crm.Sql;
using Relay.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<RelayDbContext>("relaydb");

builder.Services.AddScoped<ICrmStore, SqlCrmStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddRelayAgents(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CaseRuleExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapPortalEndpoints();
app.MapWorkspaceEndpoints();
app.MapKnowledgeEndpoints();
app.MapAiEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RelayDbContext>();
    await db.Database.MigrateAsync();
    await DemoData.SeedAsync(db, TimeProvider.System.GetUtcNow());

    // Backfill: triage open cases that arrived while AI was off.
    var triage = scope.ServiceProvider.GetRequiredService<ITriageQueue>();
    var crm = scope.ServiceProvider.GetRequiredService<ICrmStore>();
    foreach (var c in await crm.ListCasesAsync(new CaseQuery(Status: CaseStatus.New), CancellationToken.None))
    {
        if (c.Insights is null) await triage.EnqueueAsync(c.Id);
    }
}

app.Run();

sealed class CaseRuleExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "The request breaks a case rule."),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "The case changed while you were editing it."),
            _ => (0, ""),
        };
        if (status == 0) return false;

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Title = title, Detail = exception.Message, Status = status },
        });
    }
}
