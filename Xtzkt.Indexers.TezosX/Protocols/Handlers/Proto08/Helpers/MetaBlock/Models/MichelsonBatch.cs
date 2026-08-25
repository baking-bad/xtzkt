using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers.MetaBlock;

public class MichelsonBatch
{
    public int Index { get; }
    public string Hash { get; }
    public List<MichelsonOperation> Operations { get; }
    public string? CracId { get; }

    public MichelsonBatch(IMichelsonRuntime michelsonRuntime, int index, JsonElement content)
    {
        Index = index;
        Hash = content.RequiredString("hash");
        Operations = [..content.RequiredArray("contents")
                .EnumerateArray()
                .Select(x => new MichelsonOperation(this, x))];

        if (Operations[0].Internals.Count != 0 && Operations[0].From == michelsonRuntime.NullAddress)
        {
            var first = Operations[0].Internals[0];
            if (first.From == michelsonRuntime.CracOrigin &&
                first.Content.RequiredString("kind") == "event" &&
                first.Content.RequiredString("tag") == "cross_runtime_call")
                CracId = first.Content.Required("payload").RequiredString("string");
        }
    }
}

public class MichelsonOperation : IMetaOperationContent
{
    public MichelsonBatch Batch { get; }
    public JsonElement Content { get; }
    public List<MichelsonInternalOperation> Internals { get; }
    public string From { get; }
    public string? To { get; }

    public MichelsonOperation(MichelsonBatch batch, JsonElement content)
    {
        Batch = batch;
        Content = content;
        Internals = content.Required("metadata")
            .OptionalArray("internal_operation_results")?
            .EnumerateArray()
            .Select(x => new MichelsonInternalOperation(this, x))
            .ToList()
            ?? [];
        From = content.RequiredString("source");
        To = content.OptionalString("destination");
    }

    public override string ToString()
    {
        return To != null
            ? $"{From[..7]}..{From[^4..]} -> {To[..7]}..{To[^4..]} ({Content.Optional("parameters")?.OptionalString("entrypoint")})"
            : $"{From[..7]}..{From[^4..]} ({Content.RequiredString("kind")})";
    }
}

public class MichelsonInternalOperation(MichelsonOperation operation, JsonElement content) : IMetaInternalOperationContent
{
    public MichelsonOperation Operation { get; } = operation;
    public JsonElement Content { get; } = content;
    public string From { get; } = content.RequiredString("source");
    public string? To { get; } = content.OptionalString("destination");

    public override string ToString()
    {
        return To != null
            ? $"{From[..7]}..{From[^4..]} -> {To[..7]}..{To[^4..]} ({Content.Optional("parameters")?.OptionalString("entrypoint")})"
            : $"{From[..7]}..{From[^4..]} ({Content.RequiredString("kind")})";
    }
}