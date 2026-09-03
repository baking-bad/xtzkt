using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    public class Helpers(ProtocolHandler proto) : Proto01.Helpers(proto)
    {
        public override long BakingPower(L1Baker baker)
        {
            if (!baker.Staked)
                return 0;

            var depositCap = baker.FrozenDepositLimit is long depositLimit
                ? Math.Min(baker.Balance, depositLimit)
                : baker.Balance;

            var stake = Math.Min(baker.OwnDelegatedBalance + baker.ExternalDelegatedBalance, depositCap * (Context.Protocol.MaxDelegatedOverFrozenRatio + 1));
            if (stake < Context.Protocol.MinimalStake)
                return 0;

            return stake;
        }
    }
}
