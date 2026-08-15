using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto11
{
    class ProtoActivator : Proto10.ProtoActivator
    {
        public ProtoActivator(ProtocolHandler proto) : base(proto) { }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev) { }
        protected override Task MigrateContext(L1Chain state) => Task.CompletedTask;
        protected override Task RevertContext(L1Chain state) => Task.CompletedTask;
    }
}
