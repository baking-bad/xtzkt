using System.Text.Json;
using Xtzkt.Indexers.Common.Services;

namespace Xtzkt.Indexers.L1.Protocols.Proto06
{
    class Rpc : Proto01.Rpc
    {
        public Rpc(TezosNode node) : base(node) { }

        public override Task<JsonElement> GetBakingRightsAsync(int block, int cycle)
            => Node.GetAsync($"chains/main/blocks/{block}/helpers/baking_rights?cycle={cycle}&max_priority=7&all=true");

        public override Task<JsonElement> GetLevelBakingRightsAsync(int block, int level, int maxRound)
            => Node.GetAsync($"chains/main/blocks/{block}/helpers/baking_rights?level={level}&max_priority={maxRound}&all=true");
    }
}
