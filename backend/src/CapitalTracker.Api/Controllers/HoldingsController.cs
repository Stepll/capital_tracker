using CapitalTracker.Application.Holdings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

public record CreateHoldingRequest(string Name, string? Symbol, decimal InitialValue);

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
            new CreateHoldingCommand(accountId, request.Name, request.Symbol, request.InitialValue));
        return CreatedAtAction(nameof(GetByAccount), new { accountId }, holding);
    }

    [HttpDelete("holdings/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await sender.Send(new DeleteHoldingCommand(id));
        return deleted ? NoContent() : NotFound();
    }
}
