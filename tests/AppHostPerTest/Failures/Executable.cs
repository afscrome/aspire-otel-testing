using Microsoft.Extensions.Logging;

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


    [Fact]
    // https://github.com/dotnet/aspire/issues/10218#issuecomment-3712609775
    public async Task ExecutableDoesNotExist()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddExecutable("exe", "does-not-exist", "");
        AddFakeLogging(container);

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceAsync(container.Resource.Name, KnownResourceStates.FailedToStart, cts.Token);

        //await Task.Delay(1_000);

        var snapshot = app.Services.GetFakeLogCollector().GetSnapshot();
        Assert.True(snapshot.Any());
        Assert.Contains("[sys] Failed to start a process: Cmd = does-not-exist, Args = [], Error = exec: \"does-not-exist\": executable file not found in %PATH%", snapshot[1].Message);
        Assert.Contains("[sys] Failed to start Executable: Error = exec: \"does-not-exist\": executable file not found in %PATH%", snapshot[2].Message);
    }


    [Fact]
    public async Task ExecutableExitsImmediately()
    {
        using var cts = DefaultCancellationTokenSource();
        await using var builder = DistributedApplicationTestingBuilder.Create();

        var container = builder.AddExecutable("cmd", "cmd", "")
            .WithArgs("/c", "echo Hello World& exit 732");
        AddFakeLogging(container);

        await using var app = builder.Build();
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceAsync(container.Resource.Name, KnownResourceStates.Finished, cts.Token);

        var snapshot = app.Services.GetFakeLogCollector().GetSnapshot();
        Assert.True(snapshot.Any());
        Assert.EndsWith("Hello World", snapshot[1].Message);
    }

}
