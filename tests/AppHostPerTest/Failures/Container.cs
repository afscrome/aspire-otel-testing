using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;


namespace AppHostPerTest.Failures;

public class Container
{
    private static CancellationTokenSource DefaultCancellationTokenSource()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(1));
        return cts;
    }

    private static void AddFakeLogging<T>(IResourceBuilder<T> builder)
        where T: IResource
    {
        var category = $"{builder.ApplicationBuilder.Environment.ApplicationName}.Resources.{builder.Resource.Name}";
        builder.ApplicationBuilder.Services.AddLogging(x => x.AddFakeLogging(y => y.FilteredCategories.Add(category)));
    }

    private static List<string?> GetLogLines(FakeLogCollector logCollector)
    {
        return [.. logCollector.GetSnapshot()
                .Select(x => x.StructuredState?.SingleOrDefault(x => x.Key == "LineContent"))
                .Select(x => x?.Value)];
    }

    [Fact]
    //https://github.com/dotnet/aspire/issues/13756
    public async Task IllegalBindMount()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var illegalPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "blah:\\invalid"
            : "/dev/null/invalid";

        var container = builder.AddContainer("container", "nginx")
            .WithBindMount(illegalPath, "/mtn/whatever");
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
        Assert.Contains(logLines, x => x.EndsWith("docker: Error response from daemon: mkdir x:\\invalid: The system cannot find the path specified."));
    }

    [Fact]
    public async Task BadContainerRuntimeArg()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddContainer("container", "nginx")
            .WithContainerRuntimeArgs("--illegal");
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
        Assert.Contains(logLines, x => x.EndsWith("unknown flag: --illegal"));
    }

    [Fact]
    public async Task BadImage()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddContainer("container", "does-not-exist")
            .WithImageRegistry("does.not.exist.internal")
            .WithImagePullPolicy(ImagePullPolicy.Always);
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
        Assert.Contains(logLines, x => x.Contains("Error response from daemon"));
    }

    [Fact]
    public async Task NeedsAuthentication()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddContainer("container", "mattermost.com/go-msft-fips:1.24.6")
            .WithImageRegistry("cgr.dev")
            .WithImagePullPolicy(ImagePullPolicy.Always);
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
        Assert.Contains(logLines, x => x.EndsWith("Error response from daemon: error from registry: Authentication required"));
    }


    [Fact]
    //https://github.com/dotnet/aspire/issues/10218#issuecomment-3712542734
    public async Task ContainerExitsImmediatelyAfterStart()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddContainer("container", "alpine")
            .WithEntrypoint("sh")
            // write to both stdout and stderr to verify we capture both
            .WithArgs("-c", """
                echo "Hello from Stdout"
                >&2 echo "Hello from Stderr";
                exit 7123
            """);
        AddFakeLogging(container);

        FakeLogCollector logCollector;
        await using (var app = builder.Build())
        {
            logCollector = app.Services.GetFakeLogCollector();
            await app.StartAsync(cts.Token);

            //IMO, not sure this case should really go to `FailedToStart`.  The contaienr did start, it just exited immediately.
            // So I'd expect it to go to `Runnign`, and then immediately to `Exited`.
            // https://github.com/dotnet/aspire/issues/13760
            await app.ResourceNotifications.WaitForResourceAsync(container.Resource.Name, KnownResourceStates.FailedToStart, cts.Token);
            await app.StopAsync(cts.Token);
        }

        var logLines = GetLogLines(logCollector);
        // assert output from both stdout and stderr are captured
        Assert.Contains(logLines, x => x.EndsWith("Hello from Stdout"));
        Assert.Contains(logLines, x => x.EndsWith("Hello from Stderr"));
    }

}
