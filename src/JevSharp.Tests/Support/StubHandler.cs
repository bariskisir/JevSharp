using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace JevSharp.Tests.Support;

/// <summary>A deterministic HTTP transport with captured requests.</summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> respond;
    private int attempts;
    internal ConcurrentQueue<RequestCapture> Requests { get; } = new();
    internal bool Disposed { get; private set; }

    /// <summary>Configures a programmable response function.</summary>
    internal StubHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        this.respond = respond;
    }

    /// <summary>Configures a fixed JSON response.</summary>
    internal StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        : this((_, _, _) => Task.FromResult(Json(body, status)))
    {
    }

    /// <summary>Captures the request before invoking the response function.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var number = Interlocked.Increment(ref attempts);
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        Requests.Enqueue(new(request.RequestUri, request.Method.Method, body,
            request.Headers.Concat(request.Content.Headers).ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase)));
        return await respond(request, number, cancellationToken);
    }

    /// <summary>Tracks transport ownership.</summary>
    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }

    /// <summary>Creates a JSON HTTP response with a correlation ID.</summary>
    internal static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        response.Headers.Add("x-request-id", "request-42");
        return response;
    }
}
