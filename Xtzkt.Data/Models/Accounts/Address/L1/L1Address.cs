using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models
{
    public abstract class L1Address(AddressType type) : Address(Layer.L1, Runtime.Michelson, type)
    {
        [Column(nameof(Balance))]
        public long Balance { get; set; }
        public long SmartRollupBonds { get; set; }

        [Column(nameof(Counter))]
        public int Counter { get; set; }

        public int? BakerId { get; set; }
        public int? DelegationLevel { get; set; }
        public DateTime? DelegationTimestamp { get; set; }
        public bool Staked { get; set; }

        [Column(nameof(Index))]
        public int? Index { get; set; }

        #region counters
        public int SmartRollupsCount { get; set; }

        public int DelegationsCount { get; set; }

        [Column(nameof(RevealsCount))]
        public int RevealsCount { get; set; }

        [Column(nameof(TransferTicketCount))]
        public int TransferTicketCount { get; set; }

        [Column(nameof(IncreasePaidStorageCount))]
        public int IncreasePaidStorageCount { get; set; }
        public int UpdateSecondaryKeyCount { get; set; }
        public int DrainDelegateCount { get; set; }

        public int SubsidyCount { get; set; }

        public int SmartRollupAddMessagesCount { get; set; }
        public int SmartRollupCementCount { get; set; }
        public int SmartRollupExecuteCount { get; set; }
        public int SmartRollupOriginateCount { get; set; }
        public int SmartRollupPublishCount { get; set; }
        public int SmartRollupRecoverBondCount { get; set; }
        public int SmartRollupRefuteCount { get; set; }

        public int RefutationGamesCount { get; set; }
        public int ActiveRefutationGamesCount { get; set; }
        #endregion

        public override string ToString() => Hash;
    }

    public static class L1AddressModel
    {
        public static void BuildL1AddressModel(this ModelBuilder modelBuilder)
        {
            #region indexes
            modelBuilder.Entity<L1Address>()
                //.HasIndex(x => new { x.ChainId, x.Type }, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1Address.ChainId)}_{nameof(L1Address.Type)}_Partial")
                .HasIndex(x => x.Type, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1Address.Type)}_Partial")
                .HasFilter(@$"""{nameof(L1Address.Staked)}"" = true");

            modelBuilder.Entity<L1Address>()
                //.HasIndex(x => new { x.ChainId, x.Type }, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1Address.ChainId)}_{nameof(L1Address.Type)}_Partial2")
                .HasIndex(x => x.Type, $"IX_{nameof(XtzktContext.Addresses)}_{nameof(L1Address.Type)}_Partial2")
                .HasFilter(@$"""{nameof(L1Address.BakerId)}"" IS NOT NULL");

            modelBuilder.Entity<L1Address>()
                //.HasIndex(x => new { x.ChainId, x.Index })
                .HasIndex(x => x.Index)
                .HasFilter(@$"""{nameof(L1Address.Index)}"" IS NOT NULL");

            modelBuilder.Entity<L1Address>()
                .HasIndex(x => x.BakerId)
                .HasFilter(@$"""{nameof(L1Address.BakerId)}"" IS NOT NULL");
            #endregion
        }
    }
}
