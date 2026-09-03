using System.Numerics;
using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto16
{
    class Diagnostics : Proto14.Diagnostics
    {
        public Diagnostics(ProtocolHandler handler) : base(handler) { }
        
        protected override async Task TestTicketBalance(int level, TicketBalance balance)
        {
            var ticketer = await Cache.Addresses.GetAsync(balance.TicketerId);
            var ticket = Cache.Tickets.GetCached(balance.TicketId);
            var address = await Cache.Addresses.GetAsync(balance.AddressId);
            
            var ticketIdentity = JsonSerializer.Serialize(new
            {
                ticketer = ticketer.Hash,
                content_type = Micheline.FromBytes(ticket.RawType),
                content = Micheline.FromBytes(ticket.RawContent)
            });

            if (BigInteger.TryParse((await Rpc.GetTicketBalance(level, address.Hash, ticketIdentity)).RequiredString(), out var remoteBalance))
            {
                if (remoteBalance != balance.Balance)
                    throw new Exception($"Diagnostics failed: wrong ticket balance for {address.Hash}");
            }
            else
            {
                throw new Exception("Failed to get ticket balance");
            }
        }
    }
}
