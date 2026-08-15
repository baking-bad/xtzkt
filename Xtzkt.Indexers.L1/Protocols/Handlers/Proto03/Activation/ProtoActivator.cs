using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto03
{
    class ProtoActivator : Proto01.ProtoActivator
    {
        public ProtoActivator(ProtocolHandler proto) : base(proto) { }

        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.OriginationSize = parameters["origination_size"]?.Value<int>() ?? 257;
        }
    }
}
