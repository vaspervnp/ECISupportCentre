using System.Globalization;
using System.Net;

namespace EciSupportCentre.Core.Api;

/// <summary>
/// Retries GET requests on transient failures: network errors, timeouts, 408, 429 and 5xx.
/// Honours <c>Retry-After</c>, which the ECI servers send with fractional seconds (e.g. <c>0.809</c>).
/// </summary>
public sealed class RetryHandler : DelegatingHandler
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public RetryHandler(Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _delay = delay ?? Task.Delay;
    }

    public RetryHandler(HttpMessageHandler innerHandler, Func<TimeSpan, CancellationToken, Task>? delay = null)
        : base(innerHandler)
    {
        _delay = delay ?? Task.Delay;
    }

    /// <summary>Retries after the first attempt.</summary>
    public int MaxRetries { get; init; } = 3;

    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Longest wait between attempts, even if the server asks for more.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gives up on one attempt after this long (until its response headers) and retries; null waits forever.</summary>
    public TimeSpan? AttemptTimeout { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (AttemptTimeout is { } limit)
            {
                attemptCts.CancelAfter(limit);
            }

            try
            {
                response = await base.SendAsync(request, attemptCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (attemptCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                                                     && attempt >= MaxRetries)
            {
                // An HttpRequestException, so callers treat it like any other network failure.
                throw new HttpRequestException(
                    $"No response from {request.RequestUri} after {MaxRetries + 1} attempts.",
                    new TimeoutException());
            }
            catch (Exception ex) when (attempt < MaxRetries && IsTransient(ex, cancellationToken))
            {
                await _delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (attempt >= MaxRetries || !IsTransient(response.StatusCode))
            {
                return response;
            }

            var wait = RetryAfter(response) ?? Backoff(attempt);
            response.Dispose();
            await _delay(wait > MaxDelay ? MaxDelay : wait, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    private static bool IsTransient(Exception ex, CancellationToken cancellationToken) => ex switch
    {
        HttpRequestException => true,

        // HttpClient.Timeout and socket timeouts surface as cancellations the caller did not ask for.
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        IOException => true,
        _ => false,
    };

    private TimeSpan Backoff(int attempt)
    {
        var ms = BaseDelay.TotalMilliseconds * Math.Pow(2, attempt);
        ms += Random.Shared.NextDouble() * BaseDelay.TotalMilliseconds / 2;
        return TimeSpan.FromMilliseconds(Math.Min(ms, MaxDelay.TotalMilliseconds));
    }

    /// <summary>Reads Retry-After as seconds (fractions allowed) or as an HTTP date.</summary>
    internal static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Retry-After", out var values))
        {
            return null;
        }

        var raw = values.FirstOrDefault()?.Trim();
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when))
        {
            var wait = when - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }
}
