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

builder.Services.AddHangfire(config => config
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

var host = builder.Build();

// Migrations are applied by the Api on startup, not here — running MigrateAsync
// from both processes at once races on the same ALTER TABLE and crashes
// whichever loses. Do an initial sync so rates are available immediately
// rather than waiting for the first scheduled run, but don't let it be fatal:
// if the Api hasn't finished migrating yet, the daily recurring job below
// will pick it up later instead of crash-looping the whole worker.
using (var scope = host.Services.CreateScope())
{
    try
    {
        await scope.ServiceProvider.GetRequiredService<ExchangeRateSyncService>().SyncAsync();
    }
    catch (Exception ex)
    {
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>()
            .LogWarning(ex, "Initial exchange rate sync failed — will retry on the daily schedule.");
    }
}

// The static RecurringJob API requires JobStorage.Current, which isn't set
// this early — use the DI-registered manager instead, as Hangfire itself
// recommends for .NET Generic Host apps.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<ExchangeRateSyncService>(
    "sync-exchange-rates",
    service => service.SyncAsync(default),
    Cron.Daily);

host.Run();
