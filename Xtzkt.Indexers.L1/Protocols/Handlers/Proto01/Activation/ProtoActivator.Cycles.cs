using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator : ProtocolCommit
    {
        public virtual List<Cycle> BootstrapCycles(L1Protocol protocol, List<L1Address> addresses, JToken parameters)
        {
            var cycles = new List<Cycle>(protocol.ConsensusRightsDelay + 1);
            var bakers = addresses
                .Where(x => x.Type == AddressType.L1Baker)
                .OfType<L1Baker>();
            var selected = bakers.Where(x => x.BakingPower != 0);
            var selectedBakers = selected.Count();
            var selectedPower = selected.Sum(x => x.BakingPower);

            var initialSeed = parameters["initial_seed"]?.Value<string>() is string base58Seed &&
                Base58.TryParse(base58Seed, new byte[3], out var _initialSeed) &&
                _initialSeed.Length == 32
                ? _initialSeed
                : [];

            var seeds = Seed.GetInitialSeeds(protocol.ConsensusRightsDelay + 1, initialSeed);
            for (int index = 0; index <= protocol.ConsensusRightsDelay; index++)
            {
                var cycle = new Cycle
                {
                    ChainId = protocol.ChainId,
                    Index = index,
                    FirstLevel = protocol.GetCycleStart(index),
                    LastLevel = protocol.GetCycleEnd(index),
                    SnapshotLevel = 1,
                    TotalBakers = selectedBakers,
                    TotalBakingPower = selectedPower,
                    Seed = seeds[index]
                };
                Db.Cycles.Add(cycle);
                cycles.Add(cycle);
            }

            var state = Cache.Chain.Get();
            state.CyclesCount += protocol.ConsensusRightsDelay + 1;

            return cycles;
        }

        public async Task ClearCycles()
        {
            var chain = Cache.Chain.Get();

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "Cycles"
                WHERE "ChainId" = {0}
                """, chain.Id);

            chain.CyclesCount = 0;
        }
    }
}
