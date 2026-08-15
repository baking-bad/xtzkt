using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto14
{
    class RevealsCommit : Proto01.RevealsCommit
    {
        public RevealsCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override int GetConsumedGas(JsonElement result)
        {
            return (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000);
        }

        protected override void ApplyResult(L1RevealOperation op, L1Address sender, string pubKey)
        {
            if (op.Status != OperationStatus.Applied) return;
            base.ApplyResult(op, sender, pubKey);
        }

        protected override void RevertResult(L1RevealOperation op, L1Address sender)
        {
            if (op.Status != OperationStatus.Applied) return;
            base.RevertResult(op, sender);
        }
    }
}
