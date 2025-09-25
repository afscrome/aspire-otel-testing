using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace SharedAppHost.Framework;

public static class DistributedApplicationBuilderExtensions
{
    public static T WithResourceFileLogging<T>(this T builder, string logDirectory)
        where T : IDistributedApplicationBuilder
    {
        builder.Services.AddHostedService(x => ActivatorUtilities.CreateInstance<ResourceFileLogger>(x, logDirectory));

        builder.Services.AddLogging(logging =>
        {
            // Supress the resources form the main aspire logger since we're writing them to file
            logging.AddFilter($"{builder.Environment.ApplicationName}.Resources", LogLevel.None);
        });

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
            logging.AddOpenTelemetry(x => {
                var resourceBuilder = ResourceBuilder.CreateDefault();
                OtelTestFramework.ConfigureResource(resourceBuilder);
                x.IncludeFormattedMessage = true;
                x.AddOtlpExporter();
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
