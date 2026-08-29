using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class ProtoActivator(ProtocolHandler proto) : ProtocolCommit(proto), IActivator
{
    public async Task ActivateEvmContext(XChain state)
    {
        await ActivateEvmProtocol(state);
        await ActivateEvmPrecompiles(state);
        await ActivateEvmTestAccounts(state);
    }

    public async Task DeactivateEvmContext(XChain state)
    {
        await DeactivateEvmTestAccounts(state);
        await DeactivateEvmPrecompiles(state);
        await DeactivateEvmProtocol(state);
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

    async Task ActivateEvmProtocol(XChain state)
    {
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
    }

    async Task DeactivateEvmProtocol(XChain state)
    {
        await Db.Protocols
            .Where(x => x.ChainId == state.Id && x.Hash == state.Kernel)
            .ExecuteDeleteAsync();

        Cache.Chain.ReleaseProtocolId();
        await Cache.Protocols.ResetAsync();
    }

    public const string NullAddressAbi = "Protocols/Handlers/Proto01/Runtimes/Evm/Precompiles/NullAddressAbi.json";
    public const string XtzBridgeAbi = "Protocols/Handlers/Proto01/Runtimes/Evm/Precompiles/XtzBridgeAbi.json";

    protected virtual List<(string Address, string AbiPath)> EvmPrecompiles => [
        (EvmRuntime.NullAddress, NullAddressAbi),
        (EvmRuntime.XtzBridge,   XtzBridgeAbi),
    ];

    async Task ActivateEvmPrecompiles(XChain state)
    {
        var precompiles = EvmPrecompiles;
        var nullAddress = await Helpers.BootstrapEvmPrecompile(precompiles[0].Address, precompiles[0].AbiPath, null, state);
        for (int i = 1; i < precompiles.Count; i++)
            await Helpers.BootstrapEvmPrecompile(precompiles[i].Address, precompiles[i].AbiPath, nullAddress, state);
    }

    async Task DeactivateEvmPrecompiles(XChain state)
    {
        var precompiles = EvmPrecompiles;
        for (int i = precompiles.Count - 1; i >= 0; i--)
            await Helpers.RemoveEvmPrecompile(precompiles[i].Address, state);
    }

    static List<string> EvmTestAccounts => [
        "0x6ce4d79d4e77402e1ef3417fdda433aa744c6e1c",
        "0xb53dc01974176e5dff2298c5a94343c2585e3c54",
        "0x9b49c988b5817be31dfb00f7a5a4671772dcce2b",
    ];

    async Task ActivateEvmTestAccounts(XChain state)
    {
        var accounts = EvmTestAccounts;
        for (int i = 0; i < accounts.Count; i++)
            await Helpers.BootstrapEvmUser(accounts[i], state);
    }

    async Task DeactivateEvmTestAccounts(XChain state)
    {
        var accounts = EvmTestAccounts;
        for (int i = accounts.Count - 1; i >= 0; i--)
            await Helpers.RemoveEvmUser(accounts[i], state);
    }
}
