using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class L1Baker() : L1User(AddressType.L1Baker)
    {
        public int ActivationLevel { get; set; }
        public DateTime ActivationTimestamp { get; set; }
        public int DeactivationLevel { get; set; }

        public string? ConsensusAddress { get; set; }
        public string? CompanionAddress { get; set; }

        public long BakingPower { get; set; }
        public long VotingPower { get; set; }

        public long OwnDelegatedBalance { get; set; }
        public long ExternalDelegatedBalance { get; set; }
        public long MinTotalDelegated { get; set; }
        public int MinTotalDelegatedLevel { get; set; }
        public int DelegatorsCount { get; set; }

        public long OwnStakedBalance { get; set; }
        public long ExternalStakedBalance { get; set; }
        public BigInteger? IssuedPseudotokens { get; set; }
        public int StakersCount { get; set; }

        public long ExternalUnstakedBalance { get; set; }
        public long RoundingError { get; set; }

        public long? FrozenDepositLimit { get; set; }
        public long? LimitOfStakingOverBaking { get; set; }
        public long? EdgeOfBakingOverStaking { get; set; }

        [Column(nameof(BlocksCount))]
        public int BlocksCount { get; set; }
        public int AttestationsCount { get; set; }
        public int PreattestationsCount { get; set; }
        public int BallotsCount { get; set; }
        public int ProposalsCount { get; set; }
        public int DalEntrapmentEvidenceOpsCount { get; set; }
        public int DoubleBakingCount { get; set; }
        public int DoubleConsensusCount { get; set; }
        public int NonceRevelationsCount { get; set; }
        public int VdfRevelationsCount { get; set; }
        public int RevelationPenaltiesCount { get; set; }
        public int AttestationRewardsCount { get; set; }
        public int DalAttestationRewardsCount { get; set; }
        public int AutostakingOpsCount { get; set; }

        public int? SoftwareId { get; set; }
        public int? SoftwareUpdateLevel { get; set; }

        #region helpers
        [NotMapped]
        public long TotalDelegated => OwnDelegatedBalance + ExternalDelegatedBalance;

        [NotMapped]
        public long TotalStaked => OwnStakedBalance + ExternalStakedBalance;
        #endregion
    }

    public static class L1BakerModel
    {
        public static void BuildL1BakerModel(this ModelBuilder modelBuilder)
        {
            #region indexes
            modelBuilder.Entity<L1Baker>()
                //.HasIndex(x => new { x.ChainId, x.Staked }, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1Baker.ChainId)}_{nameof(L1Baker.Staked)}_Partial")
                .HasIndex(x => x.Staked, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1Baker.Staked)}_Partial")
                .HasFilter($@"""{nameof(L1Address.Type)}"" = {(int)AddressType.L1Baker}");

            modelBuilder.Entity<L1Baker>()
                //.HasIndex(x => new { x.ChainId, x.DeactivationLevel }, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1Baker.ChainId)}_{nameof(L1Baker.DeactivationLevel)}_Partial")
                .HasIndex(x => x.DeactivationLevel, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1Baker.DeactivationLevel)}_Partial")
                .HasFilter($@"""{nameof(L1Address.Type)}"" = {(int)AddressType.L1Baker}");
            #endregion
        }
    }
}
