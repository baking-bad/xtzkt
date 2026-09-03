using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Xtzkt.Data.Models
{
    public class L1User : L1Address
    {
        [Column(nameof(Revealed))]
        public bool Revealed { get; set; }

        [Column(nameof(PublicKey))]
        public string? PublicKey { get; set; }

        public BigInteger? StakedPseudotokens { get; set; }
        public long UnstakedBalance { get; set; }
        public int? UnstakedBakerId { get; set; }

        public int? StakingUpdatesCount { get; set; }

        public int ActivationsCount { get; set; }

        [Column(nameof(RegisterConstantsCount))]
        public int RegisterConstantsCount { get; set; }
        public int SetDepositsLimitsCount { get; set; }
        public int StakingOpsCount { get; set; }
        public int SetDelegateParametersOpsCount { get; set; }
        public int DalPublishCommitmentOpsCount { get; set; }

        #region ctors
        public L1User() : base(AddressType.L1User) { }
        protected L1User(AddressType type) : base(type) { }
        #endregion
    }

    public static class L1UserModel
    {
        public static void BuildL1UserModel(this ModelBuilder modelBuilder)
        {
            #region indexes
            modelBuilder.Entity<L1User>()
                .HasIndex(x => x.UnstakedBakerId)
                .HasFilter($@"""{nameof(L1User.UnstakedBakerId)}"" IS NOT NULL");

            modelBuilder.Entity<L1User>()
                .HasIndex(x => x.BakerId, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1User.BakerId)}_Partial")
                .HasFilter(@$"""{nameof(L1User.BakerId)}"" IS NOT NULL AND ""{nameof(L1User.StakedPseudotokens)}"" IS NOT NULL");

            modelBuilder.Entity<L1User>()
                //.HasIndex(x => new { x.ChainId, x.Type }, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1User.ChainId)}_{nameof(L1User.Type)}_Partial3")
                .HasIndex(x => x.Type, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1User.Type)}_Partial3")
                .HasFilter(@$"""{nameof(L1User.Staked)}"" = true AND ""{nameof(L1User.StakedPseudotokens)}"" IS NOT NULL");
            #endregion
        }
    }
}
