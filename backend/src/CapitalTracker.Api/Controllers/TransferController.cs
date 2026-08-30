using System.Text;
using System.Text.Json;
using CapitalTracker.Application.Common;
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

    /// <summary>
    /// How a foreign statement lines up with our columns, as JSON — a form field rather than
    /// a body, because the file it describes is going up as multipart alongside it. Absent
    /// when the file is already in our own format.
    /// </summary>
    public string? Mapping { get; init; }

    public ImportOptions ToOptions() =>
        new(SkipDuplicateRows, ReplaceOpeningPositions, AddMissingOpeningPositions);

    public ImportMapping? ToMapping() =>
        string.IsNullOrWhiteSpace(Mapping)
            ? null
            : JsonSerializer.Deserialize<ImportMapping>(Mapping, JsonOptions)
              ?? throw new DomainValidationException("Не вдалося прочитати зіставлення колонок.");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// The header goes up raw and is normalised server-side, so the signature a profile is
/// saved under can never drift from the one an incoming file is matched by.
/// </summary>
public record SaveProfileRequest(string Name, string Mapping, string[] Header);

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

    [HttpPost("import/inspect")]
    public async Task<ActionResult<FileInspectionDto>> Inspect([FromForm] ImportRequest request) =>
        Ok(await sender.Send(new InspectImportQuery(await ReadAsync(request))));

    [HttpGet("import/profiles")]
    public async Task<ActionResult<List<ImportProfileDto>>> GetProfiles() =>
        Ok(await sender.Send(new GetImportProfilesQuery()));

    [HttpPost("import/profiles")]
    public async Task<ActionResult<ImportProfileDto>> SaveProfile(SaveProfileRequest request) =>
        Ok(await sender.Send(new SaveImportProfileCommand(request.Name, request.Mapping, request.Header)));

    [HttpDelete("import/profiles/{id:guid}")]
    public async Task<IActionResult> DeleteProfile(Guid id)
    {
        var deleted = await sender.Send(new DeleteImportProfileCommand(id));
        return deleted ? NoContent() : NotFound();
    }

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
        return Ok(await sender.Send(
            new PreviewImportCommand(file, scope, targetId, request.ToOptions(), request.ToMapping())));
    }

    private async Task<ActionResult<ImportResultDto>> CommitAsync(
        ImportRequest request, TransferScope scope, Guid? targetId)
    {
        var file = await ReadAsync(request);
        return Ok(await sender.Send(
            new CommitImportCommand(file, scope, targetId, request.ToOptions(), request.ToMapping())));
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
