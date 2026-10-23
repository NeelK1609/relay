using System.ClientModel.Primitives;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Relay.Agents;

public static class DependencyInjection
{
    public const string TelemetrySource = "Relay.Agents";

    /// <summary>Foundry model clients and the help-centre vector index. Returns false when no endpoint is configured.</summary>
    public static bool AddRelayFoundry(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FoundryOptions>(o => Bind(o, configuration));
        var options = new FoundryOptions();
        Bind(options, configuration);
        if (!options.IsConfigured) return false;

        // Keyless: local dev uses your Azure CLI login, Azure uses the app's managed identity (AZURE_CLIENT_ID).
        services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

        // OPENAI001: token-auth constructor is marked experimental but is the documented way to call Foundry's v1 endpoint.
#pragma warning disable OPENAI001
        services.TryAddSingleton(sp => new OpenAIClient(
            new BearerTokenPolicy(sp.GetRequiredService<TokenCredential>(), "https://cognitiveservices.azure.com/.default"),
            new OpenAIClientOptions { Endpoint = new Uri(new Uri(options.Endpoint!), "openai/v1/") }));
#pragma warning restore OPENAI001

        services.TryAddSingleton(sp => sp.GetRequiredService<OpenAIClient>().GetEmbeddingClient(options.EmbeddingDeployment));
        services.TryAddSingleton<IChatClient>(sp => sp.GetRequiredService<OpenAIClient>()
            .GetChatClient(options.ChatDeployment)
            .AsIChatClient()
            .AsBuilder()
            .UseOpenTelemetry(sp.GetRequiredService<ILoggerFactory>(), TelemetrySource,
                c => c.EnableSensitiveData = sp.GetRequiredService<IOptions<FoundryOptions>>().Value.TraceSensitiveData)
            .Build(sp));
        services.TryAddSingleton<KnowledgeIndex>();
        return true;
    }

    /// <summary>
    /// Reads the Foundry section, then lets Aspire connection strings ("Endpoint=…;Deployment=chat")
    /// supply the endpoint and deployment names in Azure.
    /// </summary>
    private static void Bind(FoundryOptions options, IConfiguration configuration)
    {
        configuration.GetSection(FoundryOptions.Section).Bind(options);

        if (Parse(configuration.GetConnectionString("chat")) is { } chat)
        {
            options.Endpoint ??= chat.Endpoint;
            options.ChatDeployment = chat.Deployment ?? options.ChatDeployment;
        }
        if (Parse(configuration.GetConnectionString("embedding")) is { } embedding)
        {
            options.Endpoint ??= embedding.Endpoint;
            options.EmbeddingDeployment = embedding.Deployment ?? options.EmbeddingDeployment;
        }
    }

    private static (string? Endpoint, string? Deployment)? Parse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var parts = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
        return (parts.TryGetValue("Endpoint", out var e) ? e as string : null,
                parts.TryGetValue("Deployment", out var d) ? d as string : null);
    }

    /// <summary>Registers the case agents and background triage when Foundry is configured; otherwise AI features stay off.</summary>
    public static IServiceCollection AddRelayAgents(this IServiceCollection services, IConfiguration configuration)
    {
        if (!services.AddRelayFoundry(configuration))
        {
            services.AddSingleton<ITriageQueue, NoOpTriageQueue>();
            return services;
        }

        services.Configure<McpOptions>(configuration.GetSection(McpOptions.Section));
        services.AddSingleton<RelayAgents>();
        services.AddScoped<CaseAssistant>();
        services.AddScoped<OpsCopilot>();
        services.AddSingleton<InProcessTriageQueue>();
        services.AddSingleton<ITriageQueue>(sp => sp.GetRequiredService<InProcessTriageQueue>());
        services.AddHostedService<TriageWorker>();
        return services;
    }
}
