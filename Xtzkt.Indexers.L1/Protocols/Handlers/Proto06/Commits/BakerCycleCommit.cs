using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto06
{
    class BakerCycleCommit(ProtocolHandler protocol) : Proto05.BakerCycleCommit(protocol)
    {
        protected override long GetFutureBlockReward(L1Protocol protocol, int cycle)
            => cycle < protocol.NoRewardCycles ? 0 : (protocol.BlockReward0 * protocol.AttestersPerBlock);

        protected override long GetBlockReward(L1Protocol protocol, int cycle, int priority, long slots)
            => cycle < protocol.NoRewardCycles ? 0 : ((priority == 0 ? protocol.BlockReward0 : protocol.BlockReward1) * slots);

        protected override long GetFutureAttestationReward(L1Protocol protocol, int cycle, long slots)
            => cycle < protocol.NoRewardCycles ? 0 : (slots * protocol.AttestationReward0);

        protected override long GetAttestationReward(L1Protocol protocol, int cycle, int slots, int priority)
            => cycle < protocol.NoRewardCycles ? 0 : ((priority == 0 ? protocol.AttestationReward0 : protocol.AttestationReward1) * slots);
    }
}
