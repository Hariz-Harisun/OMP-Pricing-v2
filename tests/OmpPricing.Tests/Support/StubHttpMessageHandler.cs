using System.Net;
using System.Text;

namespace OmpPricing.Tests.Support;

internal sealed record RecordedRequest(
    string Method,
    string Uri,
    IReadOnlyDictionary<string, string> Headers,
    string Body)
{
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>Stub HttpMessageHandler: queued responses, recorded requests, no network.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

    public List<RecordedRequest> Requests { get; } = [];

    public StubHttpMessageHandler Respond(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responders.Enqueue(responder);
        return this;
    }

    public StubHttpMessageHandler RespondJson(
        string json,
        HttpStatusCode status = HttpStatusCode.OK,
        Action<HttpResponseMessage>? configure = null)
        => Respond(_ =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            configure?.Invoke(response);
            return response;
        });

    public StubHttpMessageHandler RespondStatus(
        HttpStatusCode status,
        string body = "",
        Action<HttpResponseMessage>? configure = null)
        => Respond(_ =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            configure?.Invoke(response);
            return response;
        });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }
        }

        Requests.Add(new RecordedRequest(
            request.Method.Method,
            request.RequestUri?.AbsoluteUri ?? "",
            headers,
            body));

        if (_responders.Count == 0)
        {
            throw new InvalidOperationException("No stub response queued for " + request.RequestUri);
        }

        return _responders.Dequeue()(request);
    }
}
