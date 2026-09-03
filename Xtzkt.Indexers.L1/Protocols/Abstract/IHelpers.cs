using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols
{
    public interface IHelpers
    {
        long BakingPower(L1Baker baker);
        long VotingPower(L1Baker baker);
    }
}
