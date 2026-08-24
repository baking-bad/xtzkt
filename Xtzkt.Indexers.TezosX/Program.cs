using App.Metrics;
using App.Metrics.Extensions.Configuration;
using App.Metrics.Formatters.Prometheus;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Services;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.TezosX;
using Xtzkt.Indexers.TezosX.Services;
using Xtzkt.Utils;

var builder = WebApplication.CreateBuilder(args);

#region configuration
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddEnvironmentVariables("XTZKT_TEZOSX_");
builder.Configuration.AddEnvironmentVariables("ASPNETCORE_");
builder.Configuration.AddCommandLine(args);
#endregion

#region logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
#endregion

#region services
builder.Services.AddDbContext<XtzktContext>(options =>
    options.UseNpgsql(builder.Configuration.GetDbConnectionString()));

builder.Services.AddCache(builder.Configuration);
builder.Services.AddEvmNode();
builder.Services.AddTezosNode();
builder.Services.AddTezosProtocols();
builder.Services.AddHostedService<ObserverService>();

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
            logger.LogError("Initialization failed: chain id must within [0..7].");
            return 4;
        }

        var node = scope.ServiceProvider.GetRequiredService<EvmNode>();
        var chainId = await node.GetChainId();

        var chain = await db.Chains.FirstOrDefaultAsync(x => x.Id == chainConfig.Id);
        if (chain == null)
        {
            var rollupAddress = await node.GetRollupAddress();
            var michelsonActivationLevel = await node.GetMichelsonActivationLevel();
            var network = chainConfig.Network ?? chainId switch
            {
                "0xa729" => "mainnet",
                "0x1f34f" => "shadownet",
                "0x1f440" => "previewnet",
                "0x1f094" => "dailynet",
                _ => "private",
            };
            var kernelVersion = chainId switch
            {
                "0xa729" => "0x00213a23a7a34cfbb7c1aba008d2fcad9d6e060882ffeb9745f6e3f039ece5e166",
                "0x1f34f" => "0x00985fe6f477169765206cfa26dbe7d58b333989d733363c9c648cc2707697df21",
                "0x1f440" => "0x00a237d1781d29dbcc7b9621684831f7c553946aca808acaf404d6818dc39b18e3",
                "0x1f094" => "0x00b14aa3ca1379bcb6b607cb5917572ecda788d63240727c01dec75ffa4bc75c25",
                _ => "0x000000000000000000000000000000000000000000000000000000000000000000",
            };

            chain = new XChain
            {
                Id = chainConfig.Id,
                ChainId = chainId,
                Network = network,
                RollupAddress = rollupAddress,
                Kernel = kernelVersion,
                MichelsonActivationLevel = michelsonActivationLevel,
                Level = -1,
                Timestamp = DateTimeOffset.MinValue.UtcDateTime,
                Hash = string.Empty,
            };

            db.Chains.Add(chain);
            await db.SaveChangesAsync();
        }
        else if (chain.Layer != Layer.TezosX)
        {
            logger.LogError("Initialization failed: chain id #{id} is already used for layer {layer}.", chain.Id, chain.Layer);
            return 5;
        }
        else if (chain.ChainId != chainId)
        {
            logger.LogError("Initialization failed: the node is on chain {nodeChainId}, while the DB is on {dbChainId}.", chainId, chain.ChainId);
            return 6;
        }
        else if (chainConfig.Network != null && chain.Network != chainConfig.Network)
        {
            chain.Network = chainConfig.Network;
            await db.SaveChangesAsync();
        }

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
