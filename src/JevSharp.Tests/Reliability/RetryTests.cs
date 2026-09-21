using System.Net;

namespace JevSharp.Tests.Reliability;

/// <summary>Verifies attempt counting, immutable retry payloads, cancellation, and timeout behavior.</summary>
public sealed class RetryTests
{
    /// <summary>Defaults send the entire payload three times with two exact five-second waits.</summary>
    [Fact]
    public async Task Defaults_are_three_total_attempts_with_five_second_delays()
    {
        var clock = new RecordingTimeProvider();
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.TimeProvider = clock;
        var request = Fixtures.Mixed();
        using (var handler = new StubHandler((_, attempt, _) => Task.FromResult(StubHandler.Json(
            attempt < 3 ? "busy" : Fixtures.Standard, attempt < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK))))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    var pending = client.EvaluateAsync(request);
                    await Fixtures.EventuallyAsync(() => clock.Scheduled.Count(x => x == TimeSpan.FromSeconds(5)) == 1);
                    ((Dictionary<string, JevQuestion>)request.Questions).Clear();
                    clock.Advance(TimeSpan.FromSeconds(4));
                    Assert.Single(handler.Requests);
                    clock.Advance(TimeSpan.FromSeconds(1));
                    await Fixtures.EventuallyAsync(() => clock.Scheduled.Count(x => x == TimeSpan.FromSeconds(5)) == 2);
                    Assert.Equal(2, handler.Requests.Count);
                    clock.Advance(TimeSpan.FromSeconds(5));
                    var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Equal(4, result.Answers.Count);
                    Assert.Equal(3, handler.Requests.Count);
                    Assert.Single(handler.Requests.Select(x => x.Body).Distinct());
                    Assert.Equal(2, clock.Scheduled.Count(x => x == TimeSpan.FromSeconds(5)));
                }
            }
        }
    }

    /// <summary>All supported transient statuses honor configured attempt counts.</summary>
    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    [InlineData(524)]
    [InlineData(529)]
    public async Task Transient_statuses_exhaust_configured_attempts(int status)
    {
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.Retry.MaxAttempts = 4;
        options.Retry.Delay = TimeSpan.Zero;
        using (var handler = new StubHandler("busy", (HttpStatusCode)status))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    var error = await Assert.ThrowsAnyAsync<JevApiException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Equal(4, error.Attempts);
                    Assert.Equal(4, handler.Requests.Count);
                    if (status == 429)
                    {
                        Assert.IsType<JevRateLimitException>(error);
                    }
                }
            }
        }
    }

    /// <summary>One total attempt disables retries.</summary>
    [Fact]
    public async Task One_attempt_disables_retries()
    {
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.Retry.MaxAttempts = 1;
        using (var handler = new StubHandler("busy", HttpStatusCode.ServiceUnavailable))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    await Assert.ThrowsAsync<JevApiException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Single(handler.Requests);
                }
            }
        }
    }

    /// <summary>Retry-After only extends delays when explicitly enabled, for both supported formats.</summary>
    [Theory]
    [InlineData(false, false, 2)]
    [InlineData(true, false, 7)]
    [InlineData(true, true, 7)]
    public async Task Retry_after_is_opt_in(bool respect, bool dateHeader, int seconds)
    {
        var clock = new RecordingTimeProvider();
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.TimeProvider = clock;
        options.Retry.Delay = TimeSpan.FromSeconds(2);
        options.Retry.RespectRetryAfter = respect;
        using (var handler = new StubHandler((_, attempt, _) =>
        {
            var response = StubHandler.Json(attempt == 1 ? "busy" : Fixtures.Single,
                attempt == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
            response.Headers.RetryAfter = dateHeader ? new(clock.GetUtcNow().AddSeconds(7)) : new(TimeSpan.FromSeconds(7));
            return Task.FromResult(response);
        }))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    var task = client.EvaluateAsync(Fixtures.One());
                    await Fixtures.EventuallyAsync(() => clock.Scheduled.Contains(TimeSpan.FromSeconds(seconds)));
                    clock.Advance(TimeSpan.FromSeconds(seconds));
                    await task.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Equal(2, handler.Requests.Count);
                }
            }
        }
    }

    /// <summary>Caller cancellation stops retry waits without making another HTTP call.</summary>
    [Fact]
    public async Task Cancellation_interrupts_retry_delay()
    {
        var clock = new RecordingTimeProvider();
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.TimeProvider = clock;
        using (var cancellation = new CancellationTokenSource())
        {
            using (var handler = new StubHandler("busy", HttpStatusCode.ServiceUnavailable))
            {
                using (var http = new HttpClient(handler))
                {
                    using (var client = new JevClient(options, http))
                    {
                        var pending = client.EvaluateAsync(Fixtures.One(), cancellation.Token);
                        await Fixtures.EventuallyAsync(() => clock.Scheduled.Contains(TimeSpan.FromSeconds(5)));
                        cancellation.Cancel();
                        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                        Assert.Equal(cancellation.Token, error.CancellationToken);
                        Assert.Single(handler.Requests);
                    }
                }
            }
        }
    }

    /// <summary>Cancellation in flight is preserved as cancellation rather than retried as a timeout.</summary>
    [Fact]
    public async Task Caller_cancellation_interrupts_http()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            using (var handler = new StubHandler(async (_, _, token) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return StubHandler.Json(Fixtures.Single);
                }))
            {
                using (var http = new HttpClient(handler))
                {
                    using (var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                    {
                        var pending = client.EvaluateAsync(Fixtures.One(), cancellation.Token);
                        cancellation.Cancel();
                        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                        Assert.Equal(cancellation.Token, error.CancellationToken);
                        Assert.Single(handler.Requests);
                    }
                }
            }
        }
    }

    /// <summary>Attempt timeouts are controlled by the supplied clock and have a distinct exception.</summary>
    [Fact]
    public async Task Attempt_timeout_is_distinct_from_caller_cancellation()
    {
        var clock = new RecordingTimeProvider();
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.TimeProvider = clock;
        options.AttemptTimeout = TimeSpan.FromSeconds(3);
        options.Retry.MaxAttempts = 2;
        options.Retry.Delay = TimeSpan.Zero;
        using (var handler = new StubHandler(async (_, _, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return StubHandler.Json(Fixtures.Single);
        }))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    var pending = client.EvaluateAsync(Fixtures.One());
                    clock.Advance(TimeSpan.FromSeconds(3));
                    await Fixtures.EventuallyAsync(() => handler.Requests.Count == 2);
                    clock.Advance(TimeSpan.FromSeconds(3));
                    var error = await Assert.ThrowsAsync<JevTimeoutException>(() => pending);
                    Assert.Equal(2, error.Attempts);
                }
            }
        }
    }

    /// <summary>Transient connection failures retry while TLS failures fail immediately.</summary>
    [Theory]
    [InlineData(HttpRequestError.ConnectionError, 3)]
    [InlineData(HttpRequestError.NameResolutionError, 3)]
    [InlineData(HttpRequestError.SecureConnectionError, 1)]
    public async Task Transport_errors_are_classified(HttpRequestError kind, int attempts)
    {
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.Retry.Delay = TimeSpan.Zero;
        using (var handler = new StubHandler((_, _, _) => throw new HttpRequestException(kind, "sensitive-error-details")))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    var error = await Assert.ThrowsAsync<JevTransportException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Equal(attempts, error.Attempts);
                    Assert.Equal(attempts, handler.Requests.Count);
                    Assert.DoesNotContain("sensitive-error-details", error.ToString());
                }
            }
        }
    }
}
