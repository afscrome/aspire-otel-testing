using Common;

namespace AppHostPerTest
{
    public class IntegrationTest1
    {
        [Fact]
        public async Task WeatherResponds()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(cancellationToken);
            appHost.WithTestingDefaults();
            
            var app = await appHost.BuildAsync(cancellationToken);

            await app.StartAsync(cancellationToken);

            // Act
            await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken);

            using var httpClient = app.CreateHttpClient("webfrontend");
            using var response = await httpClient.GetAsync("/weather", cancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
