using Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;

namespace Xtzkt.Indexers.TezosX.Protocols.Helpers
{
    public class OpContext(MetaBlock block, MetaBatch batch)
    {
        public bool IsFirstOp = true;
        public bool IsDelayedOp = block.Delayed.Any(x => x.Hash == batch.Hash);
        public long TotalGasLimit = 0;
        public long TotalGasUsed = 0;
        public long TotalDaFee = 0;
        public long TotalGasFee = 0;
        public long TotalGasRefund = 0;
    }
}
