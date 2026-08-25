using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

public class Proto02Commit(ProtocolHandler protocol) : ProtocolCommit(protocol)
{
    // The michelson runtime doesn't exist in this era, so there's nothing to bind aliases to.
    protected override Task BindAliases(XEvmAddress address) => Task.CompletedTask;
    protected override Task UnbindAliases(XEvmAddress address) => Task.CompletedTask;
    protected override Task BindAliases(XMichelsonAddress address) => Task.CompletedTask;
    protected override Task UnbindAliases(XMichelsonAddress address) => Task.CompletedTask;
}
