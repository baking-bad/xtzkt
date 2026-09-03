using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto03
{
    class TransactionsCommit : Proto02.TransactionsCommit
    {
        public TransactionsCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override bool HasAllocated(JsonElement result) => result.OptionalBool("allocated_destination_contract") ?? false;
    }
}
