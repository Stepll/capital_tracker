using CapitalTracker.Api.Streaming;
using CapitalTracker.Application.Insights;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InsightsController(ISender sender) : ControllerBase
{
    /// <summary>The full archive of analyses, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<List<AiInsightDto>>> GetAll() =>
        Ok(await sender.Send(new GetInsightsQuery()));

    /// <summary>
    /// Analyses the whole portfolio, streaming progress as Server-Sent Events. Same
    /// mechanics as the per-holding stream — see InsightSse — including the token that
    /// makes closing the modal abort a billed run.
    /// </summary>
    [HttpPost("portfolio/stream")]
    public Task StreamPortfolio(CancellationToken cancellationToken) =>
        InsightSse.StreamAsync(
            HttpContext,
            sender.CreateStream(new StreamPortfolioInsightCommand(this.GetUserId()), cancellationToken),
            cancellationToken);
}
