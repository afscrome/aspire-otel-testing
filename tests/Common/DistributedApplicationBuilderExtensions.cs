﻿using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace Common;

public static class DistributedApplicationBuilderExtensions
{
    public static T WithCILogging<T>(this T builder)
        where T : IDistributedApplicationBuilder
    {
        builder.WithOpenTelemetry();

        builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("", LogLevel.Information);
        });

        //TODO: Can we get the test results dir from xunit / MTP instead?
        var dcpLogDir = Environment.GetEnvironmentVariable("DCP_DIAGNOSTICS_LOG_FOLDER");
        var resourceLogBase = dcpLogDir is { Length: > 0 }
            ? Path.Combine(dcpLogDir, "..")
            : Path.Combine(".", "TestResults");

        builder.WithResourceFileLogging(Path.Combine(resourceLogBase, "../resource-logs"));

        return builder;
    }

    public static T WithResourceFileLogging<T>(this T builder, string logDirectory)
        where T : IDistributedApplicationBuilder
    {
        builder.Services.AddHostedService(x => ActivatorUtilities.CreateInstance<ResourceFileLogger>(x, logDirectory));

        builder.Services.AddLogging(logging =>
        {
            // Suppress the resources form the main aspire logger since we're writing them to file
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
