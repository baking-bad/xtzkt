using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto05;

class ProtoActivator(ProtocolHandler proto) : ProtocolCommit(proto), IActivator
{
    public async Task ActivateEvmContext(XChain state)
    {
        #region protocol
        var protocol = new XProtocol
        {
            Id = Cache.Chain.NextProtocolId(),
            ChainId = state.Id,
            Hash = state.Kernel,
            Version = Proto.Version,
            FirstLevel = 0,
            LastLevel = 0,
            MinBlockTimeMs = 500,
            MaxBlockTimeMs = 6000,
            HardEvmBlockGasLimit = 1L << 50,
            HardEvmOperationGasLimit = 30_000_000,
            DaFeePerByte = 4,
            DaFeePerByte18 = new BigInteger(4_000_000_000_000),
        };

        Context.Block.ProtocolId = protocol.Id;
        Context.Protocol = protocol;

        Cache.Protocols.Add(protocol);
        Db.Protocols.Add(protocol);
        #endregion

        #region precompiles
        var nullAddress = Helpers.BootstrapEvmPrecompile(EvmRuntime.NullAddress, [], "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/NullAbi.json", null, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.XtzBridge, [], "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/XtzBridgeAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.FaBridge, [], "Protocols/Handlers/Proto05/Runtimes/Evm/Precompiles/FaBridgeAbi.json", nullAddress, state);
        #endregion

        #region bootstrap for testnets
        await Helpers.BootstrapEvmUser("0x6ce4d79d4e77402e1ef3417fdda433aa744c6e1c", state);
        await Helpers.BootstrapEvmUser("0xb53dc01974176e5dff2298c5a94343c2585e3c54", state);
        await Helpers.BootstrapEvmUser("0x9b49c988b5817be31dfb00f7a5a4671772dcce2b", state);
        #endregion
    }

    public async Task DeactivateEvmContext(XChain state)
    {
        #region bootstrap for testnets
        await Helpers.RemoveEvmUser("0x6ce4d79d4e77402e1ef3417fdda433aa744c6e1c", state);
        await Helpers.RemoveEvmUser("0xb53dc01974176e5dff2298c5a94343c2585e3c54", state);
        await Helpers.RemoveEvmUser("0x9b49c988b5817be31dfb00f7a5a4671772dcce2b", state);
        #endregion

        #region precompiles
        await Helpers.RemoveEvmPrecompile(EvmRuntime.FaBridge, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.XtzBridge, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.NullAddress, state);
        #endregion

        #region protocol
        await Db.Protocols
            .Where(x => x.ChainId == state.Id && x.Hash == state.Kernel)
            .ExecuteDeleteAsync();

        Cache.Chain.ReleaseProtocolId();
        await Cache.Protocols.ResetAsync();
        #endregion
    }

    public Task ActivateMichelsonContext(XChain state, MetaBlock block)
    {
        // there was no Michelson runtime in old protocols
        throw new NotImplementedException();
    }

    public Task DeactivateMichelsonContext(XChain state)
    {
        // there was no Michelson runtime in old protocols
        throw new NotImplementedException();
    }
}
