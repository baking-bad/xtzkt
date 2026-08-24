using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;

public class EvmBatch
{
    public int Index { get; }
    public string Hash { get; }
    public List<EvmOperation> Operations { get; }

    public EvmBatch(JsonElement tx, JsonElement receipt)
    {
        Index = receipt.RequiredHexInt32("transactionIndex");
        Hash = tx.RequiredString("hash");
        Operations = [new EvmOperation(this, tx, receipt)];
    }
}

public class EvmOperation(EvmBatch batch, JsonElement tx, JsonElement receipt) : IMetaOperationContent
{
    public EvmBatch Batch { get; } = batch;
    public JsonElement Tx { get; } = tx;
    public JsonElement Receipt { get; } = receipt;
    public string From { get; } = receipt.RequiredString("from");
    public string? To { get; } = receipt.OptionalString("to");
    public IEnumerable<JsonElement> Logs => Receipt.OptionalArray("logs")?.EnumerateArray() ?? [];
}
