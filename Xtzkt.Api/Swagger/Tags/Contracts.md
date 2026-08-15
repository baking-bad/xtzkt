Contract code and state: storages, bigmaps, contract events, and EIP-7702 delegations — the thing
that lets a plain address run a contract's code. A **bigmap** is a lazily-loaded map kept outside
a contract's main storage, which is where ledgers, allowances and token metadata usually live.

### Good to know

- Contract storage is snapshotted on every change, so `/v1/storages` is a history rather than
  just the current state.
- Bigmap content does not appear inside storage — there you'll only find the bigmap's integer
  pointer, and the content itself is fetched separately.
- Removed bigmap keys are kept with `active=false` and their last value, so the key list doubles
  as a record of everything a bigmap ever held.
- Bigmaps the indexer recognized carry `tags` such as `ledger`, which is the quickest way to find
  a token contract's balance map.
- Logs cover both EVM logs and Michelson events. `name` and `payload` are decoded only when the
  source is known, and `guessed=true` means the match was a guess against popular standards —
  treat those with care.

### Tips

- `/v1/bigmaps/keys?bigMap=...&active=true` gives the current content of a bigmap;
  drop `active` to include the keys that were removed.
- `/v1/bigmaps/updates?bigMap=...&keyHash=...` answers "how did this value get here"
  for one particular key.
- `/v1/storages?contract=...&current=true` gives just the latest storage of a contract.
- EVM logs are best filtered by `topic0` (the event signature hash) together with `address`;
  Michelson events are filtered by `name`.
