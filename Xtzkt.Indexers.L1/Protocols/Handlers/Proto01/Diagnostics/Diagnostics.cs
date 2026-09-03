using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.L1.Services;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class Diagnostics(ProtocolHandler handler) : IDiagnostics
    {
        protected readonly XtzktContext Db = handler.Db;
        protected readonly CacheService Cache = handler.Cache;
        protected readonly IRpc Rpc = handler.Rpc;
        protected BlockContext Context => handler.Context;

        int AddedOperations = 0;
        readonly Dictionary<int, L1Address> ChangedAddresses = [];
        readonly Dictionary<long, TicketBalance> ChangedTicketBalances = [];

        public void TrackChanges()
        {
            var entries = Db.ChangeTracker.Entries();
            AddedOperations += entries.Count(x => x.Entity is IOperation or Log && x.State == EntityState.Added);

            foreach (var address in entries.Where(x =>
                x.Entity is L1Address && (x.State == EntityState.Modified || x.State == EntityState.Added))
                .Select(x => (x.Entity as L1Address)!))
                ChangedAddresses[address.Id] = address;

            foreach (var ticket in entries.Where(x =>
                x.Entity is TicketBalance && (x.State == EntityState.Modified || x.State == EntityState.Added))
                .Select(x => (x.Entity as TicketBalance)!))
                ChangedTicketBalances[ticket.Id] = ticket;
        }

        public virtual Task Run(JsonElement block)
        {
            var ops = block.GetProperty("operations");
            var opsCount = 0;

            if (ops.EnumerateArray().Any())
            {
                foreach (var op in ops[0].EnumerateArray())
                {
                    var content = op.RequiredArray("contents")[0];
                    if (content.RequiredString("kind")[^1] == 'e') // .._aggregate
                        opsCount += content.RequiredArray("committee").Count();
                    else
                        opsCount++;
                }
                foreach (var op in ops[1].EnumerateArray())
                {
                    var content = op.RequiredArray("contents")[0];
                    if (content.RequiredString("kind")[0] == 'p')
                        opsCount += content.RequiredArray("proposals").Count();
                    else
                        opsCount++;
                }
                opsCount += ops[2].Count();
                foreach (var op in ops[3].EnumerateArray())
                {
                    foreach (var content in op.Required("contents").EnumerateArray())
                    {
                        opsCount++;
                        if (content.Required("metadata").TryGetProperty("internal_operation_results", out var internalContents))
                            opsCount += internalContents.EnumerateArray().Count(x => x.RequiredString("kind") != "event" || x.Required("result").RequiredString("status") == "applied");
                    }
                }
            }

            return RunDiagnostics(block.Required("header").RequiredInt32("level"), opsCount);
        }

        public virtual Task Run(int level)
        {
            return RunDiagnostics(level);
        }

        protected virtual async Task RunDiagnostics(int level, int ops = -1)
        {
            if (ops != -1 && ops != AddedOperations + Context.TransactionOps.Count + Context.AttestationOps.Count)
                throw new Exception($"Diagnostics failed: wrong operations count");

            var state = Cache.Chain.Get();
            var proto = await Cache.Protocols.GetAsync(state.NextProtocol);

            foreach (var ticketBalance in ChangedTicketBalances.Values)
            {
                await TestTicketBalance(level, ticketBalance);
            }

            await TestGlobalCounter(level, state);

            foreach (var address in ChangedAddresses.Values)
            {
                if (address is L1Baker baker)
                    await TestBaker(level, baker, proto);

                if (address.Type >= AddressType.L1User && address.Type <= AddressType.L1Contract)
                    await TestAddress(level, address);
            }
            
            if (Cache.Blocks.Current().Events.HasFlag(L1BlockEvents.CycleBegin))
            {
                foreach (var cycle in Db.ChangeTracker.Entries().Where(x => x.Entity is Cycle).Select(x => (x.Entity as Cycle)!))
                    await TestCycle(state, cycle);
                
                await TestParticipation(state);
                await TestDalParticipation(state);
                await TestBakersList(state);
                await TestActiveBakersList(state);
            }
        }

        protected virtual Task TestParticipation(L1Chain state) => Task.CompletedTask;

        protected virtual Task TestDalParticipation(L1Chain state) => Task.CompletedTask;
        
        protected virtual Task TestCycle(L1Chain state, Cycle cycle) => Task.CompletedTask;

        protected virtual async Task TestBakersList(L1Chain state)
        {
            var local = Cache.Addresses.GetBakers().ToList();
            var remote = (await Rpc.GetDelegatesAsync(state.Level)).EnumerateArray()
                .Select(x => x.GetString())
                .ToHashSet();

            if (local.Count != remote.Count)
                throw new Exception("Invalid bakers count");

            foreach (var baker in local)
                if (!remote.Contains(baker.Hash))
                    throw new Exception($"Invalid baker {baker.Hash}");
        }
        
        protected virtual async Task TestActiveBakersList(L1Chain state)
        {
            var local = Cache.Addresses.GetBakers().Where(x => x.Staked).ToList();
            var remote = (await Rpc.GetActiveDelegatesAsync(state.Level)).EnumerateArray()
                .Select(x => x.GetString())
                .ToHashSet();

            if (local.Count != remote.Count)
                throw new Exception("Invalid active bakers count");

            foreach (var baker in local)
                if (!remote.Contains(baker.Hash))
                    throw new Exception($"Invalid active baker {baker.Hash}");
        }

        protected virtual async Task TestGlobalCounter(int level, L1Chain state)
        {
            if ((await Rpc.GetGlobalCounterAsync(level)).RequiredInt32() != state.ManagerCounter)
                throw new Exception("Diagnostics failed: wrong global counter");
        }

        protected virtual async Task TestBaker(int level, L1Baker baker, L1Protocol proto)
        {
            var remote = await Rpc.GetDelegateAsync(level, baker.Hash);

            if (remote.RequiredInt64("balance") != baker.Balance)
                throw new Exception($"Diagnostics failed: wrong balance {baker.Hash}");

            if (remote.RequiredBool("deactivated") != !baker.Staked)
                throw new Exception($"Diagnostics failed: wrong baker state {baker.Hash}");

            var deactivationCycle = (baker.DeactivationLevel - 1) >= proto.FirstLevel
                ? proto.GetCycle(baker.DeactivationLevel - 1)
                : (await Cache.Blocks.GetAsync(baker.DeactivationLevel - 1)).Cycle;
            if (remote.RequiredInt32("grace_period") != deactivationCycle)
                throw new Exception($"Diagnostics failed: wrong baker grace period {baker.Hash}");
            
            if (remote.RequiredInt64("staking_balance") != baker.OwnDelegatedBalance + baker.ExternalDelegatedBalance)
                throw new Exception($"Diagnostics failed: wrong staking balance {baker.Hash}");

            TestDelegatorsCount(remote, baker);
        }

        protected virtual void TestDelegatorsCount(JsonElement remote, L1Baker local)
        {
            if (remote.RequiredArray("delegated_contracts").Count() != local.DelegatorsCount)
                throw new Exception($"Diagnostics failed: wrong delegators count {local.Hash}");
        }

        protected virtual async Task TestAddress(int level, L1Address address)
        {
            var remote = await Rpc.GetContractAsync(level, address.Hash);

            if (address is not L1Baker && remote.RequiredInt64("balance") != address.Balance
                - address.SmartRollupBonds - ((address as L1User)?.UnstakedBalance ?? 0))
                throw new Exception($"Diagnostics failed: wrong balance {address.Hash}");

            TestAddressBaker(remote, address);
            TestAddressCounter(remote, address);
        }
        
        protected virtual Task TestTicketBalance(int level, TicketBalance ticketBalance) => Task.CompletedTask;

        protected virtual void TestAddressBaker(JsonElement remote, L1Address local)
        {
            if (local.Type != AddressType.L1User)
                return;

            var remoteBaker = remote.Required("delegate").OptionalString("value");
            var localBaker = Cache.Addresses.GetBaker(local.BakerId);

            if (remoteBaker != localBaker?.Hash)
                throw new Exception($"Diagnostics failed: wrong baker {local.Hash}");
        }

        protected virtual void TestAddressCounter(JsonElement remote, L1Address local)
        {
            if (remote.RequiredInt64("balance") > 0 && remote.RequiredInt32("counter") != local.Counter)
                throw new Exception($"Diagnostics failed: wrong counter {local.Hash}");
        }
    }
}
