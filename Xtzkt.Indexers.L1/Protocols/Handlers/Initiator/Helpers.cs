using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Initiator
{
    public class Helpers() : IHelpers
    {
        public virtual long BakingPower(L1Baker baker)
            => throw new NotImplementedException();

        public virtual long VotingPower(L1Baker baker)
            => throw new NotImplementedException();
    }
}
