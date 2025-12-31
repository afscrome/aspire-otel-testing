using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace Common;

public sealed class FinalStateLoggerService(
    ILogger<FinalStateLoggerService> logger,
    DistributedApplicationModel model,
    ResourceNotificationService resourceNotificationService)
    : HostedLifecycleServiceBase, IDisposable
{
    private bool HasLoggedFinalState = false;

    // When app host is disposed rather than being gracefully stopped, the full host lifecycle is bypassed, so we only get a `Dispose` call, and not `StoppingAsync`
    // But if the app is stopped gracefully, we want to log the state before we start shutting down the application.
    // So hook into both events, and only log on the first one to be hit.
    public void Dispose()
    {
        LogFinalState();
    }

    public override Task StoppingAsync(CancellationToken cancellationToken)
    {
        LogFinalState();
        return Task.CompletedTask;
    }

    private void LogFinalState()
    {
        if (HasLoggedFinalState || !logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        HasLoggedFinalState = true;

        foreach (var resource in model.Resources)
        {
            if (resourceNotificationService.TryGetCurrentState(resource.Name, out var evt))
            {
                var snapshot = evt.Snapshot;
                logger.LogInformation("Resource: \"{ResourceName}\", Type: \"{ResourceType}\", ExitCode: {ExitCode}, Health: {Health}, State: {State} Reports: {HealthReports}",
                    resource.Name,
                    resource.GetType().Name,
                    snapshot.ExitCode,
                    snapshot.HealthStatus,
                    snapshot.State,
                    snapshot.HealthReports);
            }
            else
            {
                logger.LogInformation("Resource: {ResourceName} Type: {ResourceType} - No state available",
                    resource.Name,
                    resource.GetType().FullName);
            }
        }
    }

}