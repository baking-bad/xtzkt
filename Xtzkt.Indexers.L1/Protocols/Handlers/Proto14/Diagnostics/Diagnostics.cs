using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto14
{
    class Diagnostics : Proto12.Diagnostics
    {
        public Diagnostics(ProtocolHandler handler) : base(handler) { }

        protected override bool CheckDelegatedBalance(JsonElement remote, L1Baker baker) =>
            remote.RequiredInt64("delegated_balance") == baker.ExternalDelegatedBalance;
    }
}
