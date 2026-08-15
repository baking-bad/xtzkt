The indexed chains themselves and their structure: chains with their sync state, blocks, protocols,
and the baker software that produced the blocks. A **chain** is one indexed network — Tezos L1 or a
Tezos X rollup — and pretty much everything else in the API is scoped to one.

### Good to know

- Each chain has an internal `id`, assigned by whoever runs the indexer, and a publicly known `chainId`.
  Filters accept both, but a bare `?chain=0` means the internal one.
- `level` is the last block indexed, `knownLevel` the last one the node knows about — compare them
  to see whether the indexer is caught up.
- Levels are per-chain: the same `level` exists on every chain, so filtering by `level` alone
  returns one block per chain.
- On Tezos X a protocol is a kernel version, identified by its root hash, and its constants are
  a different set from the Tezos L1 ones.
- Tezos X blocks have two hashes — `hash` is the EVM one, `michelsonHash` the Michelson one —
  and a block can be looked up by either.

### Tips

- Start at `/v1/chains` to find out which chains this instance indexes, and which `chain` values
  the other endpoints will accept.
- Read protocol constants from the protocol the block belongs to, not from the current one:
  they change from protocol to protocol, and today's values did not hold in the past.
- To see what happened inside a block, use `/v1/activity/block` instead of querying each
  operation endpoint separately.
