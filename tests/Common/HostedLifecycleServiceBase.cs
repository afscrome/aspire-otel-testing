using Microsoft.Extensions.Hosting;

namespace Common;

public class HostedLifecycleServiceBase : IHostedLifecycleService
{
    internal HostedLifecycleServiceBase() { }

    public virtual Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}