using CapitalTracker.Application.Holdings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

public record CreateHoldingRequest(string Name, string? Symbol, decimal? Quantity, decimal InitialValue);
public record AddValuationRequest(decimal Value, DateOnly? Date);
public record AssignSectorRequest(Guid? SectorId);
public record UpdateHoldingDetailsRequest(
    decimal? Quantity,
    string? Notes,
    Dictionary<string, string>? Attributes,
    Dictionary<string, string>? SecretAttributes);

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
            id, request.Quantity, request.Notes, request.Attributes, request.SecretAttributes)));

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
        Ok(await sender.Send(new AddValuationCommand(id, request.Value, request.Date)));

    [HttpPut("holdings/{id:guid}/sector")]
    public async Task<ActionResult<HoldingDetailDto>> AssignSector(Guid id, AssignSectorRequest request) =>
        Ok(await sender.Send(new AssignSectorCommand(id, request.SectorId)));

    [HttpDelete("holdings/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await sender.Send(new DeleteHoldingCommand(id));
        return deleted ? NoContent() : NotFound();
    }
}
