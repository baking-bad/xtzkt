Operations by kind, each with its own endpoint and its own set of filters. `migration` is here as well,
although it's produced by the protocol itself and has no hash, no sender and no fees of its own.

### Good to know

- Operations are polymorphic: a discriminator field — `layer`, `env`, `runtime` or `direction` —
  tells you where the operation ran, and therefore which extra fields to expect.
- Internal operations, made by contracts rather than by users, are returned alongside the top-level ones.
- 6-decimal amounts (mutez) are JSON numbers, while 18-decimal EVM amounts are JSON strings,
  so the JSON type alone tells you which decimals an amount is in.
- Cross-runtime transactions carry the sent and the received amount separately, in different decimals,
  plus the rounding loss between them.
- Failed operations are kept, not dropped — check `status` before treating an operation as something
  that actually happened.

### Tips

- To get everything that happened under one operation hash, including the transfers it caused,
  use `/v1/activity/opg` instead of querying each endpoint and stitching the results together.
- `anyof.sender.target=...` returns everything related to an address in a single request, instead of
  two queries you'd have to merge yourself.
- Use `select` to fetch only the fields you need — decoded parameters and storage can be bulky.
