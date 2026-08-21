Assets, tokens, tickets and bridge tickets: what they are, who holds them, and how they moved. An **asset**
is a curated group of tokens representing the same thing — like USDC, deployed as separate contracts on
Tezos L1 and on Tezos X.

### Good to know

- Nothing on-chain links deployments of the same asset — each one is issued on its own — so assets are
  grouped by hand, and a token stays an asset of its own until someone groups it.
- Tokens of one asset are independent contracts, each with its own supply, holders and decimals: the same
  asset often has 6 decimals on Tezos L1 and 18 in EVM. Nothing is summed up across them.

### Bridge tickets are a third thing

When an FA token is bridged from Tezos L1 to Tezos X, what the user ends up holding is an ordinary ERC-20
in `/v1/tokens`. Behind it the bridge keeps its own accounting entry, and *that* is neither a token nor a
Michelson ticket, so it lives in `/v1/bridge_tickets`:

- The real ticket stays on L1, held by the rollup's own address — see who holds it in
  `/v1/tickets/balances`. On Tezos X the bridge only keeps an accounting entry, identified by the ticket's
  `weakHash`, which is the same on both layers and is how you match the two. The hash doesn't cover the
  ticket content type, so treat it as a lookup key rather than an identity.
- Balances only ever move in two ways: credited when funds are bridged in, debited when they're withdrawn.
  Bridge tickets are never transferred between addresses — the transferable thing is the ERC-20 minted by
  the proxy contract.
- So the holder of a bridge ticket balance is normally that proxy contract, backing the wrapped supply.
  A regular address holds one only when the deposit had no proxy, or the proxy call failed — and then this
  balance is the only place the deposited funds exist on the chain. For the same reason a depositor's own
  `/v1/activity` feed usually shows the deposit but no `bridge_ticket_transfer`: that row belongs to the proxy.
- A deposit is credited immediately when it has no proxy, otherwise it's queued and credited later by a
  `claim` transaction. A queued deposit carries a `depositId`, and once it's claimed, the claiming
  transaction is on it as `claimTransactionId` — so `?depositId.ne=null&claimTransactionId=null`
  is the queue itself: funds that were bridged in and are still sitting on the bridge.
  `xtz` deposits never produce a bridge ticket transfer — they credit the native balance instead.
- Amounts here are ticket units: unlike EVM values they are not scaled by 18 decimals, even though they
  are rendered as strings.

### Tips

- `/v1/assets/{contract}/{tokenId}` accepts any token of the asset and returns all of them at once.
- If the pair `contract + tokenId` matches tokens on several chains (which practically never happens),
  you'll get a 400 asking you to add the `chain` parameter, or use `/v1/assets/{tokenId}` with internal `Token.Id` instead.
- You can use returned token ids in `/v1/tokens/balances?token.id.in=...` and `/v1/tokens/transfers?token.id.in=...`
  to get a single feed across the whole asset and build aggregated statistics.
