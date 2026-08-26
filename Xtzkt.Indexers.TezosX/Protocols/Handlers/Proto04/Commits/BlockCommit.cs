using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto04;

class BlockCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
{
    public virtual async Task Apply(JsonElement evmBlock)
    {
        var sequencerPoolAddress = evmBlock.RequiredString("miner");
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

    public virtual async Task Revert()
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
}
