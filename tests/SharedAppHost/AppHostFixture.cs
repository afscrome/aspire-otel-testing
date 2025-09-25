using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
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
        cts.CancelAfter(TimeSpan.FromSeconds(60));

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();

        //TODO: Sync this resource up with the xunit fixture wide resoruce
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            //logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
            logging.AddFilter("Microsoft.", LogLevel.Debug);
            logging.AddFilter(typeof(AppHostFixture).Namespace, LogLevel.Debug);
        });

        ConfigureOtel(appHost);

        App = await appHost.BuildAsync(cts.Token);
        var logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<AppHostFixture>();

        logger.LogInformation("Starting App Host");
        await App.StartAsync(cts.Token);
        logger.LogInformation("App Host started");

        //wait for all resources to go healthy
        await Task.WhenAll(appHost.Resources.Select(WaitForHealthy));

        async Task WaitForHealthy(IResource resource)
        {
            try
            {
                logger.LogInformation("Waiting for {Resource} to become healthy", resource.Name);
                await App.ResourceNotifications.WaitForResourceHealthyAsync(resource.Name, cts.Token);
                logger.LogInformation("{Resource} is healthy", resource.Name);
            }
            catch (OperationCanceledException ex) //when (ex.CancellationToken == cts.Token)
            {
                if (App.ResourceNotifications.TryGetCurrentState(resource.Name, out var state))
                {
                    throw new TimeoutException($"{resource.Name} failed to become healthy. Health {state.Snapshot.HealthStatus}, State: {state.Snapshot.State},", ex);
                }
                else
                {
                    throw new TimeoutException($"{resource.Name} failed to become healthy - unable to determine current state", ex);
                }
            }
        }
    }


    private static void ConfigureOtel(IDistributedApplicationTestingBuilder appHost)
    {
        var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (string.IsNullOrEmpty(otelEndpoint))
            return;

        // Speed up export interval
        // (Aspire already does it for resources it manages, but want those same for the telemetry the host itself emits
        appHost.Configuration["OTEL_BSP_SCHEDULE_DELAY"] = "1000";
        appHost.Configuration["OTEL_BLRP_SCHEDULE_DELAY"] = "1000";
        appHost.Configuration["OTEL_METRIC_EXPORT_INTERVAL"] = "1000";

        // Only configuring logging here - traces & metrics are handled at process level by xUnit integration
        appHost.Services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(x => {
                var resourceBuilder = ResourceBuilder.CreateDefault();
                OtelTestFramework.ConfigureResource(resourceBuilder);
                x.IncludeFormattedMessage = true;
                x.AddOtlpExporter();
                x.SetResourceBuilder(resourceBuilder);
            });
        });

        appHost.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
        {
            foreach (var resource in evt.Model.Resources)
            {
                resource.Annotations.Add(new EnvironmentCallbackAnnotation(e =>
                {
                    e.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = new HostUrl(otelEndpoint);
                }));
            }

            return Task.CompletedTask;
        });

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
