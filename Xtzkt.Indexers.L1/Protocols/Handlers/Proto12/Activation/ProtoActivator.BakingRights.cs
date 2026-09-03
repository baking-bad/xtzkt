using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    partial class ProtoActivator : Proto11.ProtoActivator
    {
        protected override async Task<(IEnumerable<RightsGenerator.BR>, IEnumerable<RightsGenerator.AR>)> GetRights(
            L1Protocol protocol,
            List<L1Address> addresses,
            Cycle cycle)
        {
            var bakers = addresses
                .Where(x => x is L1Baker d && d.BakingPower != 0)
                .OfType<L1Baker>();

            var sampler = GetSampler(bakers.Select(x => (x.Id, x.BakingPower)));

            #region temporary diagnostics
            await sampler.Validate(Proto, 1, cycle.Index);
            #endregion

            var bakingRights = await RightsGenerator.GetBakingRightsAsync(sampler, protocol, cycle);
            var attestationRights = await RightsGenerator.GetAttestationRightsAsync(sampler, protocol, cycle);
            return (bakingRights, attestationRights);
        }
    }
}
