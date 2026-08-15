Assets, tokens and tickets: what they are, who holds them, and how they moved. An **asset** is a curated
group of tokens representing the same thing — like USDC, deployed as separate contracts on Tezos L1 and
on Tezos X.

### Good to know

- Nothing on-chain links deployments of the same asset — each one is issued on its own — so assets are
  grouped by hand, and a token stays an asset of its own until someone groups it.
- Tokens of one asset are independent contracts, each with its own supply, holders and decimals: the same
  asset often has 6 decimals on Tezos L1 and 18 in EVM. Nothing is summed up across them.

### Tips

- `/v1/assets/{contract}/{tokenId}` accepts any token of the asset and returns all of them at once.
- If the pair `contract + tokenId` matches tokens on several chains (which practically never happens),
  you'll get a 400 asking you to add the `chain` parameter, or use `/v1/assets/{tokenId}` with internal `Token.Id` instead.
- You can use returned token ids in `/v1/tokens/balances?token.id.in=...` and `/v1/tokens/transfers?token.id.in=...`
  to get a single feed across the whole asset and build aggregated statistics.
