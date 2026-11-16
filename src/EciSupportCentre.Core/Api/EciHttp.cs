using System.Net;
using System.Net.Http.Headers;

namespace EciSupportCentre.Core.Api;

/// <summary>Builds the HttpClient used for the ECI services.</summary>
public static class EciHttp
{
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates a client with retries, compression and a product User-Agent.
    /// </summary>
    /// <param name="innerHandler">Platform handler (e.g. Android's); defaults to <see cref="SocketsHttpHandler"/>.</param>
    /// <param name="appVersion">Version string for the User-Agent.</param>
    public static HttpClient CreateClient(HttpMessageHandler? innerHandler = null, string appVersion = "0.1")
    {
        innerHandler ??= new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        var client = new HttpClient(new RetryHandler(innerHandler), disposeHandler: true)
        {
            Timeout = DefaultTimeout,
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EciSupportCentre", appVersion));
        return client;
    }
}

/// <summary>An ECI service answered with an error.</summary>
public sealed class EciApiException : Exception
{
    public EciApiException()
    {
    }

    public EciApiException(string message)
        : base(message)
    {
    }

    public EciApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EciApiException(HttpStatusCode statusCode, Uri? requestUri)
        : base($"ECI service returned {(int)statusCode} {statusCode} for {requestUri}.")
    {
        StatusCode = statusCode;
        RequestUri = requestUri;
    }

    public HttpStatusCode? StatusCode { get; }

    public Uri? RequestUri { get; }
}
