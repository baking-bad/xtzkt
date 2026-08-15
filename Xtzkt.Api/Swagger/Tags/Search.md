A single entry point that takes whatever a user typed — an address, a hash, a level, an alias —
and returns the entities it can refer to, best match first. Meant for a search box, where you don't
know in advance what kind of thing the input is.

### Good to know

- The query is interpreted by its shape, and the modes are exclusive: something that looks like a hash
  is only looked up by hash, never treated as an alias.
- A number is treated as a block level. Anything else is matched against address aliases and token
  names and symbols, tolerating typos and partial words.
- Searching for a contract address also returns the tokens that contract issued, not just the address itself.
- Results are identity records, not full entities — enough to render a suggestion list, not a whole page.
- `limit` caps the total number of results, however many scopes were searched.

### Tips

- Narrow things down with `scopes` when you know what you're after, e.g. `?scopes=address,token`
  for a wallet-style picker.
- Add `chain` to search a single chain instead of all the indexed ones.
- Every result carries a `scope` field telling you what it is; take its `hash` or `id` from there
  and fetch the full entity from the matching endpoint.
