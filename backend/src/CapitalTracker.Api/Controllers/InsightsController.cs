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
}
