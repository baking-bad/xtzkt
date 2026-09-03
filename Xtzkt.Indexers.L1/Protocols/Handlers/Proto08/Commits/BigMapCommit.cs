using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto08
{
    class BigMapCommit : Proto01.BigMapCommit
    {
        public BigMapCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override BigMapTag GetTags(L1Contract contract, TreeView node)
        {
            var tags = base.GetTags(contract, node);

            // custom handler for Tezos Domains
            if (contract.Hash == "KT1GBZmSxmnKJXGMdMLbugPfLyUPmuLSMwKS" &&
                (node.Value as MichelineInt)!.Value == 1264) // %records
                tags |= BigMapTag.Ledger12;

            return tags;
        }
    }
}
