using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class L1Statistics() : Statistics(Layer.L1)
    {
        public int? Cycle { get; set; }

        #region supply
        public long TotalBootstrapped { get; set; }
        public long TotalCommitments { get; set; }

        public long TotalActivated { get; set; }
        public long TotalCreated { get; set; }
        public long TotalBurned { get; set; }
        public long TotalBanished { get; set; }
        public long TotalLost { get; set; }

        public long TotalFrozen { get; set; }
        public long TotalSmartRollupBonds { get; set; }
        #endregion

        #region staking
        public long TotalOwnStaked { get; set; }
        public long TotalOwnDelegated { get; set; }
        public long TotalExternalStaked { get; set; }
        public long TotalExternalDelegated { get; set; }

        public long TotalBakingPower { get; set; }
        public long TotalVotingPower { get; set; }

        public int TotalBakers { get; set; }
        public int TotalStakers { get; set; }
        public int TotalDelegators { get; set; }
        #endregion
    }

    public static class L1StatisticsModel
    {
        public static void BuildL1StatisticsModel(this ModelBuilder modelBuilder)
        {
        }
    }
}
