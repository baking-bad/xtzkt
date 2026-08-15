using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto14
{
    class DelegationsCommit : Proto01.DelegationsCommit
    {
        public DelegationsCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override int GetConsumedGas(JsonElement result)
        {
            return (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000);
        }
    }
}
