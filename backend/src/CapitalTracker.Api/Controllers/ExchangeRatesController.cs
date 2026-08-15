using CapitalTracker.Application.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

[ApiController]
[Route("api/exchange-rates")]
public class ExchangeRatesController(ISender sender) : ControllerBase
{
    [HttpGet("latest")]
    public async Task<ActionResult<List<ExchangeRateDto>>> GetLatest() =>
        Ok(await sender.Send(new GetLatestExchangeRatesQuery()));
}
