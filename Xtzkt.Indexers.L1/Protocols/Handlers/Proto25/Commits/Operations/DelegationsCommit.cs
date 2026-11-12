using System.Text.Json;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto25
{
    class DelegationsCommit(ProtocolHandler protocol) : Proto18.DelegationsCommit(protocol)
    {
        protected override async Task Unstake(DelegationOperation operation, List<JsonElement> balanceUpdates)
        {
            await base.Unstake(operation, balanceUpdates);

            #region debug
            if (await Cache.Addresses.GetAsync(operation.SenderId) is L1User { StakedPseudotokens: not null } sender)
                throw new Exception($"Staking pseudotokens left after delegation change {sender.Hash}");
            #endregion
        }
    }
}
