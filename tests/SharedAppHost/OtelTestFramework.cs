using Aspire.Hosting.ApplicationModel;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PracticalOtel.xUnit.v3.OpenTelemetry;
using Projects;
using SharedAppHost;
using System.Diagnostics;
using System.Reflection;
using Xunit.Sdk;
using Xunit.v3;

[assembly: TestPipelineStartup(typeof(OtelTestFramework))]

namespace SharedAppHost;

public class OtelTestFramework : TracedPipelineStartup {

    static OtelTestFramework()
    {
        //TODO: Listen to otel event source, and record otel failures somewhow
        // https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation/blob/main/src/OpenTelemetry.AutoInstrumentation/Diagnostics/SdkSelfDiagnosticsEventListener.cs

        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4317");
        Environment.SetEnvironmentVariable("OTEL_BSP_SCHEDULE_DELAY", "1000");
        Environment.SetEnvironmentVariable("OTEL_BLRP_SCHEDULE_DELAY", "1000");
        Environment.SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "1000");

    }

    public OtelTestFramework()
    {
        traceProviderSetup = tpb => tpb
            .AddSource("*")
            .ConfigureResource(OtelHelper.ConfigureResource)
            ;
    }



}