Accounts and addresses of all kinds — users, bakers, contracts, smart rollups — with all their details.
An **address** is a single hash on a single chain, while an **account** is a group of addresses belonging
to the same party (note that in classic TzKT these two were the same thing).

### Good to know

- The same hash may exist on several chains, so an address is identified by `hash + chain`, not by `hash` alone.
- Tezos X has multiple runtimes (Michelson and EVM), and an address in one runtime can have **aliases**
  in the others: a Michelson address can have an EVM alias `0x...`, an EVM address can have a Michelson
  alias `KT1...`.
- Both apply at once, so an account can span several chains and have aliases on several runtimes.
- Every address keeps its own balance and counters — an account is a set of addresses, not a sum of them.

### Tips

- `/v1/accounts/{address}` accepts any address of the account, including aliases, and returns all of them at once.
- `/v1/addresses` works with individual addresses. Use the `chain` filter when a hash exists on more than one chain.