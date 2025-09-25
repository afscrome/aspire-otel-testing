using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace SharedAppHost.Framework;

public static class DistributedApplicationExtensions
{
    public static async Task StartWithLoggingAsync(this DistributedApplication app, CancellationToken cancellationToken)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<AppHostFixture>();

        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation("Starting App Host");

        var startTask = app.StartAsync(cancellationToken);
        var healthCheckTasks = Task.WhenAll(model.Resources.Select(WaitForHealthy));

        await Task.WhenAll(startTask, healthCheckTasks);

        logger.LogInformation("App Host started, and all resources are healthy");

        async Task WaitForHealthy(IResource resource)
        {
            try
            {
                await app.ResourceNotifications.WaitForResourceHealthyAsync(resource.Name, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (app.ResourceNotifications.TryGetCurrentState(resource.Name, out var state))
                {
                    var snapshot = state.Snapshot;
                    logger.LogError("{Resource} failed to become healthy. {Health} {State} {HealthReports}", resource.Name, snapshot.HealthStatus, snapshot.State, snapshot.HealthReports);
                }
                else
                {
                    logger.LogError("{Resource} failed to become healthy - Unable to determine state", resource.Name);
                }
                throw;
            }
        }
    }

}
