using Xtzkt.Data.Models;
using Xtzkt.Indexers.L1.Services;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    public class Helpers(ProtocolHandler proto) : IHelpers
    {
        protected CacheService Cache { get; } = proto.Cache;
        protected BlockContext Context => proto.Context;
        
        public virtual long BakingPower(L1Baker baker)
        {
            if (!baker.Staked)
                return 0;

            var stake = baker.OwnDelegatedBalance + baker.ExternalDelegatedBalance;
            if (stake < Context.Protocol.MinimalStake)
                return 0;

            return stake - stake % Context.Protocol.MinimalStake;
        }

        public virtual long VotingPower(L1Baker baker)
        {
            if (!baker.Staked)
                return 0;

            var stake = baker.OwnDelegatedBalance + baker.ExternalDelegatedBalance;
            if (stake < Context.Protocol.MinimalStake)
                return 0;

            return stake - stake % Context.Protocol.MinimalStake;
        }
    }
}
