using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace Common;

public static class DistributedApplicationBuilderExtensions
{
    public static T WithTestingDefaults<T>(this T builder)
        where T : IDistributedApplicationBuilder
    {
        builder.Configuration["DcpPublisher:DependencyCheckTimeout"] = "5";
        builder.Configuration["ASPIRE_ENABLE_CONTAINER_TUNNEL"] = "true";

        return builder
            .WithTestLogging()
            .WithFinalStateLogging()
            .WithStartupTimeout(TimeSpan.FromMinutes(5))
            .WithResourceFileLogging()
            .WithOpenTelemetry()
            .WithTempAspireStore();
    }

    private static T WithTempAspireStore<T>(this T builder, string? path = null)
        where T : IDistributedApplicationBuilder
    {
        // We create the Aspire Store in a folder with user-only access. This way non-root containers won't be able
        // to access the files unless they correctly assign the required permissions for the container to work.
        builder.Configuration["Aspire:Store:Path"] = path ?? Directory.CreateTempSubdirectory().FullName;
        return builder;
    }

    public static T WithTestLogging<T>(this T builder)
        where T : IDistributedApplicationBuilder
    {
        builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug)
                .AddFilter("", LogLevel.Information)
                // See: https://github.com/dotnet/aspire/issues/13714
                .AddFilter("Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService", LogLevel.Information)
                .AddSimpleConsole(x =>
                {
                    x.SingleLine = true;
                    x.TimestampFormat = "[HH:mm:ss] ";
                    //TODO: Only set for CI?
                    x.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Disabled;
                });
        });


#pragma warning disable EXTEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        // Remove Default resiliency adding noise to health checks - https://github.com/dotnet/aspire/issues/6788
        builder.Services.ConfigureHttpClientDefaults(x => x.RemoveAllResilienceHandlers());
#pragma warning restore EXTEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        return builder;
    }


    public static T WithFinalStateLogging<T>(this T builder)
        where T : IDistributedApplicationBuilder
    { 
        builder.Services.AddHostedService<FinalStateLoggerService>();
        return builder;
    }

    public static T WithStartupTimeout<T>(this T builder, TimeSpan timeout)
        where T : IDistributedApplicationBuilder
    {
        builder.Services.AddHostedService<StartupTimeoutService>();
        builder.Services.Configure<StartupTimeoutOptions>(x => x.Timeout = timeout);
        return builder;
    }

    public static T WithResourceFileLogging<T>(this T builder)
        where T : IDistributedApplicationBuilder
    {
        //TODO: Can we get the test results dir from xunit / MTP instead?
        var dcpLogDir = Environment.GetEnvironmentVariable("ASPIRE:TEST:DCPLOGBASEPATH");
        var resourceLogBase = dcpLogDir is { Length: > 0 }
            ? Path.Combine(dcpLogDir, "..")
            : Path.Combine(".", "TestResults");

        return builder.WithResourceFileLogging(Path.Combine(resourceLogBase, "../resource-logs"));
    }

    public static T WithResourceFileLogging<T>(this T builder, string logDirectory)
        where T : IDistributedApplicationBuilder
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            return builder;
        }

        builder.Services.AddHostedService<ResourceFileLoggerHostedLifecycleService>()
            .Configure<ResourceFileLoggerOptions>(x => x.LogDirectory = logDirectory);

        // Suppress the resources form the main aspire logger since we're writing them to file
        builder.Services.AddLogging(x => x.AddFilter($"{builder.Environment.ApplicationName}.Resources", LogLevel.None));

        return builder;
    }


    public static T WithOpenTelemetry<T>(this T builder)
        where T : IDistributedApplicationBuilder
    {
        var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (string.IsNullOrEmpty(otelEndpoint))
            return builder;

        // Speed up export interval
        // (Aspire already does it for resources it manages, but want those same for the telemetry the host itself emits
        builder.Configuration["OTEL_BSP_SCHEDULE_DELAY"] = "1000";
        builder.Configuration["OTEL_BLRP_SCHEDULE_DELAY"] = "1000";
        builder.Configuration["OTEL_METRIC_EXPORT_INTERVAL"] = "1000";

        // Only configuring logging here - traces & metrics are handled at process level by xUnit integration
        builder.Services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(x =>
            {
                x.IncludeFormattedMessage = true;
                x.IncludeScopes = true;
                x.AddOtlpExporter();

                var resourceBuilder = ResourceBuilder.CreateDefault();
                OtelHelper.ConfigureResource(resourceBuilder);
                x.SetResourceBuilder(resourceBuilder);
            });
        });

        builder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
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

        return builder;

    }
}
