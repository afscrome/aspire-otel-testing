using Common;
using System.Text;

namespace AppHostPerTest;

public class Scenarios
{
    [Fact]
    public async Task ResourceGoesHealthy()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await using var builder = DistributedApplicationTestingBuilder.Create()
            .WithTestingDefaults();

        var nginx = builder.AddContainer("nginx", "nginx")
            .WithHttpEndpoint(targetPort: 80)
            .WithHttpHealthCheck("/");

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);

        await app.ResourceNotifications.WaitForResourceHealthyAsync(nginx.Resource.Name, cts.Token);

        var x = 123;
    }


    [Fact(Explicit = true)]
    public async Task ResourceDoesntGoHealthy()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await using var builder = DistributedApplicationTestingBuilder.Create()
            .WithTestingDefaults()
            .WithStartupTimeout(TimeSpan.FromSeconds(10));

        var nginx = builder.AddContainer("nginx", "nginx")
            .WithHttpEndpoint(targetPort: 80);

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);

        await app.ResourceNotifications.WaitForResourceHealthyAsync(nginx.Resource.Name, cts.Token);
    }

    [Fact(Explicit = true)]
    public async Task ResourceDoesntGoHealthyBetter()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await using var builder = DistributedApplicationTestingBuilder.Create()
            .WithTestingDefaults()
            .WithStartupTimeout(TimeSpan.FromSeconds(10));


        var nginx = builder.AddContainer("nginx", "nginx")
            .WithHttpEndpoint(targetPort: 80)
            .WithHttpHealthCheck("/does-not-exist")
            ;

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);

        await WaitForResourceHealthyAsyncBetter(nginx);

        async Task WaitForResourceHealthyAsyncBetter(IResourceBuilder<IResource> builder)
        {
            try
            {
                await app.ResourceNotifications.WaitForResourceHealthyAsync(nginx.Resource.Name, cts.Token);
            }
            catch (OperationCanceledException ex)
            {
                var resource = builder.Resource;
                if (app.ResourceNotifications.TryGetCurrentState(resource.Name, out var evt) && evt.Snapshot != null)
                {
                    var state = evt.Snapshot;
                    var error = new StringBuilder()
                        .AppendLine($"Resource {resource.Name} failed to become healthy before WaitForResourceHealthyAsync was cancelled")
                        .AppendLine($"Current State: {state.State?.Text}")
                        .AppendLine($"Current Health: {state.HealthStatus}");

                    foreach(var report in evt.Snapshot.HealthReports)
                    {
                        error.AppendLine($"- {report.Name}: {report.Status} @ {report.LastRunAt} {report.ExceptionText}");
                    }

                    throw new OperationCanceledException(error.ToString(), ex, ex.CancellationToken);
                }

                throw new OperationCanceledException($"WaitForResourceHealthyAsync canceleld before resource {nginx.Resource.Name} started", ex, ex.CancellationToken);
            }

        }
    }
}

