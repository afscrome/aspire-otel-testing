using Aspire.Hosting;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedAppHost;

public static class DistributedApplicationExtensions
{

    public static async Task StartWithLoggingAsync(this DistributedApplication app, CancellationToken cancellationToken)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<AppHostFixture>();

        logger.LogInformation("Starting App Host");

        var startTask = app.StartAsync(cancellationToken);
        var healthCheckTasks = Task.WhenAll(model.Resources.Select(WaitForHealthy));

        await Task.WhenAll(startTask, healthCheckTasks);

        logger.LogInformation("App Host started");

        async Task WaitForHealthy(IResource resource)
        {
            try
            {
                logger.LogInformation("Waiting for {Resource} to become healthy", resource.Name);
                await app.ResourceNotifications.WaitForResourceHealthyAsync(resource.Name, cancellationToken);
                logger.LogInformation("{Resource} is healthy", resource.Name);
            }
            catch (OperationCanceledException)
            {
                if (app.ResourceNotifications.TryGetCurrentState(resource.Name, out var state))
                {
                    logger.LogError("{Resource} failed to become healthy. {Health} {State} {HealthReports}", resource.Name, state.Snapshot.HealthStatus, state.Snapshot.State, state.Snapshot.HealthReports);
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
