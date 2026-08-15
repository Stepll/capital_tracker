using CapitalTracker.Application.Accounts;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

public record CreateAccountRequest(string Name, AccountType Type, string Currency);

[ApiController]
[Route("api/[controller]")]
public class AccountsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAll() =>
        Ok(await sender.Send(new GetAccountsQuery()));

    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create(CreateAccountRequest request)
    {
        var account = await sender.Send(new CreateAccountCommand(request.Name, request.Type, request.Currency));
        return CreatedAtAction(nameof(GetAll), new { id = account.Id }, account);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await sender.Send(new DeleteAccountCommand(id));
        return deleted ? NoContent() : NotFound();
    }
}
