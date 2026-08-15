using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class DoubleBakingCommit : Proto18.DoubleBakingCommit
    {
        public DoubleBakingCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override int GetSlashingLevel(L1Block block, L1Protocol protocol, int accusedLevel)
        {
            return Cache.Protocols.GetCycleEnd(protocol.GetCycle(accusedLevel) + protocol.SlashingDelay);
        }
    }
}
