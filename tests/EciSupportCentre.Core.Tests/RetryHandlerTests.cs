using System.Net;
using EciSupportCentre.Core.Api;

namespace EciSupportCentre.Core.Tests;

public class RetryHandlerTests
{
    private readonly List<TimeSpan> _delays = [];

    private HttpClient Create(HttpMessageHandler inner, int maxRetries = 3) =>
        new(new RetryHandler(inner, (d, _) => { _delays.Add(d); return Task.CompletedTask; }) { MaxRetries = maxRetries });

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    [Fact]
    public async Task Too_many_requests_honours_fractional_Retry_After()
    {
        var throttled = Status(HttpStatusCode.TooManyRequests);
        throttled.Headers.TryAddWithoutValidation("Retry-After", "0.809");
        var inner = new SequenceHandler(throttled, Status(HttpStatusCode.OK));
        using var client = Create(inner);

        using var response = await client.GetAsync(new Uri("https://example.test/a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls);
        Assert.Equal([TimeSpan.FromMilliseconds(809)], _delays);
    }

    [Fact]
    public async Task Server_errors_are_retried_then_returned()
    {
        var inner = new SequenceHandler(
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.BadGateway),
            Status(HttpStatusCode.InternalServerError),
            Status(HttpStatusCode.InternalServerError));
        using var client = Create(inner);

        using var response = await client.GetAsync(new Uri("https://example.test/a"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(4, inner.Calls);
        Assert.Equal(3, _delays.Count);
        Assert.True(_delays[1] > _delays[0], "Back-off grows");
    }

    [Fact]
    public async Task Client_errors_are_not_retried()
    {
        var inner = new SequenceHandler(Status(HttpStatusCode.NotFound));
        using var client = Create(inner);

        using var response = await client.GetAsync(new Uri("https://example.test/a"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, inner.Calls);
        Assert.Empty(_delays);
    }

    [Fact]
    public async Task Network_errors_are_retried()
    {
        var inner = new SequenceHandler(new HttpRequestException("reset"), Status(HttpStatusCode.OK));
        using var client = Create(inner);

        using var response = await client.GetAsync(new Uri("https://example.test/a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Network_errors_surface_after_the_last_retry()
    {
        var inner = new SequenceHandler(new HttpRequestException("1"), new HttpRequestException("2"));
        using var client = Create(inner, maxRetries: 1);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri("https://example.test/a")));
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task A_slow_attempt_times_out_and_is_retried()
    {
        var inner = new HangOnceHandler();
        using var client = new HttpClient(new RetryHandler(inner, (_, _) => Task.CompletedTask)
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(100),
        });

        using var response = await client.GetAsync(new Uri("https://example.test/a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Attempts_that_all_time_out_end_in_a_timeout()
    {
        var inner = new HangOnceHandler { HangForever = true };
        using var client = new HttpClient(new RetryHandler(inner, (_, _) => Task.CompletedTask)
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(50),
            MaxRetries = 2,
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri("https://example.test/a")));
        Assert.IsType<TimeoutException>(ex.InnerException);
        Assert.Equal(3, inner.Calls);
    }

    /// <summary>Never answers the first request (or any, with HangForever) until it is cancelled; then answers 200.</summary>
    private sealed class HangOnceHandler : HttpMessageHandler
    {
        public bool HangForever { get; init; }

        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (++Calls == 1 || HangForever)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Caller_cancellation_is_not_retried()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var inner = new SequenceHandler(new OperationCanceledException(cts.Token));
        using var client = Create(inner);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync(new Uri("https://example.test/a"), cts.Token));
        Assert.Empty(_delays);
    }

    [Fact]
    public async Task Posts_are_not_retried()
    {
        var inner = new SequenceHandler(Status(HttpStatusCode.ServiceUnavailable));
        using var client = Create(inner);
        using var content = new StringContent("x");

        using var response = await client.PostAsync(new Uri("https://example.test/a"), content);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Long_Retry_After_is_capped()
    {
        var throttled = Status(HttpStatusCode.TooManyRequests);
        throttled.Headers.TryAddWithoutValidation("Retry-After", "3600");
        var inner = new SequenceHandler(throttled, Status(HttpStatusCode.OK));
        using var client = Create(inner);

        using var response = await client.GetAsync(new Uri("https://example.test/a"));

        Assert.Equal([TimeSpan.FromSeconds(30)], _delays);
    }
}
