using System.Net;

namespace JevSharp.Tests.Protocols;

/// <summary>Exercises failures after HTTP response headers have already arrived.</summary>
public sealed class ResponseStreamTests
{
    /// <summary>A stalled error body must not turn an HTTP 401 into a retryable timeout.</summary>
    [Fact]
    public async Task Permanent_status_survives_error_body_timeout()
    {
        var clock = new RecordingTimeProvider();
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.TimeProvider = clock;
        options.AttemptTimeout = TimeSpan.FromSeconds(2);
        using (var stream = new FailingReadStream(waitForCancellation: true))
        {
            using (var handler = new StubHandler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StreamContent(stream)
            })))
            {
                using (var http = new HttpClient(handler))
                {
                    using (var client = new JevClient(options, http))
                    {
                        var pending = client.EvaluateAsync(Fixtures.One());
                        await Fixtures.EventuallyAsync(() => stream.ReadStarted);
                        clock.Advance(TimeSpan.FromSeconds(2));
                        var error = await Assert.ThrowsAsync<JevAuthenticationException>(() => pending);
                        Assert.Null(error.Details);
                        Assert.Single(handler.Requests);
                        Assert.True(stream.WasDisposed);
                    }
                }
            }
        }
    }

    /// <summary>The configured timeout covers reading successful response bodies, not only headers.</summary>
    [Fact]
    public async Task Successful_response_body_is_covered_by_timeout()
    {
        var clock = new RecordingTimeProvider();
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.TimeProvider = clock;
        options.AttemptTimeout = TimeSpan.FromSeconds(2);
        options.Retry.MaxAttempts = 1;
        using (var stream = new FailingReadStream(waitForCancellation: true))
        {
            using (var handler = new StubHandler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            })))
            {
                using (var http = new HttpClient(handler))
                {
                    using (var client = new JevClient(options, http))
                    {
                        var pending = client.EvaluateAsync(Fixtures.One());
                        await Fixtures.EventuallyAsync(() => stream.ReadStarted);
                        clock.Advance(TimeSpan.FromSeconds(2));
                        await Assert.ThrowsAsync<JevTimeoutException>(() => pending);
                        Assert.True(stream.WasDisposed);
                    }
                }
            }
        }
    }

    /// <summary>A broken success stream is classified as transient without exposing raw exception text.</summary>
    [Fact]
    public async Task Broken_response_stream_retries_then_disposes()
    {
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.Retry.Delay = TimeSpan.Zero;
        var streams = new List<FailingReadStream>();
        using (var handler = new StubHandler((_, _, _) =>
        {
            var stream = new FailingReadStream(waitForCancellation: false);
            streams.Add(stream);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });
        }))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    var error = await Assert.ThrowsAsync<JevTransportException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.True(error.IsTransient);
                    Assert.Equal(3, error.Attempts);
                    Assert.Equal(3, streams.Count);
                    Assert.All(streams, stream => Assert.True(stream.WasDisposed));
                    Assert.DoesNotContain("sensitive", error.ToString());
                }
            }
        }
    }
}
