using CapitalTracker.Infrastructure.MarketData;
using CapitalTracker.Infrastructure.Persistence;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.");

builder.Services.AddDbContext<CapitalTrackerDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpClient<NbuExchangeRateClient>();
builder.Services.AddScoped<ExchangeRateSyncService>();

// Unlike the Api, no eager validation here: a missing Finnhub key means holdings keep
// their last manual value, which is the pre-existing behaviour, not a broken deploy.
// The sync service says so in its log rather than failing.
builder.Services.Configure<FinnhubOptions>(
    builder.Configuration.GetSection(FinnhubOptions.SectionName));
builder.Services.AddHttpClient<FinnhubClient>(client =>
{
    client.BaseAddress = new Uri(FinnhubClient.BaseUrl);
    // Longer than the Api's 8s: there nobody waits on the other end of an SSE stream,
    // and a retried request is cheaper than a holding left unpriced for the day.
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<HoldingPriceSyncService>();

builder.Services.AddHangfire(config => config
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

var host = builder.Build();

// Migrations are applied by the Api on startup, not here — running MigrateAsync
// from both processes at once races on the same ALTER TABLE and crashes
// whichever loses. Do an initial sync so data is available immediately rather
// than waiting for the first scheduled run, but don't let it be fatal: if the
// Api hasn't finished migrating yet, the daily recurring jobs below will pick
// it up later instead of crash-looping the whole worker.
using (var scope = host.Services.CreateScope())
{
    await RunAtStartupAsync(scope.ServiceProvider.GetRequiredService<ExchangeRateSyncService>().SyncAsync, "exchange rate");
    await RunAtStartupAsync(scope.ServiceProvider.GetRequiredService<HoldingPriceSyncService>().SyncAsync, "holding price");

    async Task RunAtStartupAsync(Func<CancellationToken, Task> sync, string what)
    {
        try
        {
            await sync(default);
        }
        catch (Exception ex)
        {
            scope.ServiceProvider.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "Initial {What} sync failed — will retry on the daily schedule.", what);
        }
    }
}

// The static RecurringJob API requires JobStorage.Current, which isn't set
// this early — use the DI-registered manager instead, as Hangfire itself
// recommends for .NET Generic Host apps.
var recurringJobs = host.Services.GetRequiredService<IRecurringJobManager>();

recurringJobs.AddOrUpdate<ExchangeRateSyncService>(
    "sync-exchange-rates",
    service => service.SyncAsync(default),
    Cron.Daily);

// 22:00 UTC is after the US close all year (20:00 EDT / 21:00 EST) and still lands on
// the same UTC date, unlike Cron.Daily's midnight which would date a close to the
// following day.
recurringJobs.AddOrUpdate<HoldingPriceSyncService>(
    "sync-holding-prices",
    service => service.SyncAsync(default),
    Cron.Daily(22));

host.Run();
