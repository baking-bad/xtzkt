using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto11;

class EvmRuntime : Proto10.EvmRuntime
{
    #region special addresses
    public override string RescueGuard => "0x3822d2fe3dce6ce4f4c7f274e2415fa3fae66c3e";
    #endregion

    #region helpers
    public override string GetAlias(string address)
    {
        return Runtimes.GetRecoverableEvmAlias(address) ?? Runtimes.GetLegacyEvmAlias(address);
    }

    public override string[] GetAliases(string address)
    {
        var legacyAlias = Runtimes.GetLegacyEvmAlias(address);
        return Runtimes.GetRecoverableEvmAlias(address) is string alias ? [alias, legacyAlias] : [legacyAlias];
    }
    #endregion
}
