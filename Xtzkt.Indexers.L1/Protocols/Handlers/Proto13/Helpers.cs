using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto13
{
    public class Helpers(ProtocolHandler proto) : Proto12.Helpers(proto)
    {
        public override long VotingPower(L1Baker baker)
        {
            if (!baker.Staked)
                return 0;

            var stake = baker.OwnDelegatedBalance + baker.ExternalDelegatedBalance;
            if (stake < Context.Protocol.MinimalStake)
                return 0;

            return stake;
        }
    }
}
