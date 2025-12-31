using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

namespace Common;

// Based on https://github.com/dotnet/aspire/blob/main/src/Aspire.Hosting/ResourceLoggerForwarderService.cs
internal class ResourceFileLogger(ResourceNotificationService resourceNotificationService,
    ResourceLoggerService resourceLoggerService, string logDirectory) : HostedLifecycleServiceBase
{
    public override Task StartingAsync(CancellationToken cancellationToken)
    {
        _ = Watch(cancellationToken);
        return Task.CompletedTask;
    }

    public async Task Watch(CancellationToken cancellationToken)
    {
        try
        {
            var loggingResourceIds = new HashSet<string>();
            var logWatchTasks = new List<Task>();

            await foreach (var resourceEvent in resourceNotificationService.WatchAsync(cancellationToken).ConfigureAwait(false))
            {
                var resourceId = resourceEvent.ResourceId;

                if (loggingResourceIds.Add(resourceId))
                {
                    // Start watching the logs for this resource ID
                    logWatchTasks.Add(WatchResourceLogs(resourceEvent.Resource, resourceId, cancellationToken));
                }
            }

            await Task.WhenAll(logWatchTasks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // this was expected as the token was canceled
        }
    }

    private async Task WatchResourceLogs(IResource resource, string resourceId, CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(logDirectory);
        directory.Create();

        var logPath = Path.Combine(directory.FullName, $"{resource.Name}-{resourceId}.log");

        using var logFileStream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(logFileStream)
        {
            AutoFlush = true,

        };

        try
        {
            await foreach (var logEvent in resourceLoggerService.WatchAsync(resourceId).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                foreach (var line in logEvent)
                {
                    if (line.IsErrorMessage)
                    {
                        await writer.WriteAsync("ERR: ");
                    }
                    await writer.WriteLineAsync(line.Content);
                }
            }
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // this was expected as the token was canceled
        }
    }
}
