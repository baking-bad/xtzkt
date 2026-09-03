using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto17
{
    class SmartRollupCementCommit : Proto16.SmartRollupCementCommit
    {
        public SmartRollupCementCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override string? GetCommitment(JsonElement content)
            => content.Required("metadata").Required("operation_result").OptionalString("commitment_hash");
    }
}
