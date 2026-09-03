using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols
{
    static class GracePeriod
    {
        public static int Init(int level, L1Protocol proto)
            => proto.GetCycleStart(proto.GetCycle(level) + proto.ConsensusRightsDelay + proto.ToleratedInactivityPeriod + 1);

        public static int Reset(int level, L1Protocol proto)
            => proto.GetCycleStart(proto.GetCycle(level) + proto.ToleratedInactivityPeriod + 1);
    }
}
