using Netezos.Contracts;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto06
{
    class BigMapCommit : Proto01.BigMapCommit
    {
        public BigMapCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override BigMapTag GetTags(L1Contract contract, TreeView node)
        {
            var tags = base.GetTags(contract, node);

            // custom handler for tzBTC
            if (contract.Hash == "KT1PWx2mnDueood7fEmfbBDKx1D9BAnnXitn")
                tags |= BigMapTag.Ledger7;

            return tags;
        }
    }
}
