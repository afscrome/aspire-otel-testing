using Common;
using Google.Protobuf.WellKnownTypes;
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
    }


    [Fact(Explicit = true)]
    public async Task ResourceDoesntGoHealthy()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await using var builder = DistributedApplicationTestingBuilder.Create()
            .WithTestingDefaults()
            .WithStartupTimeout(TimeSpan.FromSeconds(10));

        var nginx = builder.AddContainer("nginx", "nginx")
            .WithHttpEndpoint(targetPort: 80)
            .WithHttpHealthCheck("/always-fail"); ;

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);

        cts.CancelAfter(TimeSpan.FromSeconds(5));
        await app.ResourceNotifications.WaitForResourceHealthyAsync(nginx.Resource.Name, cts.Token);
    }

    [Fact(Explicit = true)]
    public async Task ResourceDoesntGoHealthyBetter()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        await using var builder = DistributedApplicationTestingBuilder.Create()
            .WithTestingDefaults()
            .WithStartupTimeout(TimeSpan.FromSeconds(10));


        var nginx = builder.AddContainer("nginx", "nginx")
            .WithHttpEndpoint(targetPort: 80)
            .WithHttpHealthCheck("/does-not-exist");

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);

        await WaitForResourceHealthyAsyncBetter(app.ResourceNotifications, nginx, cts.Token, TimeSpan.FromSeconds(5));
    }

    async Task WaitForResourceHealthyAsyncBetter(ResourceNotificationService resourceNotificationService, IResourceBuilder<IResource> builder, CancellationToken token, TimeSpan timeout)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        await WaitForResourceHealthyAsyncBetter(resourceNotificationService, builder, cts.Token);
    }

    async Task WaitForResourceHealthyAsyncBetter(ResourceNotificationService resourceNotificationService, IResourceBuilder<IResource> builder, CancellationToken token)
    {
        try
        {
            await resourceNotificationService.WaitForResourceHealthyAsync(builder.Resource.Name, token);
        }
        catch (OperationCanceledException ex)
        {
            var resource = builder.Resource;
            if (resourceNotificationService.TryGetCurrentState(resource.Name, out var evt) && evt.Snapshot != null)
            {
                var state = evt.Snapshot;
                var error = new StringBuilder()
                    .AppendLine($"Resource {resource.Name} failed to become healthy before WaitForResourceHealthyAsync was cancelled")
                    .AppendLine($"Current State: {state.State?.Text}")
                    .AppendLine($"Current Health: {state.HealthStatus}");

                foreach (var report in evt.Snapshot.HealthReports)
                {
                    error.AppendLine($"- {report.Name}: {report.Status} @ {report.LastRunAt} {report.ExceptionText}");
                }

                throw new OperationCanceledException(error.ToString(), ex, ex.CancellationToken);
            }

            throw new OperationCanceledException($"WaitForResourceHealthyAsync canceleld before resource {builder.Resource.Name} started", ex, ex.CancellationToken);
        }

    }

}

