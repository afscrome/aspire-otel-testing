using OpenTelemetry;
using OpenTelemetry.Trace;
using PracticalOtel.xUnit.v3.OpenTelemetry;
using System.Diagnostics;

namespace Common;

public class OtelTestFramework : TracedPipelineStartup
{

    static OtelTestFramework()
    {
        //TODO: Listen to otel event source, and record otel failures somewhow
        // https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation/blob/main/src/OpenTelemetry.AutoInstrumentation/Diagnostics/SdkSelfDiagnosticsEventListener.cs

        //TODO: Dont' hardcode env vars like this
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4317");
        Environment.SetEnvironmentVariable("OTEL_BSP_SCHEDULE_DELAY", "1000");
        Environment.SetEnvironmentVariable("OTEL_BLRP_SCHEDULE_DELAY", "1000");
        Environment.SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "1000");

    }

    public OtelTestFramework()
    {
        traceProviderSetup = tpb => tpb
            .AddSource("*")
            .AddProcessor(new DcpNoiseScrubber())
            .SetSampler<DcpNoiseSampler>()
            .ConfigureResource(OtelHelper.ConfigureResource)
            ;
    }


    public class DcpNoiseScrubber : BaseProcessor<Activity>
    {
        public override void OnStart(Activity activity)
        {
            var url = activity.Tags.FirstOrDefault(kv => kv.Key == "url.full").Value;

            if (url == null)
            {
                return;
            }

            if (url.Contains("apis/usvc-dev.developer.microsoft.com") || url.Contains("/DCP/"))
            {
                // Console.WriteLine($"DCP Noise scrubber dropping {data.DisplayName} {url}");
                activity.IsAllDataRequested = false;
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }
        }
        public override void OnEnd(Activity activity)
        {
            if (activity.OperationName != "System.Net.Http.HttpRequestOut")
            {
                return;
            }

            var url = activity.Tags.FirstOrDefault(kv => kv.Key == "url.full").Value;

            if (url == null)
            {
                return;
            }

            if (url.Contains("apis/usvc-dev.developer.microsoft.com") || url.Contains("/DCP/"))
            {
                // Console.WriteLine($"DCP Noise scrubber dropping {data.DisplayName} {url}");
                activity.IsAllDataRequested = false;
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }
        }
    }

    public class DcpNoiseSampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            var url = samplingParameters.Tags?.FirstOrDefault(kv => kv.Key == "url.full").Value as string;

            return IsNoise(url)
                ? new SamplingResult(SamplingDecision.Drop)
                : new SamplingResult(SamplingDecision.RecordAndSample);
        }

        bool IsNoise(string? url)
        {
            if (url != null && url.Contains("apis/usvc-dev.developer.microsoft.com"))
            {
                return true;
            }

            return false;
        }
    }
}