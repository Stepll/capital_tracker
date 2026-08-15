using CapitalTracker.Application.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

public record UpdateSettingsRequest(string DisplayCurrency);

[ApiController]
[Route("api/[controller]")]
public class SettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SettingsDto>> Get() =>
        Ok(await sender.Send(new GetSettingsQuery(this.GetUserId())));

    [HttpPut]
    public async Task<ActionResult<SettingsDto>> Update(UpdateSettingsRequest request)
    {
        try
        {
            var settings = await sender.Send(
                new UpdateDisplayCurrencyCommand(this.GetUserId(), request.DisplayCurrency));
            return Ok(settings);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
