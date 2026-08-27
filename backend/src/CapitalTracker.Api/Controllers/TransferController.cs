using System.Text;
using CapitalTracker.Application.Transfer;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

[ApiController]
[Route("api")]
public class TransferController(ISender sender) : ControllerBase
{
    [HttpGet("export")]
    public Task<IActionResult> ExportPortfolio() => ExportAsync(ExportScope.Portfolio, null);

    [HttpGet("accounts/{id:guid}/export")]
    public Task<IActionResult> ExportAccount(Guid id) => ExportAsync(ExportScope.Account, id);

    [HttpGet("holdings/{id:guid}/export")]
    public Task<IActionResult> ExportHolding(Guid id) => ExportAsync(ExportScope.Holding, id);

    private async Task<IActionResult> ExportAsync(ExportScope scope, Guid? targetId)
    {
        var file = await sender.Send(new ExportCsvQuery(scope, targetId));
        if (file is null)
            return NotFound();

        // BOM first: without it Excel reads the Cyrillic headers as mojibake, which is the
        // whole reason this file is written the way it is.
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(file.Content))
            .ToArray();

        return File(bytes, "text/csv; charset=utf-8", file.FileName);
    }
}
