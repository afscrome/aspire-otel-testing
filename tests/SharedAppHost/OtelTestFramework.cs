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
            .ConfigureResource(ConfigureResource)
            ;
    }


    public static string TestSessionId = Guid.NewGuid().ToString()[..8];

    // Somewhat copied from PracticalOtel.xUnit.v3.OpenTelemetry
    // Need to share the same resource between Xunit infrastructure, and the apphost infrastructure
    public static void ConfigureResource(ResourceBuilder builder)
    {
        builder
            .AddService(Assembly.GetExecutingAssembly().GetName().Name, null, null, serviceInstanceId: TestSessionId.ToString())
            .AddAttributes(new Dictionary<string, object>
            {
                ["test.session_id"] = TestSessionId.ToString(),
                ["test.framework.name"] = "xunit",
                ["test.framework.version"] = typeof(ITestFrameworkExecutionOptions).Assembly.GetName().Version?.ToString() ?? "unknown"
            });
    }
}