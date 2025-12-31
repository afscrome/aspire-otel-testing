using Aspire.Hosting;
using Common;
using System.Diagnostics;

namespace SharedAppHost;

public class AppHostFixture : IAsyncLifetime, IClassFixture<AppHostFixture>
{
    private static readonly ActivitySource Source = new("AppHostFixture");

    public DistributedApplication App { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        using var _ = Source.StartActivity("AppHostFixture.InitializeAsync");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(40));

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(cts.Token);
        appHost.WithCILogging();

        App = await appHost.BuildAsync(cts.Token);

        await App.StartAsync(cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        // HACK: Need to give Projects enough time to flush their telemery
        // Without this, telemetry intermittently goes missing
        await Task.Delay(TimeSpan.FromSeconds(2));

        using var _ = Source.StartActivity("AppHostFixture.DisposeAsync");
        await (App?.DisposeAsync() ?? ValueTask.CompletedTask);
    }
}
