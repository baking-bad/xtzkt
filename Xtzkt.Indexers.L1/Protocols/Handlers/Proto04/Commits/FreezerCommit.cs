using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto04
{
    class FreezerCommit : Proto01.FreezerCommit
    {
        public FreezerCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override int GetFreezerCycle(JsonElement el) => el.RequiredInt32("cycle");
    }
}
