using CapitalTracker.Infrastructure.MarketData;
using CapitalTracker.Infrastructure.Persistence;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

// Apply any pending migrations, then run an initial sync so rates are
// available immediately rather than waiting for the first scheduled run.
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CapitalTrackerDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ExchangeRateSyncService>().SyncAsync();
}

RecurringJob.AddOrUpdate<ExchangeRateSyncService>(
    "sync-exchange-rates",
    service => service.SyncAsync(default),
    Cron.Daily);

host.Run();
