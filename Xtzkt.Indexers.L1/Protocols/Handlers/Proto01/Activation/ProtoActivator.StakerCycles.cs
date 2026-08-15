using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator : ProtocolCommit
    {
        protected virtual void BootstrapStakerCycles(L1Protocol protocol, List<L1Address> addresses)
        {
            // staker cycles start from proto19
        }

        protected async Task ClearStakerCycles()
        {
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "StakerCycles"
                WHERE "ChainId" = {0}
                """, Cache.Chain.Get().Id);
        }
    }
}
