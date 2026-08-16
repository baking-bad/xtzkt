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

        var chain = await db.Chains.FirstOrDefaultAsync(x => x.Id == chainConfig.Id);
        if (chain == null)
        {
            var node = scope.ServiceProvider.GetRequiredService<EvmNode>();
            
            var chainId = await node.GetChainId();
            var rollupAddress = await node.GetRollupAddress();
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
                "0x1f094" => "0x0019ea6db27c8e9f9081aecc112c01614505d3fc7eaa1a50e9822a6143c348eff7",
                _ => "0x000000000000000000000000000000000000000000000000000000000000000000",
            };

            chain = new XChain
            {
                Id = chainConfig.Id,
                ChainId = chainId,
                Network = network,
                RollupAddress = rollupAddress,
                Kernel = kernelVersion,
                MichelsonActivationLevel = chainConfig.MichelsonActivationLevel,
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
        else if (chainConfig.Network != null && chain.Network != chainConfig.Network)
        {
            chain.Network = chainConfig.Network;
            await db.SaveChangesAsync();
        }

        logger.LogInformation("Chain initialized: {chainId} ({network})", chain.ChainId, chain.Network);
        #endregion

        #region play
        //var evmNode = scope.ServiceProvider.GetRequiredService<EvmNode>();
        //var tezNode = scope.ServiceProvider.GetRequiredService<TezosNode>();

        //static IEnumerable<JsonElement> FilterLogs(JsonElement logs)
        //{
        //    return logs.EnumerateArray().Where(x => x.RequiredString("address") != EvmRuntime.MichelsonGateway && x.RequiredArray("topics")[0].RequiredString() != "0x60a9f8ac7be7e117b08e5ff52239667fcf051d55e03ead4bfa34c73ff86642e0");
        //}

        //static void PrintTrace(StringBuilder sb, JsonElement trace, int padding = 0)
        //{
        //    sb.AppendLine($"{new string(' ', padding)}{trace.RequiredString("from")[2..6]} -> {trace.RequiredString("to")[2..6]}    [{string.Join(" | ", FilterLogs(trace.RequiredArray("logs")).Select(x => x.RequiredString("address")[2..6]))}]");
        //    foreach (var call in trace.RequiredArray("calls").EnumerateArray())
        //        PrintTrace(sb, call, padding + 2);
        //}

        //var (_, receipts, traces) = await evmNode.GetBlockData(358195);

        //foreach (var trace in traces.EnumerateArray().Select(x => x.Required("result")))
        //{
        //    var sb = new StringBuilder();
        //    PrintTrace(sb, trace);
        //    Console.WriteLine(sb.ToString());
        //}

        //var call = new EvmCall(traces[0].Required("result"));

        //var flogs = FilterLogs(receipts[0].RequiredArray("logs"));
        //if (call.Logs.Count + call.Children().Sum(x => x.Logs.Count) != flogs.Count())
        //    throw new Exception("Not all logs were consumed");

        //return 0;

        //var level = 13162;

        //var state = new XChain
        //{
        //    ChainId = string.Empty,
        //    Hash = string.Empty,
        //    Id = 1,
        //    Kernel = string.Empty,
        //    Network = string.Empty,
        //    MichelsonActivationLevel = 0,
        //    RollupAddress = string.Empty,
        //    Level = level - 1,
        //};

        //var meta = new Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock.MetaBlockBuilder(
        //    new Xtzkt.Indexers.TezosX.Protocols.Proto01.EvmRpc(evmNode),
        //    new Xtzkt.Indexers.TezosX.Protocols.Proto01.MichelsonRpc(tezNode));

        //var block = await meta.GetNextBlock(state);

        //if (block.Batches.Count != 0)
        //{
        //    var blockStr = block.ToString();
        //    Console.WriteLine(level);
        //    Console.WriteLine(blockStr);
        //}
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
