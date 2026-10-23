var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

var db = sql.AddDatabase("relaydb");

var api = builder.AddProject<Projects.Relay_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("web", "../../web", "dev")
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("RELAY_API_URL", api.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(api);

builder.Build().Run();
