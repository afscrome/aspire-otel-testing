using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using SharedAppHost.Framework;
using System.Diagnostics;

namespace SharedAppHost;

public class AppHostFixture : IAsyncLifetime, IClassFixture<AppHostFixture>
{
    public DistributedApplication App { get; private set; } = default!;
    ActivitySource Source = new("AppHostFixture");

    public async ValueTask InitializeAsync()
    {
        using var _ = Source.StartActivity("AppHostFixture.InitializeAsync");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();

        appHost.WithOpenTelemetry();

        if (Environment.GetEnvironmentVariable("DCP_DIAGNOSTICS_LOG_FOLDER") is { Length: > 0 } dcpLogDir)
        {
            //TODO: Can we get the test results directory from xunit instead of repeating it
            appHost.WithResourceFileLogging(Path.Combine(dcpLogDir, ".."));

        }


        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Information);
            logging.AddFilter("Microsoft.", LogLevel.Information);
            logging.AddFilter(typeof(AppHostFixture).Namespace, LogLevel.Information);
        });

        App = await appHost.BuildAsync(cts.Token);
 
        await App.StartWithLoggingAsync(cts.Token);
        throw new Exception();
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
