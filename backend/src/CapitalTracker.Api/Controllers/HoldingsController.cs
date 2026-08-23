using CapitalTracker.Api.Streaming;
using CapitalTracker.Application.Holdings;
using CapitalTracker.Application.Insights;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

public record CreateHoldingRequest(string Name, string? Symbol, decimal? Quantity, decimal InitialValue);
public record AddValuationRequest(decimal Value, DateOnly? Date, string? Currency);
public record AssignSectorRequest(Guid? SectorId);
public record UpdateHoldingDetailsRequest(
    string? Notes,
    Dictionary<string, string>? Attributes,
    Dictionary<string, string>? SecretAttributes,
    bool? ExcludeFromAiAnalysis);

[ApiController]
[Route("api")]
public class HoldingsController(ISender sender) : ControllerBase
{
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
            id, request.Notes, request.Attributes, request.SecretAttributes,
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
    public Task StreamInsight(Guid id, CancellationToken cancellationToken) =>
        InsightSse.StreamAsync(
            HttpContext,
            sender.CreateStream(new StreamHoldingInsightCommand(id), cancellationToken),
            cancellationToken);

    [HttpDelete("holdings/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await sender.Send(new DeleteHoldingCommand(id));
        return deleted ? NoContent() : NotFound();
    }
}
