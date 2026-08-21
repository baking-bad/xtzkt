using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols
{
    public class BlockContext
    {
        public required XBlock Block { get; init; }
        public required XProtocol Protocol { get; set; }
        public required XStatistics Statistics { get; init; }
        public XEvmAddress? SequencerPool { get; set; }

        #region operations
        public List<DepositOperation> DepositOps { get; set; } = [];
        public List<OriginationOperation> OriginationOps { get; set; } = [];
        public List<TransactionOperation> TransactionOps { get; set; } = [];
        public List<RevealOperation> RevealOps { get; set; } = [];
        public List<RegisterConstantOperation> RegisterConstantOps { get; set; } = [];
        public List<IncreasePaidStorageOperation> IncreasePaidStorageOps { get; set; } = [];
        public List<TransferTicketOperation> TransferTicketOps { get; set; } = [];
        #endregion

        #region fictive operations
        public List<MigrationOperation> MigrationOps { get; set; } = [];
        #endregion

        #region evm tokens
        public List<EvmTokenTransferData> EvmTokenTransfers { get; set; } = [];
        #endregion

        #region bridge tickets
        public List<BridgeTicketUpdateData> BridgeTicketUpdates { get; set; } = [];
        #endregion

        public IEnumerable<IOperation> EnumerateOps()
        {
            var ops = Enumerable.Empty<IOperation>();

            if (DepositOps.Count != 0) ops = ops.Concat(DepositOps);
            if (OriginationOps.Count != 0) ops = ops.Concat(OriginationOps);
            if (TransactionOps.Count != 0) ops = ops.Concat(TransactionOps);
            if (RevealOps.Count != 0) ops = ops.Concat(RevealOps);
            if (RegisterConstantOps.Count != 0) ops = ops.Concat(RegisterConstantOps);
            if (IncreasePaidStorageOps.Count != 0) ops = ops.Concat(IncreasePaidStorageOps);
            if (TransferTicketOps.Count != 0) ops = ops.Concat(TransferTicketOps);

            return ops;
        }

        public void Apply(XtzktContext db)
        {
            //var conn = (db.Database.GetDbConnection() as NpgsqlConnection)!;

            //if (TransactionOps.Count != 0)
            //    MichelsonTransactionOperation.Write(conn, TransactionOps);
        }

        public async Task Revert(XtzktContext db)
        {
            //if (TransactionOps.Count != 0)
            //    await db.Database.ExecuteSqlRawAsync($$"""
            //        DELETE FROM "{{nameof(XtzktContext.TransactionOps)}}"
            //        WHERE "{{nameof(TransactionOperation.ChainId)}}" = {0}
            //        AND "{{nameof(TransactionOperation.Level)}}" = {1}
            //        """, Block.ChainId, Block.Level);
        }
    }

    public record EvmTokenTransferData(
        XEvmContract Contract,
        BigInteger TokenId,
        TokenTags Type,
        string From,
        string To,
        BigInteger Amount,
        ISourceOperation Op);

    public record BridgeTicketUpdateData(
        byte[] TicketHash,
        string? From,
        string? To,
        BigInteger Amount,
        ISourceOperation Op);
}
