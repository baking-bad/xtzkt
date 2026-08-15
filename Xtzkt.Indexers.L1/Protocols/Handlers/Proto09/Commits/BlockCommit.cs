using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto09
{
    class BlockCommit : Proto01.BlockCommit
    {
        public BlockCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override (long, long) ParseBalanceUpdates(JsonElement balanceUpdates)
        {
            var deposit = 0L;
            var reward = 0L;
            foreach (var bu in balanceUpdates.EnumerateArray().Where(x => x.RequiredString("origin")[0] == 'b').Take(3))
            {
                if (bu.RequiredString("kind")[0] == 'f')
                {
                    var change = bu.RequiredInt64("change");
                    if (change > 0)
                    {
                        if (bu.RequiredString("category")[0] == 'd')
                            deposit = change;
                        else
                            reward = change;
                    }

                }
            }
            return (deposit, reward);
        }
    }
}
