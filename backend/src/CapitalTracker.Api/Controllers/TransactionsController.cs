using CapitalTracker.Application.Transactions;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

/// <summary>
/// Currency is optional: omitted, the transaction inherits whatever the holding is already
/// denominated in, the same rule valuations follow.
/// </summary>
public record SaveTransactionRequest(
    TransactionType Type,
    DateOnly Date,
    decimal Quantity,
    decimal UnitPrice,
    string? Currency,
    string? Notes);

[ApiController]
[Route("api")]
public class TransactionsController(ISender sender) : ControllerBase
{
    [HttpGet("holdings/{holdingId:guid}/transactions")]
    public async Task<ActionResult<List<TransactionDto>>> GetByHolding(Guid holdingId) =>
        Ok(await sender.Send(new GetHoldingTransactionsQuery(holdingId)));

    [HttpPost("holdings/{holdingId:guid}/transactions")]
    public async Task<ActionResult<TransactionDto>> Create(Guid holdingId, SaveTransactionRequest request)
    {
        var transaction = await sender.Send(new AddTransactionCommand(
            holdingId, request.Type, request.Date, request.Quantity, request.UnitPrice,
            request.Currency, request.Notes));
        return CreatedAtAction(nameof(GetByHolding), new { holdingId }, transaction);
    }

    [HttpGet("accounts/{accountId:guid}/transactions")]
    public async Task<ActionResult<List<TransactionDto>>> GetByAccount(Guid accountId) =>
        Ok(await sender.Send(new GetAccountTransactionsQuery(accountId)));

    [HttpPut("transactions/{id:guid}")]
    public async Task<ActionResult<TransactionDto>> Update(Guid id, SaveTransactionRequest request) =>
        Ok(await sender.Send(new UpdateTransactionCommand(
            id, request.Type, request.Date, request.Quantity, request.UnitPrice,
            request.Currency, request.Notes)));

    [HttpDelete("transactions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await sender.Send(new DeleteTransactionCommand(id));
        return deleted ? NoContent() : NotFound();
    }
}
