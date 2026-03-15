using System.Net;
using DotPilot.Core.HttpDiagnostics;

namespace DotPilot.Tests.HttpDiagnostics;

public class DebugHttpHandlerTests
{
    [Test]
    public async Task DebugHttpHandlerReturnsSuccessfulResponsesWithoutMutation()
    {
        using var handler = new DebugHttpHandler(new StubHttpMessageHandler(HttpStatusCode.OK));
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("https://example.test/runtime");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task DebugHttpHandlerReturnsFailedResponsesAndReadsRequestDetails()
    {
        using var handler = new DebugHttpHandler(new StubHttpMessageHandler(HttpStatusCode.BadRequest));
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/runtime")
        {
            Content = new StringContent("runtime payload"),
        };

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.RequestMessage.Should().NotBeNull();
    }

    [Test]
    public async Task DebugHttpHandlerReturnsFailedResponsesWithoutRequestContent()
    {
        using var handler = new DebugHttpHandler(new StubHttpMessageHandler(HttpStatusCode.InternalServerError));
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "https://example.test/runtime");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.RequestMessage.Should().BeSameAs(request);
    }

    [Test]
    public async Task DebugHttpHandlerReturnsFailedResponsesWhenRequestContentIsWhitespace()
    {
        using var handler = new DebugHttpHandler(new StubHttpMessageHandler(HttpStatusCode.Unauthorized));
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://example.test/runtime")
        {
            Content = new StringContent("   "),
        };

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.RequestMessage.Should().BeSameAs(request);
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
            });
        }
    }
}
