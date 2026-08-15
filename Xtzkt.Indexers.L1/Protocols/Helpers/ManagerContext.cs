using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols
{
    public class ManagerContext(ProtocolHandler proto)
    {
        readonly ProtocolHandler Proto = proto;
        L1Address? Address = null;
        long Change = 0;

        public void Init(JsonElement operation)
        {
            Proto.Cache.Chain.IncreaseManagerCounter(operation.RequiredArray("contents").Count());
            Address = null;
            Change = 0;
        }

        public void Set(L1Address address)
        {
            Address = address;
        }

        public void Credit(long amount)
        {
            Change += amount;
        }

        public void Burn(long amount)
        {
            Change -= amount;
        }

        public void Reset()
        {
            if (Address?.Type == AddressType.L1User && (Address.Balance == 0 || Address.Balance - Change == 0))
            {
                (Address as L1User)!.Counter = Proto.Cache.Chain.GetManagerCounter();
                (Address as L1User)!.Revealed = false;
            }
        }
    }
}
