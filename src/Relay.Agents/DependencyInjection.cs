using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Relay.Agents;

public static class DependencyInjection
{
    public const string TelemetrySource = "Relay.Agents";

    /// <summary>Registers the agents when a Foundry endpoint is configured; otherwise AI features stay off.</summary>
    public static IServiceCollection AddRelayAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FoundryOptions>(configuration.GetSection(FoundryOptions.Section));
        var options = configuration.GetSection(FoundryOptions.Section).Get<FoundryOptions>() ?? new FoundryOptions();

        if (!options.IsConfigured)
        {
            services.AddSingleton<ITriageQueue, NoOpTriageQueue>();
            return services;
        }

        // Keyless: local dev uses your Azure CLI login, Azure uses the app's managed identity.
        // OPENAI001: token-auth constructor is marked experimental but is the documented way to call Foundry's v1 endpoint.
#pragma warning disable OPENAI001
        services.AddSingleton(_ => new OpenAIClient(
            new BearerTokenPolicy(new DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default"),
            new OpenAIClientOptions { Endpoint = new Uri(new Uri(options.Endpoint!), "openai/v1/") }));
#pragma warning restore OPENAI001

        services.AddSingleton(sp => sp.GetRequiredService<OpenAIClient>().GetEmbeddingClient(options.EmbeddingDeployment));

        services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<OpenAIClient>()
            .GetChatClient(options.ChatDeployment)
            .AsIChatClient()
            .AsBuilder()
            .UseOpenTelemetry(sp.GetRequiredService<ILoggerFactory>(), TelemetrySource,
                c => c.EnableSensitiveData = sp.GetRequiredService<IOptions<FoundryOptions>>().Value.TraceSensitiveData)
            .Build(sp));

        services.AddSingleton<KnowledgeIndex>();
        services.AddSingleton<RelayAgents>();
        services.AddScoped<CaseAssistant>();
        services.AddSingleton<InProcessTriageQueue>();
        services.AddSingleton<ITriageQueue>(sp => sp.GetRequiredService<InProcessTriageQueue>());
        services.AddHostedService<TriageWorker>();
        return services;
    }
}
