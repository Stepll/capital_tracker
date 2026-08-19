using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalTracker.Application.Insights;
using Microsoft.AspNetCore.Http.Features;

namespace CapitalTracker.Api.Streaming;

/// <summary>
/// Writes an insight stream to the response as Server-Sent Events. Shared by the
/// per-holding and portfolio endpoints — everything here was learned from the proxy and
/// the SDK the hard way, and is not worth rediscovering per endpoint.
/// </summary>
public static class InsightSse
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    // The AddJsonOptions configured in Program.cs applies to MVC formatters only, not to a
    // hand-rolled JsonSerializer.Serialize — so the SSE payloads have to repeat the same
    // settings, or enums would go out as integers here and as names everywhere else.
    private static readonly JsonSerializerOptions SseJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// The token must be HttpContext.RequestAborted: it is what makes closing the modal
    /// abort the (billed) model call instead of letting it run on unwatched.
    /// </summary>
    public static async Task StreamAsync(
        HttpContext context,
        IAsyncEnumerable<InsightStreamEvent> events,
        CancellationToken cancellationToken)
    {
        var response = context.Response;

        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache, no-transform";
        // Tells nginx to skip proxy buffering for this response only, which is what lets
        // the frames reach the browser as they happen without touching the VPS config.
        // (No Connection: keep-alive — it is hop-by-hop and invalid under HTTP/2.)
        response.Headers["X-Accel-Buffering"] = "no";
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        // Commit the headers before any work starts, so the client sees an open stream
        // immediately rather than after the first phase lands.
        await response.Body.FlushAsync(cancellationToken);

        try
        {
            await WriteStreamAsync(response, events, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The user closed the modal or navigated away. Expected, not an error —
            // without this every close would log an unhandled exception.
        }
    }

    private static async Task WriteStreamAsync(
        HttpResponse response,
        IAsyncEnumerable<InsightStreamEvent> events,
        CancellationToken cancellationToken)
    {
        await using var enumerator = events.GetAsyncEnumerator(cancellationToken);

        // Race each event against a timer so the connection produces traffic even during a
        // long silent search. Two reasons: nginx's default 60s proxy_read_timeout resets on
        // every read, and RequestAborted only fires once a write is attempted after the peer
        // has gone — so the ping is also what makes cancellation prompt rather than deferred
        // until the model finishes.
        var pending = enumerator.MoveNextAsync().AsTask();

        while (true)
        {
            // No token on the delay: when an event wins the race the timer is abandoned,
            // and a cancelled one would fault unobserved every iteration. Letting it run
            // out harmlessly is cheaper than the plumbing to cancel it cleanly. Aborts
            // still exit the loop — the writes below carry the token.
            var heartbeat = Task.Delay(HeartbeatInterval);

            if (await Task.WhenAny(pending, heartbeat) != pending)
            {
                await WriteRawAsync(response, ": ping\n\n", cancellationToken);
                continue;
            }

            if (!await pending)
            {
                break;
            }

            await WriteEventAsync(response, enumerator.Current, cancellationToken);
            pending = enumerator.MoveNextAsync().AsTask();
        }
    }

    private static async Task WriteEventAsync(
        HttpResponse response, InsightStreamEvent e, CancellationToken cancellationToken)
    {
        var name = e.Type switch
        {
            InsightStreamEventType.Completed => "completed",
            InsightStreamEventType.Failed => "failed",
            _ => "phase",
        };

        await WriteRawAsync(
            response, $"event: {name}\ndata: {JsonSerializer.Serialize(e, SseJson)}\n\n", cancellationToken);
    }

    private static async Task WriteRawAsync(
        HttpResponse response, string frame, CancellationToken cancellationToken)
    {
        await response.WriteAsync(frame, cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
