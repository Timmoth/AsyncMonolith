using Aspire.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var postgresdb = postgres.AddDatabase("postgresdb");

//postgres.WithPgAdmin();

var api = builder.AddProject<Example_Api>("example-api")
    .WaitFor(postgresdb)
    .WithReference(postgresdb);

builder.Build().Run();