using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace AppHostPerTest.Failures;

public class Executable
{
    private static CancellationTokenSource DefaultCancellationTokenSource()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(1));
        return cts;
    }

    private static void AddFakeLogging<T>(IResourceBuilder<T> builder)
        where T : IResource
    {
        var category = $"{builder.ApplicationBuilder.Environment.ApplicationName}.Resources.{builder.Resource.Name}";
        builder.ApplicationBuilder.Services.AddLogging(x => x.AddFakeLogging(y => y.FilteredCategories.Add(category)));
    }

    private static List<string> GetLogLines(FakeLogCollector logCollector)
    {
        return [.. logCollector.GetSnapshot()
                .Select(x => x.StructuredState?.SingleOrDefault(x => x.Key == "LineContent"))
                .Where(x => x is not null)
                .Select(x => x!.Value.Value ?? "")];
    }


    [Fact]
    // https://github.com/dotnet/aspire/issues/10218#issuecomment-3712609775
    public async Task ExecutableDoesNotExist()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddExecutable("exe", "does-not-exist", "");
        AddFakeLogging(container);

        FakeLogCollector logCollector;
        await using (var app = builder.Build())
        {
            logCollector = app.Services.GetFakeLogCollector();
            await app.StartAsync(cts.Token);
            await app.ResourceNotifications.WaitForResourceAsync(container.Resource.Name, KnownResourceStates.FailedToStart, cts.Token);
            //await app.StopAsync(cts.Token);
        }

        var logLines = GetLogLines(logCollector);
 
        var path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "%PATH%" : "$PATH";
        Assert.Contains(logLines, x => x.EndsWith($"[sys] Failed to start a process: Cmd = does-not-exist, Args = [], Error = exec: \"does-not-exist\": executable file not found in {path}"));
        Assert.Contains(logLines, x => x.EndsWith($"[sys] An attempt to start the Executable failed: Error = exec: \"does-not-exist\": executable file not found in {path}"));
    }


    [Fact]
    public async Task ExecutableExitsImmediately()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddExecutable("pwsh", "pwsh", "")
            .WithArgs("-Command", """
                Write-Host "Hello from Stdout"
                [Console]::Error.WriteLine("Hello from Stderr")
            """);
        AddFakeLogging(container);

        FakeLogCollector logCollector;
        await using (var app = builder.Build())
        {
            logCollector = app.Services.GetFakeLogCollector();
            await app.StartAsync(cts.Token);
            await app.ResourceNotifications.WaitForResourceAsync(container.Resource.Name, KnownResourceStates.Finished, cts.Token);
            //await app.StopAsync(cts.Token);
        }

        var logLines = GetLogLines(logCollector);
        Assert.Contains(logLines, x => x.EndsWith("Hello from Stdout"));
        Assert.Contains(logLines, x => x.EndsWith("Hello from Stderr"));
    }

}
