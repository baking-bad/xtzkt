using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto07
{
    class ProtoActivator : Proto06.ProtoActivator
    {
        public ProtoActivator(ProtocolHandler proto) : base(proto) { }

        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.ByteCost = parameters["cost_per_byte"]?.Value<int>() ?? 250;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            protocol.ByteCost = 250;
        }

        protected override Task MigrateContext(L1Chain state) => Task.CompletedTask;
        protected override Task RevertContext(L1Chain state) => Task.CompletedTask;
    }
}
