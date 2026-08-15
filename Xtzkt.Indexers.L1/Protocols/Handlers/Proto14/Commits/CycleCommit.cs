using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto14
{
    class CycleCommit(ProtocolHandler protocol) : Proto13.CycleCommit(protocol)
    {
        protected override async Task<byte[]?> GetVdfSolution(L1Block block)
        {
            return (await Db.VdfRevelationOps
                .AsNoTracking()
                .Where(x => x.ChainId == block.ChainId && x.Cycle == block.Cycle - 1)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync())?.Solution;
        }
    }
}
