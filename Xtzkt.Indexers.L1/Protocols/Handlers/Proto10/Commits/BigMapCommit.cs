using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto10
{
    class BigMapCommit : Proto01.BigMapCommit
    {
        public BigMapCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override BigMapTag GetTags(L1Contract contract, TreeView node)
        {
            var tags = base.GetTags(contract, node);

            // custom handler for QUIPU
            if (contract.Hash == "KT193D4vozYnhGJQVtw7CoxxqphqUEEwK6Vb" &&
                (node.Value as MichelineInt)!.Value == 12043) // %account_info
                tags |= BigMapTag.Ledger11;

            return tags;
        }
    }
}
