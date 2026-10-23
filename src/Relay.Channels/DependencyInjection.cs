using Azure.Communication.Email;
using Azure.Communication.Identity;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Relay.Agents;
using Relay.Domain;

namespace Relay.Channels;

public static class DependencyInjection
{
    /// <summary>
    /// Live chat and email over Azure Communication Services, authenticated with Entra ID.
    /// Call after AddRelayAgents: chat needs the AI assistant, email does not.
    /// </summary>
    public static IServiceCollection AddRelayChannels(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CommunicationOptions>(configuration.GetSection(CommunicationOptions.Section));
        var options = configuration.GetSection(CommunicationOptions.Section).Get<CommunicationOptions>() ?? new();

        if (!options.IsConfigured)
        {
            services.TryAddSingleton<ICaseNotifier, NoOpCaseNotifier>();
            return services;
        }

        var endpoint = new Uri(options.Endpoint!);
        services.TryAddSingleton<TokenCredential>(_ => new Azure.Identity.DefaultAzureCredential());
        services.AddSingleton(sp => new CommunicationIdentityClient(endpoint, sp.GetRequiredService<TokenCredential>()));
        services.AddSingleton(sp => new EmailClient(endpoint, sp.GetRequiredService<TokenCredential>()));
        services.AddSingleton(sp => new ChatGateway(sp.GetRequiredService<CommunicationIdentityClient>(), endpoint));

        services.AddScoped<EmailSender>();
        services.AddSingleton<QueuedCaseNotifier>();
        services.AddSingleton<ICaseNotifier>(sp => sp.GetRequiredService<QueuedCaseNotifier>());
        services.AddHostedService<EmailNotificationWorker>();

        if (services.Any(d => d.ServiceType == typeof(RelayAgents)))
        {
            services.AddScoped<ChatService>();
        }
        return services;
    }
}
