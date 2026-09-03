using Netezos.Encoding;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto14
{
    class BakingRightsCommit(ProtocolHandler protocol) : Proto13.BakingRightsCommit(protocol)
    {
        protected override Sampler GetSampler(IEnumerable<(int id, long stake)> selection, bool forceBase)
        {
            var sorted = selection.OrderByDescending(x =>
            {
                var baker = Cache.Addresses.GetBaker(x.id);
                return new byte[] { (byte)baker.PublicKey![0] }.Concat(Base58.Parse(baker.Hash));
            }, BytesComparer.Instance);

            return new Sampler(sorted.Select(x => x.id).ToArray(), sorted.Select(x => x.stake).ToArray());
        }
    }
}
