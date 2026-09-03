using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto04
{
    class TransactionsCommit(ProtocolHandler protocol) : Proto03.TransactionsCommit(protocol)
    {
        protected override Task ResetGracePeriod(L1TransactionOperation transaction, L1Address target) => Task.CompletedTask;
    }
}
