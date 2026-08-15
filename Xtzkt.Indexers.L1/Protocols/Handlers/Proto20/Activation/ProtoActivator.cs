using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto20
{
    partial class ProtoActivator : Proto19.ProtoActivator
    {
        public ProtoActivator(ProtocolHandler proto) : base(proto) { }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            // nothing to upgrade
        }

        protected override Task MigrateContext(L1Chain state)
        {
            // nothing to migrate
            return Task.CompletedTask;
        }

        protected override Task RevertContext(L1Chain state)
        {
            // nothing to revert
            return Task.CompletedTask;
        }
    }
}
