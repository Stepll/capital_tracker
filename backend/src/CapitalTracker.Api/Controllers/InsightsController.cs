using CapitalTracker.Application.Insights;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

public record GenerateInsightRequest(Guid SectorId);

[ApiController]
[Route("api/[controller]")]
public class InsightsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AiInsightDto>>> GetAll() =>
        Ok(await sender.Send(new GetInsightsQuery()));

    [HttpPost("generate")]
    public async Task<ActionResult<AiInsightDto>> Generate(GenerateInsightRequest request) =>
        Ok(await sender.Send(new GenerateInsightCommand(request.SectorId)));
}
