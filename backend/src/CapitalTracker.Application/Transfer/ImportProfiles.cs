using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transfer;

public record ImportProfileDto(Guid Id, string Name, string Mapping, DateTime CreatedAt);

public record SaveImportProfileCommand(string Name, string Mapping, string[] Header)
    : IRequest<ImportProfileDto>;

public record GetImportProfilesQuery : IRequest<List<ImportProfileDto>>;

public record DeleteImportProfileCommand(Guid Id) : IRequest<bool>;

/// <summary>
/// How a saved mapping recognises a file. Two statements from the same source carry the
/// same column names, so the header is the format's fingerprint — which means the owner
/// never has to pick the right profile from a list, and a profile keeps working when the
/// letterhead above the table changes length.
/// </summary>
public static class HeaderSignature
{
    public static string Of(IEnumerable<string> header) =>
        string.Join(
            '|',
            header
                .Select(c => string.Join(' ', c.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant())
                .Where(c => c.Length > 0));
}

public class SaveImportProfileCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SaveImportProfileCommand, ImportProfileDto>
{
    public async Task<ImportProfileDto> Handle(SaveImportProfileCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            throw new DomainValidationException("Дайте зіставленню назву.");

        var signature = HeaderSignature.Of(request.Header);
        if (signature.Length == 0)
            throw new DomainValidationException("У файлу порожня шапка — нема за чим упізнавати формат.");

        // One profile per format: saving again after correcting the mapping replaces it
        // rather than leaving two profiles competing to recognise the same file.
        var profile = await db.ImportProfiles
            .SingleOrDefaultAsync(p => p.HeaderSignature == signature, cancellationToken);

        if (profile is null)
        {
            profile = new ImportProfile
            {
                Id = Guid.NewGuid(),
                Name = name,
                Mapping = request.Mapping,
                HeaderSignature = signature,
            };
            db.ImportProfiles.Add(profile);
        }
        else
        {
            profile.Name = name;
            profile.Mapping = request.Mapping;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ImportProfileDto(profile.Id, profile.Name, profile.Mapping, profile.CreatedAt);
    }
}

public class GetImportProfilesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetImportProfilesQuery, List<ImportProfileDto>>
{
    public async Task<List<ImportProfileDto>> Handle(GetImportProfilesQuery request, CancellationToken cancellationToken) =>
        (await db.ImportProfiles.ToListAsync(cancellationToken))
        .OrderByDescending(p => p.CreatedAt)
        .Select(p => new ImportProfileDto(p.Id, p.Name, p.Mapping, p.CreatedAt))
        .ToList();
}

public class DeleteImportProfileCommandHandler(IApplicationDbContext db)
    : IRequestHandler<DeleteImportProfileCommand, bool>
{
    public async Task<bool> Handle(DeleteImportProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.ImportProfiles.SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (profile is null)
            return false;

        // Nothing hangs off a profile — it only ever pre-fills a form.
        db.ImportProfiles.Remove(profile);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
