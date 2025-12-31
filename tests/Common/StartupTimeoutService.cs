using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Common;

public class StartupTimeoutService(ILogger<StartupTimeoutService> logger, IHostApplicationLifetime applicationLifetime)
    : HostedLifecycleServiceBase
{
    public override Task StartingAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), applicationLifetime.ApplicationStarted);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == applicationLifetime.ApplicationStarted)
            {
                logger.LogInformation("Application started successfully within the timeout period");
                return;
            }
            logger.LogCritical("App failed to start in time");
            applicationLifetime.StopApplication();
        }, applicationLifetime.ApplicationStarted);

        return Task.CompletedTask;
    }
}