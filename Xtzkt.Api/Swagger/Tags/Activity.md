Everything that happened, from one of three angles: an account, a block, or an operation group.
Operations and token/ticket transfers come back merged into a single **stream** sorted by `id`,
so an entire flow can be reconstructed from one request.

### Good to know

- Every item carries an `activity` field telling you which kind it is, and therefore which model to expect.
- Noisy types are left out by default — attestations, autostaking and the like — so ask for them
  explicitly via `types` if you need them.
- Account activity matches the address in any role by default: sender, target, initiator, or just
  mentioned somewhere in the operation. Narrow it down with `roles`.
- These endpoints page by `cursor` rather than `offset`, which isn't supported here, and accept
  `id` and `timestamp` as the only sort fields.

### Tips

- `/v1/activity/opg` is what you want for an "operation details": one request returns the whole operation
  group (batch), including internal operations and the transfers they caused.
- `/v1/activity/block` is keyed by `level`, which exists on every chain, so add `chain` unless you
  really want all of them at once.
- Use `?sort=id.desc` for newest first, and pass the last item's `id` as `cursor` to get the next page.
