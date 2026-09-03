using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class BlockCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
{
    public async Task Apply(JsonElement evmBlock)
    {
        var sequencerPoolAddress = GetSequencerPoolAddress(evmBlock);
        if (sequencerPoolAddress != EvmRuntime.NullAddress)
        {
            var sequencerPool = await Helpers.GetOrCreateXEvmAddress(sequencerPoolAddress);

            Db.TryAttach(sequencerPool);
            sequencerPool.BlocksCount++;
            sequencerPool.LastLevel = Context.Block.Level;
            sequencerPool.LastTimestamp = Context.Block.Timestamp;

            Context.Block.SequencerPoolId = sequencerPool.Id;
            Context.SequencerPool = sequencerPool;
        }

        Cache.Chain.Get().BlocksCount++;
        Cache.Blocks.Add(Context.Block);
        Batch.Blocks.Add(Context.Block);
    }

    public async Task Revert()
    {
        if (Context.SequencerPool is XEvmAddress sequencerPool)
        {
            sequencerPool.BlocksCount--;
            sequencerPool.LastLevel = Context.Block.Level;
            sequencerPool.LastTimestamp = Context.Block.Timestamp;
            if (sequencerPool.IsEmpty()) await Helpers.RemoveXEvmAddress(sequencerPool);
        }

        Cache.Chain.Get().BlocksCount--;
        Cache.Blocks.Remove(Context.Block);
        Db.Blocks.Remove(Context.Block);
        Cache.Chain.ReleaseOperationId();
    }

    protected virtual string GetSequencerPoolAddress(JsonElement evmBlock)
    {
        // old kernels don't write the sequencer pool address into the block header, so we hardcode it
        return "0xcf02b9ca488f8f6f4e28e37aa1bdd16b3f1b2ad8";
    }
}
