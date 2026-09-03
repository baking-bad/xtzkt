using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Xtzkt.Data.Models
{
    public class EvmMigrationOperation() : MigrationOperation(Runtime.Evm)
    {
        [Column($"{nameof(BalanceChange)}18")]
        public BigInteger BalanceChange { get; set; }

        public int NonceChange { get; set; }
    }

    public static class EvmMigrationOperationModel
    {
        public static void BuildEvmMigrationOperationModel(this ModelBuilder modelBuilder)
        {
        }
    }
}
