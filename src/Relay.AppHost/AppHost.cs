var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

var db = sql.AddDatabase("relaydb");

// Output of infra/foundry.bicep. Set with: dotnet user-secrets set Parameters:foundry-endpoint <url> --project src/Relay.AppHost
var foundryEndpoint = builder.AddParameter("foundry-endpoint");

var api = builder.AddProject<Projects.Relay_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("Foundry__Endpoint", foundryEndpoint)
    .WithEnvironment("Foundry__TraceSensitiveData", builder.ExecutionContext.IsRunMode ? "true" : "false")
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("web", "../../web", "dev")
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("RELAY_API_URL", api.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(api);

builder.Build().Run();
