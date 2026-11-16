using System.Net;
using System.Text;

namespace EciSupportCentre.Core.Tests;

/// <summary>Recorded API responses (trimmed, organiser names replaced). See Samples/README.md.</summary>
internal static class Samples
{
    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", name), Encoding.UTF8);

    public static HttpResponseMessage Json(string name) => Text(Read(name), "application/json");

    public static HttpResponseMessage Text(string body, string mediaType = "application/json", HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
}

/// <summary>Answers requests with a callback and records every request URI.</summary>
internal sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }
}

/// <summary>Answers requests in order from a queue of responses or exceptions.</summary>
internal sealed class SequenceHandler(params object[] steps) : HttpMessageHandler
{
    private readonly Queue<object> _steps = new(steps);

    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return _steps.Dequeue() switch
        {
            HttpResponseMessage response => Task.FromResult(response),
            Exception ex => Task.FromException<HttpResponseMessage>(ex),
            var other => throw new InvalidOperationException($"Bad step {other}"),
        };
    }
}
