using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

var x = builder.AddContainer("nginx", "nginx")
    .WithOtlpExporter()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithHttpEndpoint(targetPort: 80)
    .WithHttpHealthCheck("/dddd")
    ;

var cache = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithOtlpExporter();

var apiService = builder.AddProject<Projects.ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    ;

builder.AddProject<Projects.Web>("webfrontend")
    .WithExternalHttpEndpoints()
    //.WithHttpHealthCheck("/health")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
