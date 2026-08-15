using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    partial class ProtoActivator : Proto11.ProtoActivator
    {
        protected override async Task<List<L1Address>> BootstrapAddresses(L1Protocol protocol, JToken parameters)
        {
            var addresses = await base.BootstrapAddresses(protocol, parameters);

            Cache.Statistics.Current.TotalFrozen = addresses
                .Where(x => x is L1Baker baker && baker.BakingPower != 0)
                .Sum(x => (x as L1Baker)!.BakingPower / (protocol.MaxDelegatedOverFrozenRatio + 1));

            return addresses;
        }
    }
}
