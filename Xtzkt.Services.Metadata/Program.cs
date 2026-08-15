using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xtzkt.Data;
using Xtzkt.Services.Metadata.Resolvers.Evm;
using Xtzkt.Services.Metadata.Resolvers.Ipfs;
using Xtzkt.Services.Metadata.Resolvers.Http;
using Xtzkt.Services.Metadata.Resolvers.DipDup;
using Xtzkt.Services.Metadata.Services;
using Xtzkt.Utils;

var builder = WebApplication.CreateBuilder(args);

#region configuration
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddEnvironmentVariables("XTZKT_METADATA_");
builder.Configuration.AddEnvironmentVariables("ASPNETCORE_");
builder.Configuration.AddCommandLine(args);
#endregion

#region logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
#endregion

#region services
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<XtzktContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

builder.Services.AddSingleton(serviceProvider =>
    new NpgsqlDataSourceBuilder(connectionString)
        .UseLoggerFactory(serviceProvider.GetService<ILoggerFactory>())
        .Build());

builder.Services.AddSingleton<StoreService>();
builder.Services.AddSingleton<MetadataService>();
builder.Services.AddHostedService<EvmResolver>();
builder.Services.AddHostedService<IpfsResolver>();
builder.Services.AddHostedService<HttpResolver>();
builder.Services.AddHostedService<DipDupResolver>();

builder.Services.AddHealthChecks();
#endregion

var app = builder.Build();

#region init
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Version {version}", AssemblyInfo.Version);

while (true)
{
    var dbFactory = app.Services.GetRequiredService<IDbContextFactory<XtzktContext>>();
    using var db = dbFactory.CreateDbContext();
    try
    {
        logger.LogInformation("Initialize database...");

        var migrations = db.Database.GetMigrations().ToList();
        var applied = db.Database.GetAppliedMigrations().ToList();

        for (int i = 0; i < Math.Min(migrations.Count, applied.Count); i++)
        {
            if (migrations[i] != applied[i])
            {
                logger.LogError("Initialization failed: metadata service and DB schema have incompatible versions. Drop the DB and restore it from the appropriate snapshot.");
                return 1;
            }
        }

        if (applied.Count > migrations.Count)
        {
            logger.LogError("Initialization failed: metadata service version is out of date. Update it to the newer version.");
            return 2;
        }

        if (applied.Count < migrations.Count)
        {
            logger.LogWarning("{cnt} pending migrations. Let's wait for the indexer to migrate the database, and try again.", migrations.Count - applied.Count);
            await Task.Delay(3000);
            continue;
        }

        logger.LogInformation("Database initialized");
        break;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Initialization failed. Let's try again.");
        await Task.Delay(3000);
        continue;
    }
}
#endregion

#region endpoints
app.MapGet("/version", () => AssemblyInfo.Version);
app.MapHealthChecks("/health");
#endregion

app.Run();

return 0;
