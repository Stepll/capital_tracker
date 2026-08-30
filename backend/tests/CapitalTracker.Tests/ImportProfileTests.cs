using System.Text;
using CapitalTracker.Application.Common;
using CapitalTracker.Application.Transfer;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Tests;

public class ImportProfileTests
{
    [Fact]
    public void The_same_header_signs_the_same_however_the_spreadsheet_wrapped_it()
    {
        // Real headers arrive with line breaks inside them ("Дата\nоперації") and stray
        // spacing, and the same bank's next export need not wrap them identically.
        Assert.Equal(
            HeaderSignature.Of(["Дата операції", "Вид операції (дебет/кредит)", ""]),
            HeaderSignature.Of(["Дата\n операції", "  Вид операції (дебет/кредит)  ", "   "]));
    }

    [Fact]
    public async Task A_file_from_a_source_seen_before_recognises_itself()
    {
        await using var db = TestDbContext.Create();
        var header = new[] { "Дата операції", "Вид операції", "Сума" };
        await SaveAsync(db, "Монобанк", """{"headerRow":0,"columns":{"Дата":0},"event":{"fixed":"Оцінка"}}""", header);

        var inspection = await InspectAsync(db, "Дата операції;Вид операції;Сума\n2026-01-10;кредит;100\n");

        Assert.NotNull(inspection.MatchedProfile);
        Assert.Equal("Монобанк", inspection.MatchedProfile!.Name);
    }

    [Fact]
    public async Task A_file_from_somewhere_else_is_not_mistaken_for_it()
    {
        await using var db = TestDbContext.Create();
        await SaveAsync(db, "Монобанк", "{}", ["Дата операції", "Вид операції", "Сума"]);

        var inspection = await InspectAsync(db, "Time;Base-asset;Price\n2026-01-10;BTC;100\n");

        Assert.Null(inspection.MatchedProfile);
    }

    [Fact]
    public async Task Saving_again_for_the_same_format_corrects_it_rather_than_adding_a_rival()
    {
        // Two profiles matching one header would both claim the next file, and which one
        // won would come down to row order.
        await using var db = TestDbContext.Create();
        var header = new[] { "Дата", "Сума" };
        await SaveAsync(db, "Банк", """{"headerRow":0}""", header);
        await SaveAsync(db, "Банк (виправлено)", """{"headerRow":1}""", header);

        var profile = Assert.Single(await db.ImportProfiles.ToListAsync());
        Assert.Equal("Банк (виправлено)", profile.Name);
        Assert.Contains("\"headerRow\":1", profile.Mapping);
    }

    [Fact]
    public async Task A_profile_needs_a_name_and_a_header_to_be_worth_keeping()
    {
        await using var db = TestDbContext.Create();

        await Assert.ThrowsAsync<DomainValidationException>(() => SaveAsync(db, "  ", "{}", ["Дата"]));
        await Assert.ThrowsAsync<DomainValidationException>(() => SaveAsync(db, "Банк", "{}", ["", "  "]));
    }

    private static Task<ImportProfileDto> SaveAsync(TestDbContext db, string name, string mapping, string[] header) =>
        new SaveImportProfileCommandHandler(db).Handle(new SaveImportProfileCommand(name, mapping, header), default);

    private static Task<FileInspectionDto> InspectAsync(TestDbContext db, string csv) =>
        new InspectImportQueryHandler(db)
            .Handle(new InspectImportQuery(new ImportFile("f.csv", Encoding.UTF8.GetBytes(csv))), default);
}
