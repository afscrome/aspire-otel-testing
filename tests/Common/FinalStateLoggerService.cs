using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace Common;

public class FinalStateLoggerService(
    ILogger<FinalStateLoggerService> logger,
    DistributedApplicationModel model,
    ResourceNotificationService resourceNotificationService)
    : HostedLifecycleServiceBase
{
    public override Task StoppingAsync(CancellationToken cancellationToken)
    {
        foreach (var resource in model.Resources)
        {
            if (resourceNotificationService.TryGetCurrentState(resource.Name, out var evt))
            {
                var snapshot = evt.Snapshot;
                logger.LogInformation("StateAtTestEnd: {ResourceName} Type: {ResourceType} Health: {Health} State: {State} Reports: {HealthReports}",
                    resource.Name,
                    resource.GetType().Name,
                    snapshot.HealthStatus,
                    snapshot.State,
                    snapshot.HealthReports);
            }
            else
            {
                logger.LogInformation("StateAtTestEnd: {ResourceName} Type: {ResourceType} - No state available",
                    resource.Name,
                    resource.GetType().FullName);
            }
        }
        return Task.CompletedTask;
    }
}