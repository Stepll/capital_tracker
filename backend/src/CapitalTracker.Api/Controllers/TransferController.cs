using System.Text;
using CapitalTracker.Application.Transfer;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CapitalTracker.Api.Controllers;

/// <summary>
/// The file plus the decisions the owner made in the preview. Sent as multipart because the
/// file is; the options ride along as ordinary form fields.
/// </summary>
public class ImportRequest
{
    public required IFormFile File { get; init; }
    public bool SkipDuplicateRows { get; init; } = true;
    public bool ReplaceOpeningPositions { get; init; }
    public bool AddMissingOpeningPositions { get; init; }

    public ImportOptions ToOptions() =>
        new(SkipDuplicateRows, ReplaceOpeningPositions, AddMissingOpeningPositions);
}

[ApiController]
[Route("api")]
public class TransferController(ISender sender) : ControllerBase
{
    [HttpGet("export")]
    public Task<IActionResult> ExportPortfolio() => ExportAsync(TransferScope.Portfolio, null);

    [HttpGet("accounts/{id:guid}/export")]
    public Task<IActionResult> ExportAccount(Guid id) => ExportAsync(TransferScope.Account, id);

    [HttpGet("holdings/{id:guid}/export")]
    public Task<IActionResult> ExportHolding(Guid id) => ExportAsync(TransferScope.Holding, id);

    [HttpPost("import/preview")]
    public Task<ActionResult<ImportPreviewDto>> PreviewPortfolio([FromForm] ImportRequest request) =>
        PreviewAsync(request, TransferScope.Portfolio, null);

    [HttpPost("accounts/{id:guid}/import/preview")]
    public Task<ActionResult<ImportPreviewDto>> PreviewAccount(Guid id, [FromForm] ImportRequest request) =>
        PreviewAsync(request, TransferScope.Account, id);

    [HttpPost("holdings/{id:guid}/import/preview")]
    public Task<ActionResult<ImportPreviewDto>> PreviewHolding(Guid id, [FromForm] ImportRequest request) =>
        PreviewAsync(request, TransferScope.Holding, id);

    [HttpPost("import/commit")]
    public Task<ActionResult<ImportResultDto>> CommitPortfolio([FromForm] ImportRequest request) =>
        CommitAsync(request, TransferScope.Portfolio, null);

    [HttpPost("accounts/{id:guid}/import/commit")]
    public Task<ActionResult<ImportResultDto>> CommitAccount(Guid id, [FromForm] ImportRequest request) =>
        CommitAsync(request, TransferScope.Account, id);

    [HttpPost("holdings/{id:guid}/import/commit")]
    public Task<ActionResult<ImportResultDto>> CommitHolding(Guid id, [FromForm] ImportRequest request) =>
        CommitAsync(request, TransferScope.Holding, id);

    [HttpGet("imports")]
    public async Task<ActionResult<List<ImportBatchDto>>> GetImports() =>
        Ok(await sender.Send(new GetImportBatchesQuery()));

    [HttpPost("imports/{id:guid}/undo")]
    public async Task<IActionResult> UndoImport(Guid id)
    {
        var undone = await sender.Send(new UndoImportCommand(id));
        return undone ? NoContent() : NotFound();
    }

    private async Task<ActionResult<ImportPreviewDto>> PreviewAsync(
        ImportRequest request, TransferScope scope, Guid? targetId)
    {
        var file = await ReadAsync(request);
        return Ok(await sender.Send(new PreviewImportCommand(file, scope, targetId, request.ToOptions())));
    }

    private async Task<ActionResult<ImportResultDto>> CommitAsync(
        ImportRequest request, TransferScope scope, Guid? targetId)
    {
        var file = await ReadAsync(request);
        return Ok(await sender.Send(new CommitImportCommand(file, scope, targetId, request.ToOptions())));
    }

    private static async Task<ImportFile> ReadAsync(ImportRequest request)
    {
        using var stream = new MemoryStream();
        await request.File.CopyToAsync(stream);
        return new ImportFile(Path.GetFileName(request.File.FileName), stream.ToArray());
    }

    private async Task<IActionResult> ExportAsync(TransferScope scope, Guid? targetId)
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
