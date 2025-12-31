using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common;

public class StartupTimeoutService(ILogger<StartupTimeoutService> logger, IHostApplicationLifetime applicationLifetime, IOptions<StartupTimeoutOptions> options)
    : HostedLifecycleServiceBase
{
    public override Task StartingAsync(CancellationToken cancellationToken)
    {
        var timeout = options.Value.Timeout;
        var timeoutCts = new CancellationTokenSource(timeout);

        var timeoutRegistration = timeoutCts.Token.Register(() =>
        {
            if (logger.IsEnabled(LogLevel.Critical))
            {
                logger.LogCritical("App failed to start within {Timeout}", timeout);
            }
            applicationLifetime.StopApplication();
        });

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            timeoutRegistration.Unregister();
        });

        return Task.CompletedTask;
    }
}

public class StartupTimeoutOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}