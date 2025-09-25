using System.Reflection;
using OpenTelemetry.Resources;
using Xunit.Sdk;

public class OtelHelper
{
    internal static string TestSessionId = Environment.GetEnvironmentVariable("TEST_SESSION_ID") ?? Guid.NewGuid().ToString()[..8];

    // Somewhat copied from PracticalOtel.xUnit.v3.OpenTelemetry
    // Need to share the same resource between Xunit infrastructure, and the apphost infrastructure
    public static void ConfigureResource(ResourceBuilder builder)
    {
        builder
            .AddService(Assembly.GetExecutingAssembly().GetName().Name!, null, null, serviceInstanceId: TestSessionId.ToString())
            .AddAttributes(new Dictionary<string, object>
            {
                ["test.session.id"] = TestSessionId.ToString(),
                ["test.framework.name"] = "xunit",
                ["test.framework.version"] = typeof(ITestFrameworkExecutionOptions).Assembly.GetName().Version?.ToString() ?? "unknown"
            });
    }
}