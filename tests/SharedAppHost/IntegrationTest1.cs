using SharedAppHost;

[assembly: AssemblyFixture(typeof(AppHostFixture))]

namespace SharedAppHost;

public class IntegrationTest1(AppHostFixture appHostFixture)
{
    [Fact]
    public async Task WeatherResponds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
    
        // Act
        await appHostFixture.App.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken);

        using var httpClient = appHostFixture.App.CreateHttpClient("webfrontend");
        using var response = await httpClient.GetAsync("/weather", cancellationToken);
   
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
