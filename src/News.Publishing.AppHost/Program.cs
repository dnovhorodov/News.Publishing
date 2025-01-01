var builder = DistributedApplication.CreateBuilder(args);

var martenDb = builder.AddPostgres("marten")
    .WithLifetime(ContainerLifetime.Persistent);

if (builder.ExecutionContext.IsRunMode)
{
    // Data volumes don't work on ACA for Postgres so only add when running
    martenDb.WithDataVolume();
}

var publishingService = builder.AddProject<Projects.News_Publishing_Api>("publishing")
    .WithExternalHttpEndpoints()
    .WithReference(martenDb)
    .WaitFor(martenDb);

builder.Build().Run();
