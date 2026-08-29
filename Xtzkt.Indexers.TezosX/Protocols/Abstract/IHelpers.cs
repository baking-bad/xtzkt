using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Abstract;

public interface IHelpers
{
    Task<MetaBlock> GetMetaBlock(int level);

    #region fees
    BigInteger GetDaFee(JsonElement tx, bool isDelayedOp);
    BigInteger GetGasFee(BigInteger effectiveGasPrice, int gasUsed, BigInteger daFee);
    #endregion

    #region addresses
    Task<XEvmAddress> GetOrCreateXEvmAddress(string hash);
    Task RemoveXEvmAddress(XEvmAddress address);

    Task<XEvmUser> GetOrCreateXEvmUser(string hash);
    Task<XEvmUser> CreateXEvmUser(string hash);
    Task RemoveXEvmUser(XEvmUser user);

    Task<XEvmAlias> GetOrCreateXEvmAlias(string hash, XMichelsonAddress owner);
    Task RemoveXEvmAlias(XEvmAlias alias, XMichelsonAddress owner);

    Task<XEvmContract> GetOrCreateXEvmContract(string hash);
    Task<XEvmContract> CreateXEvmContract(string hash, XEvmAddress creator);
    XEvmContract UpgradeToXEvmContract(XEvmUser ghost, XEvmAddress creator);
    Task RemoveXEvmContract(XEvmContract contract, XEvmAddress creator);

    Task<XMichelsonAddress> GetOrCreateXMichelsonAddress(string hash);
    Task<XMichelsonAddress> GetCachedOrCreateXMichelsonAddress(string hash);
    Task<XMichelsonAddress> GetCachedOrCreateXMichelsonAddress(string hash, XBlock block);
    Task RemoveXMichelsonAddress(XMichelsonAddress address);

    Task<XMichelsonUser> GetOrCreateXMichelsonUser(string hash);
    Task RemoveXMichelsonUser(XMichelsonUser user);

    Task<XMichelsonAlias> GetOrCreateXMichelsonAlias(string hash, XEvmAddress owner);
    Task RemoveXMichelsonAlias(XMichelsonAlias alias, XEvmAddress owner);

    Task<XMichelsonContract> CreateXMichelsonContract(string hash, XMichelsonAddress creator);
    Task RemoveXMichelsonContract(XMichelsonContract contract, XMichelsonAddress creator);

    Task<XEvmContract> BootstrapEvmPrecompile(string address, string abiPath, XAddress? creator, XChain state);
    Task RemoveEvmPrecompile(string address, XChain state);
    Task<XEvmContract> UpgradeEvmPrecompile(string address, string abiPath, XChain state);
    Task DowngradeEvmPrecompile(string address, XChain state);
    Task BootstrapEvmUser(string address, XChain state);
    Task RemoveEvmUser(string address, XChain state);
    #endregion
}
