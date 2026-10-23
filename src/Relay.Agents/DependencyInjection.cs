using System.ClientModel.Primitives;
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
        services.Configure<FoundryOptions>(configuration.GetSection(FoundryOptions.Section));
        var options = configuration.GetSection(FoundryOptions.Section).Get<FoundryOptions>() ?? new FoundryOptions();
        if (!options.IsConfigured) return false;

        // Keyless: local dev uses your Azure CLI login, Azure uses the app's managed identity.
        // OPENAI001: token-auth constructor is marked experimental but is the documented way to call Foundry's v1 endpoint.
#pragma warning disable OPENAI001
        services.TryAddSingleton(_ => new OpenAIClient(
            new BearerTokenPolicy(new DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default"),
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
