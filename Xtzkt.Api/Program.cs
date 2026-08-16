using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.EntityFrameworkCore;
using Dapper;
using Npgsql;
using Xtzkt.Api;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Services.Database;
using Xtzkt.Api.Services.ResponseCache;
using Xtzkt.Api.Swagger;
using Xtzkt.Api.Utils.Converters;
using Xtzkt.Data;
using Xtzkt.Utils;

var builder = WebApplication.CreateBuilder(args);

#region configuration
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddEnvironmentVariables("XTZKT_API_");
builder.Configuration.AddEnvironmentVariables("ASPNETCORE_");
builder.Configuration.AddCommandLine(args);
#endregion

#region logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
#endregion

#region services
builder.Services.AddDbContextFactory<XtzktContext>(options =>
{
    // XtzktContext is used internally, so no statement timeout here
    options.UseNpgsql(builder.Configuration.GetDbConnectionString(statementTimeout: false));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});
builder.Services.AddSingleton(serviceProvider =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetDbConnectionString());
#pragma warning disable NPG9001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    dataSourceBuilder.AddTypeInfoResolverFactory(new BigIntegerNumericTypeInfoResolverFactory());
#pragma warning restore NPG9001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    dataSourceBuilder.UseLoggerFactory(serviceProvider.GetService<ILoggerFactory>());
    return dataSourceBuilder.Build();
});

builder.Services.AddSingleton<DbInitService>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<DbInitService>());
builder.Services.AddHostedService<DbListenerService>();

builder.Services.AddSingleton<AddressCache>();
builder.Services.AddSingleton<AssetCache>();
builder.Services.AddSingleton<SoftwareCache>();
builder.Services.AddSingleton<AliasCache>();
builder.Services.AddSingleton<ChainCache>();
builder.Services.AddSingleton<ProtocolCache>();
builder.Services.AddSingleton<ResponseCacheService>();
builder.Services.AddTransient<AccountRepository>();
builder.Services.AddTransient<AssetRepository>();
builder.Services.AddTransient<ChainRepository>();
builder.Services.AddTransient<BlockRepository>();
builder.Services.AddTransient<ProtocolRepository>();
builder.Services.AddTransient<AddressRepository>();
builder.Services.AddTransient<SoftwareRepository>();
builder.Services.AddTransient<TransactionRepository>();
builder.Services.AddTransient<RevealRepository>();
builder.Services.AddTransient<IncreasePaidStorageRepository>();
builder.Services.AddTransient<TransferTicketRepository>();
builder.Services.AddTransient<RegisterConstantRepository>();
builder.Services.AddTransient<DepositRepository>();
builder.Services.AddTransient<MigrationRepository>();
builder.Services.AddTransient<OriginationRepository>();
builder.Services.AddTransient<TokenRepository>();
builder.Services.AddTransient<TicketRepository>();
builder.Services.AddTransient<TicketBalanceRepository>();
builder.Services.AddTransient<TokenBalanceRepository>();
builder.Services.AddTransient<TokenTransferRepository>();
builder.Services.AddTransient<TicketTransferRepository>();
builder.Services.AddTransient<ActivityRepository>();
builder.Services.AddTransient<BigMapRepository>();
builder.Services.AddTransient<BigMapKeyRepository>();
builder.Services.AddTransient<BigMapUpdateRepository>();
builder.Services.AddTransient<LogRepository>();
builder.Services.AddTransient<StorageRepository>();
builder.Services.AddTransient<Eip7702DelegationRepository>();
builder.Services.AddTransient<SearchRepository>();

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<BadRequestExceptionFilter>();
        options.Filters.Add<TimeoutExceptionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new BigIntegerConverter());
        options.JsonSerializerOptions.Converters.Add(new BigIntegerNullableConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.MaxDepth = 100_000;
        options.JsonSerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { TypeInfoResolverModifiers.BasePropsFirst },
        };
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context => new BadRequest(context);
    });

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver
    {
        Modifiers = { TypeInfoResolverModifiers.BasePropsFirst },
    };
});

builder.Services.AddSwagger();
builder.Services.AddHealthChecks();
#endregion

#region dapper
SqlMapper.AddTypeHandler(new BigIntegerTypeHandler());
SqlMapper.AddTypeHandler(new JsonElementTypeHandler());
SqlMapper.AddTypeHandler(new RawJsonTypeHandler());
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
                logger.LogError("Initialization failed: API and DB schema have incompatible versions. Drop the DB and restore it from the appropriate snapshot.");
                return 1;
            }
        }

        if (applied.Count > migrations.Count)
        {
            logger.LogError("Initialization failed: API version is out of date. Update the API to the newer version.");
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

#region middleware
app.UseCors(builder => builder
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin());
    //.WithExposedHeaders(
    //    StateHeadersMiddleware.TZKT_VERSION,
    //    StateHeadersMiddleware.TZKT_LEVEL,
    //    StateHeadersMiddleware.TZKT_KNOWN_LEVEL,
    //    StateHeadersMiddleware.TZKT_SYNCED_AT));

app.MapControllers();
app.MapSwagger();
app.MapHealthChecks("/health");
#endregion

app.Run();

return 0;
