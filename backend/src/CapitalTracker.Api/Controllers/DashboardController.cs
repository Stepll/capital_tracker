using CapitalTracker.Application.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(ISender sender) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary() =>
        Ok(await sender.Send(new GetDashboardSummaryQuery(this.GetUserId())));
}
