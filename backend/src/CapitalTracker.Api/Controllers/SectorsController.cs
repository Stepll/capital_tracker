using CapitalTracker.Application.Sectors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

public record CreateSectorRequest(string Name);

[ApiController]
[Route("api/[controller]")]
public class SectorsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SectorDto>>> GetAll() =>
        Ok(await sender.Send(new GetSectorsQuery()));

    [HttpPost]
    public async Task<ActionResult<SectorDto>> Create(CreateSectorRequest request) =>
        Ok(await sender.Send(new CreateSectorCommand(request.Name)));
}
