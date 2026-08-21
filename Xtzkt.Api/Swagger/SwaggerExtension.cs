using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Utils;

namespace Xtzkt.Api.Swagger;

static class SwaggerExtension
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        return services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer((schema, ctx, ct) =>
            {
                if (ctx.JsonPropertyInfo?.CustomConverter is Int64StringConverter)
                {
                    schema.Type = JsonSchemaType.String;
                    schema.Format = "int64";
                }
                if (ctx.JsonPropertyInfo?.CustomConverter is Int64StringNullableConverter)
                {
                    schema.Type = JsonSchemaType.String | JsonSchemaType.Null;
                    schema.Format = "int64";
                }
                if (ctx.JsonTypeInfo.Type == typeof(BigInteger))
                {
                    schema.Properties?.Clear();
                    schema.Type = JsonSchemaType.String;
                    schema.Format = "bigint";
                }
                if (ctx.JsonTypeInfo.Type == typeof(SelectionParameter) ||
                    ctx.JsonTypeInfo.Type == typeof(MichelineParameter) ||
                    ctx.JsonTypeInfo.Type == typeof(HexBytesParameter) ||
                    ctx.JsonTypeInfo.Type == typeof(Utf8BytesParameter))
                {
                    foreach (var (_, prop) in schema.Properties!)
                    {
                        prop.Properties?.Clear();
                        (prop as OpenApiSchema)!.Type = JsonSchemaType.String;
                    }
                }
                if (ctx.JsonTypeInfo.Type == typeof(SortParameter) ||
                    ctx.JsonTypeInfo.Type == typeof(CursorParameter) ||
                    ctx.JsonTypeInfo.Type == typeof(ActivityTypesParameter) ||
                    ctx.JsonTypeInfo.Type == typeof(ActivityRolesParameter) ||
                    ctx.JsonTypeInfo.Type == typeof(SearchScopesParameter))
                {
                    schema.Properties?.Clear();
                    schema.Type = JsonSchemaType.String;
                }
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((op, ctx, ct) =>
            {
                if (op.Parameters != null)
                    foreach (var p in op.Parameters.Where(x => x.In == ParameterLocation.Query))
                        if (p is OpenApiParameter _p && _p.Name != null)
                            _p.Name = JsonNamingPolicy.CamelCase.ConvertName(_p.Name);
                return Task.CompletedTask;
            });
            options.AddDocumentTransformer((doc, ctx, ct) =>
            {
                doc.Info.Title = "0xTzKT API";
                doc.Info.Version = $"v{string.Join('.', AssemblyInfo.Version.Split('.').Take(3))}";
                doc.Info.Contact = new()
                {
                    Name = "Baking Bad Team",
                    Email = "hello@bakingbad.dev",
                    Url = new("https://bakingbad.dev"),
                };
                doc.Info.Description = LoadText("Xtzkt.Api.Swagger.description.md");
                doc.Info.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                doc.Info.Extensions["x-logo"] = new JsonNodeExtension(new JsonObject
                {
                    ["url"] = "https://tzkt.io/logo.png",
                    ["href"] = "https://tzkt.io/",
                });

                static int TagOrder(string tag) => tag switch
                {
                    "Accounts" => 0,
                    "Operations" => 1,
                    "Assets" => 2,
                    "Activity" => 3,
                    "Contracts" => 4,
                    "Chains" => 5,
                    "Search" => 6,
                    _ => throw new Exception("Unregistered OpenAPI tag"),
                };

                static int PathOrder(string path) => path switch
                {
                    _ when path.StartsWith("/v1/assets") => 0,
                    _ when path.StartsWith("/v1/tokens") => 1,
                    _ when path.StartsWith("/v1/tickets") => 2,
                    _ when path.StartsWith("/v1/bridge_tickets") => 3,
                    _ => 4,
                };

                foreach (var tag in doc.Tags!)
                    tag.Description = LoadText($"Xtzkt.Api.Swagger.Tags.{tag.Name}.md");

                var restTags = new JsonArray();
                foreach (var tag in doc.Tags!.OrderBy(x => TagOrder(x.Name!)).ThenBy(x => x.Name))
                    restTags.Add(tag.Name);

                var paths = new OpenApiPaths();
                foreach (var (path, item) in doc.Paths!
                    .OrderBy(x => TagOrder(x.Value.Operations!.Values.First().Tags!.First().Name!))
                    .ThenBy(x => PathOrder(x.Key)))
                    paths[path] = item;
                    
                doc.Paths = paths;

                doc.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                doc.Extensions["x-tagGroups"] = new JsonNodeExtension(new JsonArray
                {
                    new JsonObject { ["name"] = "REST API", ["tags"] = restTags },
                    new JsonObject { ["name"] = "WebSocket API", ["tags"] = new JsonArray() },
                });

                // fixes missed descriptions for OneOf(null, ref)
                var types = new List<string>()
                {
                    nameof(AddressInfoParameter),
                    nameof(AddressInfoNullParameter),
                    nameof(ChainInfoParameter),
                    nameof(TokenInfoParameter),
                    nameof(TicketInfoParameter),
                    nameof(BridgeTicketInfoParameter),
                    nameof(BigMapInfoParameter),
                    nameof(BigMapKeyInfoParameter),
                    nameof(ContractInfoParameter),
                };

                foreach (var type in types)
                {
                    var schema = doc.Components!.Schemas![type];
                    foreach (var key in schema.Properties!.Keys.ToList())
                        schema.Properties[key] = schema.Properties[key].OneOf![1];
                }

                return Task.CompletedTask;
            });
        });
    }

    /// <summary>
    /// Serves redoc at '/' and openapi at '/{documentName}/openapi.json'.
    /// </summary>
    public static WebApplication MapSwagger(this WebApplication app)
    {
        var html = LoadBytes("Xtzkt.Api.Swagger.index.html");
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(SHA256.HashData(html)[..8])}\"");

        app.MapGet("/", () => Results.Bytes(html, "text/html; charset=utf-8", entityTag: etag)).ExcludeFromDescription();
        app.MapOpenApi("/{documentName}/openapi.json");

        return app;
    }

    static byte[] LoadBytes(string name)
    {
        using var stream = OpenResource(name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    static string LoadText(string name)
    {
        using var stream = OpenResource(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    static Stream OpenResource(string name)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' not found");
    }
}
