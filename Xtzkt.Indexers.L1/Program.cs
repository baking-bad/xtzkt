using Microsoft.EntityFrameworkCore;
using App.Metrics;
using App.Metrics.Extensions.Configuration;
using App.Metrics.Formatters.Prometheus;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.L1;
using Xtzkt.Indexers.Common.Services;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.L1.Services;
using Xtzkt.Indexers.L1.Services.Domains;
using Xtzkt.Indexers.L1.Protocols;
using Xtzkt.Utils;

var builder = WebApplication.CreateBuilder(args);

#region configuration
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddEnvironmentVariables("XTZKT_L1_");
builder.Configuration.AddEnvironmentVariables("ASPNETCORE_");
builder.Configuration.AddCommandLine(args);
#endregion

#region logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
#endregion

#region services
builder.Services.AddDbContext<XtzktContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCache(builder.Configuration);
builder.Services.AddTezosNode();
builder.Services.AddTezosProtocols();
builder.Services.AddQuotes(builder.Configuration);
builder.Services.AddHostedService<ObserverService>();

if (builder.Configuration.GetDomainsConfig().Enabled)
    builder.Services.AddHostedService<DomainsService>();

builder.Services.AddHealthChecks();

builder.Services.AddMetrics(options =>
{
    options.Configuration.ReadFrom(builder.Configuration);
    options.OutputMetrics.AsPrometheusPlainText();
    options.OutputMetrics.AsPrometheusProtobuf();
});

builder.Services.AddMetricsEndpoints(builder.Configuration, options =>
{
    options.MetricsEndpointOutputFormatter = new MetricsPrometheusProtobufOutputFormatter();
    options.MetricsTextEndpointOutputFormatter = new MetricsPrometheusTextOutputFormatter();
});
#endregion

var app = builder.Build();

#region init
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Version {version}", AssemblyInfo.Version);

while (true)
{
    using var scope = app.Services.CreateScope();
    using var db = scope.ServiceProvider.GetRequiredService<XtzktContext>();
    try
    {
        #region database
        logger.LogInformation("Initialize database...");

        var migrations = db.Database.GetMigrations().ToList();
        var applied = db.Database.GetAppliedMigrations().ToList();

        for (int i = 0; i < Math.Min(migrations.Count, applied.Count); i++)
        {
            if (migrations[i] != applied[i])
            {
                logger.LogError("Initialization failed: indexer and DB schema have incompatible versions. Drop the DB and restore it from the appropriate snapshot.");
                return 1;
            }
        }

        if (applied.Count > migrations.Count)
        {
            logger.LogError("Initialization failed: indexer version is out of date. Update the indexer to the newer version.");
            return 2;
        }

        if (applied.Count < migrations.Count)
        {
            logger.LogInformation("{cnt} pending migrations. Migrate database...", migrations.Count - applied.Count);
            db.Database.SetCommandTimeout(0);
            db.Database.Migrate();
        }

        logger.LogInformation("Database initialized");
        #endregion

        #region chain
        logger.LogInformation("Initialize chain...");

        var chainConfig = app.Configuration.GetSection("Chain").Get<ChainConfig>();
        if (chainConfig == null)
        {
            logger.LogError("Initialization failed: chain config is missed.");
            return 3;
        }

        if ((chainConfig.Id & ~0x7) != 0)
        {
            logger.LogError("Initialization failed: chain index must within [0..7].");
            return 4;
        }

        var chain = await db.Chains.FirstOrDefaultAsync(x => x.Id == chainConfig.Id);
        if (chain == null)
        {
            var node = scope.ServiceProvider.GetRequiredService<TezosNode>();

            var chainId = await node.GetAsync<string>("chains/main/chain_id");
            var network = chainId switch
            {
                "NetXdQprcVkpaWU" => "mainnet",
                "NetXsqzbfFenSTS" => "shadownet",
                "NetXpX8WSZkAZZA" => "ushuaianet",
                _ => "private"
            };

            chain = new L1Chain
            {
                Id = chainConfig.Id,
                ChainId = chainId,
                Network = network,
                Cycle = -1,
                Level = -1,
                Timestamp = DateTimeOffset.MinValue.UtcDateTime,
                Protocol = string.Empty,
                NextProtocol = string.Empty,
                Hash = string.Empty,
                VotingEpoch = -1,
                VotingPeriod = -1,
                QuoteLevel = -1,
                DomainsNameRegistry = string.Empty,
            };
            db.Chains.Add(chain);
            await db.SaveChangesAsync();
        }
        else if (chain.Layer != Layer.L1)
        {
            logger.LogError("Initialization failed: chain index #{index} is already used for layer {layer}.", chain.Id, chain.Layer);
            return 5;
        }
        else if (chainConfig.Network != null && chain.Network != chainConfig.Network)
        {
            chain.Network = chainConfig.Network;
            await db.SaveChangesAsync();
        }

        NullAddress.Id = (chain.Id << 28) + 1;
        logger.LogInformation("Chain initialized: {chainId} ({network})", chain.ChainId, chain.Network);
        #endregion

        break;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Initialization failed. Let's try again.");
        Thread.Sleep(3000);
        continue;
    }
}
#endregion

#region middleware
app.UseMetricsEndpoint();
app.UseMetricsTextEndpoint();
app.MapGet("/version", () => AssemblyInfo.Version);
app.MapHealthChecks("/health");
#endregion

app.Run();

return 0;
