using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class MichelsonMigrationOperation() : MigrationOperation(Runtime.Michelson), ISourceOperation
    {
        [Column(nameof(BalanceChange))]
        public long BalanceChange { get; set; }

        public long? StorageId { get; set; }
        public int? BigMapUpdates { get; set; }
        public int? TokenTransfers { get; set; }

        public int? SubsCounter { get; set; }
    }

    public static class MichelsonMigrationOperationModel
    {
        public static void BuildMichelsonMigrationOperationModel(this ModelBuilder modelBuilder)
        {
        }
    }
}
