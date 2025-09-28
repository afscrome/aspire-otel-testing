using Common;
using Microsoft.Extensions.Logging;

namespace AppHostPerTest.Tests
{
    public class IntegrationTest1
    {
        [Fact]
        public async Task WeatherResponds()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(cancellationToken);
            appHost.WithCILogging();
            
            var app = await appHost.BuildAsync(cancellationToken);

            await app.StartWithLoggingAsync(cancellationToken);

            // Act
            await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken);

            using var httpClient = app.CreateHttpClient("webfrontend");
            using var response = await httpClient.GetAsync("/weather", cancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
