using System.Text.Json;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public async Task Activate(L1Chain state, JsonElement rawBlock)
        {
            if (state.Level == 1) // bootstrap
            {
                var (protocol, parameters) = BootstrapProtocol(rawBlock);

                var addresses = await BootstrapAddresses(protocol, parameters);
                var cycles = BootstrapCycles(protocol, addresses, parameters);
                var (bakingRights, attestationRights) = await BootstrapBakingRights(protocol, addresses, cycles);
                BootstrapDelegationSnapshots(addresses);
                BootstrapSnapshotBalances(addresses);
                BootstrapBakerCycles(protocol, addresses, cycles, bakingRights, attestationRights);
                BootstrapStakerCycles(protocol, addresses);
                BootstrapDelegatorCycles(protocol, addresses);
                BootstrapVoting(protocol, addresses);
                BootstrapCommitments(parameters);
                await ActivateContext(state);
            }
            else // upgrade
            {
                await UpgradeProtocol(state);
                await MigrateContext(state);
            }
        }

        public async Task Deactivate(L1Chain state)
        {
            if (state.Level == 1) // clear
            {
                await DeactivateContext(state);
                await ClearCommitments();
                await ClearVoting();
                await ClearSnapshotBalances();
                await ClearDelegationSnapshots();
                await ClearBakerCycles();
                await ClearStakerCycles();
                await ClearDelegatorCycles();
                await ClearCycles();
                await ClearBakingRights();
                await ClearAddresses();
                await ClearProtocol();
            }
            else // downgrade
            {
                await RevertContext(state);
                await DowngradeProtocol(state);
            }
        }

        protected virtual Task ActivateContext(L1Chain state) => Task.CompletedTask;
        protected virtual Task DeactivateContext(L1Chain state) => Task.CompletedTask;
        protected virtual Task MigrateContext(L1Chain state) => Task.CompletedTask;
        protected virtual Task RevertContext(L1Chain state) => Task.CompletedTask;
    }
}
