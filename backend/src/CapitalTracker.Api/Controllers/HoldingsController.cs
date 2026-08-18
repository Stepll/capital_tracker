using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalTracker.Application.Holdings;
using CapitalTracker.Application.Insights;
using MediatR;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

public record CreateHoldingRequest(string Name, string? Symbol, decimal? Quantity, decimal InitialValue);
public record AddValuationRequest(decimal Value, DateOnly? Date, string? Currency);
public record AssignSectorRequest(Guid? SectorId);
public record UpdateHoldingDetailsRequest(
    decimal? Quantity,
    string? Notes,
    Dictionary<string, string>? Attributes,
    Dictionary<string, string>? SecretAttributes,
    bool? ExcludeFromAiAnalysis);

[ApiController]
[Route("api")]
public class HoldingsController(ISender sender) : ControllerBase
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    // The AddJsonOptions configured in Program.cs applies to MVC formatters only, not to a
    // hand-rolled JsonSerializer.Serialize — so the SSE payloads have to repeat the same
    // settings, or enums would go out as integers here and as names everywhere else.
    private static readonly JsonSerializerOptions SseJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [HttpGet("accounts/{accountId:guid}/holdings")]
    public async Task<ActionResult<List<HoldingDto>>> GetByAccount(Guid accountId) =>
        Ok(await sender.Send(new GetHoldingsByAccountQuery(accountId)));

    [HttpPost("accounts/{accountId:guid}/holdings")]
    public async Task<ActionResult<HoldingDto>> Create(Guid accountId, CreateHoldingRequest request)
    {
        var holding = await sender.Send(
            new CreateHoldingCommand(accountId, request.Name, request.Symbol, request.Quantity, request.InitialValue));
        return CreatedAtAction(nameof(GetByAccount), new { accountId }, holding);
    }

    [HttpGet("holdings/{id:guid}")]
    public async Task<ActionResult<HoldingDetailDto>> GetById(Guid id)
    {
        var holding = await sender.Send(new GetHoldingByIdQuery(id));
        return holding is null ? NotFound() : Ok(holding);
    }

    [HttpPut("holdings/{id:guid}/details")]
    public async Task<ActionResult<HoldingDetailDto>> UpdateDetails(Guid id, UpdateHoldingDetailsRequest request) =>
        Ok(await sender.Send(new UpdateHoldingDetailsCommand(
            id, request.Quantity, request.Notes, request.Attributes, request.SecretAttributes,
            request.ExcludeFromAiAnalysis)));

    [HttpGet("holdings/{id:guid}/secrets/{key}")]
    public async Task<ActionResult<object>> RevealSecret(Guid id, string key)
    {
        var value = await sender.Send(new RevealSecretAttributeQuery(id, key));
        return value is null ? NotFound() : Ok(new { value });
    }

    [HttpDelete("holdings/{id:guid}/secrets/{key}")]
    public async Task<ActionResult<HoldingDetailDto>> DeleteSecret(Guid id, string key) =>
        Ok(await sender.Send(new DeleteSecretAttributeCommand(id, key)));

    [HttpPost("holdings/{id:guid}/valuations")]
    public async Task<ActionResult<HoldingDetailDto>> AddValuation(Guid id, AddValuationRequest request) =>
        Ok(await sender.Send(new AddValuationCommand(id, request.Value, request.Date, request.Currency)));

    [HttpPut("holdings/{id:guid}/sector")]
    public async Task<ActionResult<HoldingDetailDto>> AssignSector(Guid id, AssignSectorRequest request) =>
        Ok(await sender.Send(new AssignSectorCommand(id, request.SectorId)));

    [HttpGet("holdings/{id:guid}/insights")]
    public async Task<ActionResult<List<AiInsightDto>>> GetInsights(Guid id) =>
        Ok(await sender.Send(new GetHoldingInsightsQuery(id)));

    /// <summary>
    /// Generates an analysis, streaming progress as Server-Sent Events. Returns Task rather
    /// than ActionResult because the body is written by hand.
    ///
    /// This is the only action here that takes a CancellationToken, and deliberately so:
    /// bound to HttpContext.RequestAborted, it is what makes closing the modal actually
    /// abort the (billed) model call instead of letting it run on unwatched.
    /// </summary>
    [HttpPost("holdings/{id:guid}/insights/stream")]
    public async Task StreamInsight(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";
        // Tells nginx to skip proxy buffering for this response only, which is what lets
        // the frames reach the browser as they happen without touching the VPS config.
        // (No Connection: keep-alive — it is hop-by-hop and invalid under HTTP/2.)
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        // Commit the headers before any work starts, so the client sees an open stream
        // immediately rather than after the first phase lands.
        await Response.Body.FlushAsync(cancellationToken);

        try
        {
            await WriteEventStreamAsync(id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The user closed the modal or navigated away. Expected, not an error —
            // without this every close would log an unhandled exception.
        }
    }

    private async Task WriteEventStreamAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var events = sender
            .CreateStream(new StreamHoldingInsightCommand(id), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        // Race each event against a timer so the connection produces traffic even during a
        // long silent search. Two reasons: nginx's default 60s proxy_read_timeout resets on
        // every read, and RequestAborted only fires once a write is attempted after the peer
        // has gone — so the ping is also what makes cancellation prompt rather than deferred
        // until the model finishes.
        var pending = events.MoveNextAsync().AsTask();

        while (true)
        {
            // No token on the delay: when an event wins the race the timer is abandoned,
            // and a cancelled one would fault unobserved every iteration. Letting it run
            // out harmlessly is cheaper than the plumbing to cancel it cleanly. Aborts
            // still exit the loop — the writes below carry the token.
            var heartbeat = Task.Delay(HeartbeatInterval);

            if (await Task.WhenAny(pending, heartbeat) != pending)
            {
                await WriteRawAsync(": ping\n\n", cancellationToken);
                continue;
            }

            if (!await pending)
            {
                break;
            }

            await WriteEventAsync(events.Current, cancellationToken);
            pending = events.MoveNextAsync().AsTask();
        }
    }

    private async Task WriteEventAsync(InsightStreamEvent e, CancellationToken cancellationToken)
    {
        var name = e.Type switch
        {
            InsightStreamEventType.Completed => "completed",
            InsightStreamEventType.Failed => "failed",
            _ => "phase",
        };

        await WriteRawAsync($"event: {name}\ndata: {JsonSerializer.Serialize(e, SseJson)}\n\n", cancellationToken);
    }

    private async Task WriteRawAsync(string frame, CancellationToken cancellationToken)
    {
        await Response.WriteAsync(frame, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    [HttpDelete("holdings/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await sender.Send(new DeleteHoldingCommand(id));
        return deleted ? NoContent() : NotFound();
    }
}
