using Microsoft.Extensions.Logging;


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


    [Fact]
    //https://github.com/dotnet/aspire/issues/13756
    public async Task BindMountDoesNotExist()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddContainer("container", "nginx")
            .WithBindMount("X:\\invalid\\path", "/mtn/whatever");
        AddFakeLogging(container);

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceAsync(container.Resource.Name, KnownResourceStates.FailedToStart, cts.Token);

        var snapshot = app.Services.GetFakeLogCollector().GetSnapshot();
        Assert.True(snapshot.Any());
        Assert.Contains("docker: Error response from daemon: mkdir x:\\invalid: The system cannot find the path specified.", snapshot[0].Message);
    }

    [Fact]
    public async Task BadContainerArg()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddContainer("container", "nginx")
            .WithContainerRuntimeArgs("--illegal");
        AddFakeLogging(container);

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceAsync(container.Resource.Name, KnownResourceStates.FailedToStart, cts.Token);

        // Asert on ILogger `{builder.Environment.ApplicationName}.Resources.container`
        // Expected Log: "unknown flag: --illegal"

        var snapshot = app.Services.GetFakeLogCollector().GetSnapshot();
        Assert.Contains("unknown flag: --illegal", snapshot[0].Message);
    }

    [Fact]
    public async Task BadImage()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddContainer("container", "does-not-exist")
            .WithImageRegistry("does.not.exist.internal");
        AddFakeLogging(container);

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceAsync(container.Resource.Name, KnownResourceStates.FailedToStart, cts.Token);

        var snapshot = app.Services.GetFakeLogCollector().GetSnapshot();
        Assert.Contains("Unable to find image 'does.not.exist.internal/does-not-exist:latest' locally", snapshot[0].Message);
        Assert.Contains("Error response from daemon", snapshot[1].Message);
    }

    [Fact]
    //https://github.com/dotnet/aspire/issues/10218#issuecomment-3712542734
    public async Task ContainerExitsImmediatelyAfterStart()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddContainer("container", "alpine")
            .WithEntrypoint("sh")
            .WithArgs("-c", "echo Hello World;exit 732");
        AddFakeLogging(container);

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);

        //IMO, not sure this case should really go to `FailedToStart`.  The contaienr did start, it just exited immediately.
        // So I'd expect it to go to `Runnign`, and then immediately to `Exited`.
        await app.ResourceNotifications.WaitForResourceAsync(container.Resource.Name, KnownResourceStates.FailedToStart, cts.Token);

        var snapshot = app.Services.GetFakeLogCollector().GetSnapshot();
        Assert.EndsWith("Hello World", snapshot[3].Message);
    }

}
