Everything that happened, from one of three angles: an account, a block, or an operation group.
Operations and token, ticket and bridge ticket transfers come back merged into a single **stream** sorted by `id`,
so an entire flow can be reconstructed from one request.

### Good to know

- Every item carries an `activity` field telling you which kind it is, and therefore which model to expect.
- Noisy types are left out by default — attestations, autostaking and the like — so ask for them
  explicitly via `types` if you need them, one by one or as a whole group (see below).
- Account activity matches the address in any role by default: sender, target, initiator, or just
  mentioned somewhere in the operation. Narrow it down with `roles`.
- `address` in account activity takes internal address ids, and an id pins an address to one chain.
  `address.hash` matches a hash on every chain it exists on, so ids are the way to pick exact addresses.
- These endpoints page by `cursor` rather than `offset`, which isn't supported here, and accept
  `id` and `timestamp` as the only sort fields.

### Tips

- `/v1/activity/opg` is what you want for an "operation details": one request returns the whole operation
  group (batch), including internal operations and the transfers they caused.
- `/v1/activity/block` is keyed by `level`, which exists on every chain, so add `chain` unless you
  really want all of them at once.
- Use `?sort=id.desc` for newest first, and pass the last item's `id` as `cursor` to get the next page.

### Type groups

Wherever `types` takes a type, it also takes a group, which stands for all of its types, noisy ones included.
Groups and types mix freely, e.g. `?types=manager,attestation`.

- `consensus` — `attestation`, `preattestation`, `baking`.
- `voting` — `proposal`, `ballot`.
- `anonymous` — `activation`, `dal_entrapment_evidence`, `double_baking`, `double_consensus`, `drain_delegate`,
  `nonce_revelation`, `vdf_revelation`.
- `manager` — `transaction`, `origination`, `delegation`, `reveal`, `staking`, `set_delegate_parameters`,
  `set_deposits_limit`, `update_secondary_key`, `increase_paid_storage`, `register_constant`, `transfer_ticket`,
  `dal_publish_commitment`, `deposit`, `smart_rollup_add_messages`, `smart_rollup_cement`, `smart_rollup_execute`,
  `smart_rollup_originate`, `smart_rollup_publish`, `smart_rollup_recover_bond`, `smart_rollup_refute`,
  plus token, ticket and bridge ticket transfers: `token_transfer`, `ticket_transfer`, `bridge_ticket_transfer`.
- `implicit` — `migration`, `attestation_reward`, `dal_attestation_reward`, `autostaking`, `revelation_penalty`,
  `subsidy`.
- `all` — every type of every group above.

A group grows as new types are added to it, so be ready to meet `activity` kinds you haven't seen before.
