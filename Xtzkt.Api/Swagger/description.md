# Introduction

0xTzKT API provides you with convenient and flexible access to the Tezos L1 and Tezos X (L2) data, processed and indexed by its own indexer. 
You can fetch all historical data via REST API, or subscribe for real-time data via WebSocket API.

0xTzKT Indexer and API are [open-source](https://github.com/baking-bad/xtzkt), so don't be afraid to depend on the third-party service,
because you can always clone, build and run it yourself to have full control over all the components.

Feel free to contact us if you have any questions or feature requests.
Your feedback is much appreciated!

- Discord: https://discord.gg/aG8XKuwsQd
- Telegram: https://t.me/baking_bad_chat
- X: https://x.com/TezosBakingBad
- Email: hello@bakingbad.dev

And don't forget to star 0xTzKT [on GitHub](https://github.com/baking-bad/xtzkt) if you like it 😊

# Get Started

## Pagination and sorting

Every endpoint that returns a list is paginated and sorted the same way, with four query parameters:
`sort`, `cursor`, `offset` and `limit`. There are two ways to walk a list — **cursor** (keyset) and
**offset** — and the short version is: use `cursor` for anything longer than a few pages.

### Sorting

- `?sort=<field>` sorts ascending, `?sort=<field>.desc` descending. Several fields are allowed:
  `?sort=balance.desc,id.asc`.
- Only whitelisted fields can be sorted by, because each of them is backed by an index. The list
  differs per endpoint and is spelled out under "Allowed fields" in the `sort` parameter of each one.
  Anything else is a `400`, which also names the fields that endpoint does accept.
- `id` is always appended as the last sort field, unless you sorted by it explicitly. So the order
  is always strict — items with equal `balance` never shuffle between two identical requests — and
  a cursor can never skip or duplicate a row.
- The default is `?sort=id`, ascending. For newest first use `?sort=id.desc`.

### Cursor

A cursor is not an opaque token: it is the **sort field values of the last item from the previous
page**, comma-separated, one value per `sort` field.

```
GET /v1/blocks?sort=id.desc&limit=100                    → last item has id 5261000
GET /v1/blocks?sort=id.desc&limit=100&cursor=5261000     → the next 100 blocks
```

```
GET /v1/tokens/balances?sort=balance.desc,id&limit=100   → last item: balance 1000, id 1234
GET /v1/tokens/balances?sort=balance.desc,id&limit=100&cursor=1000,1234
```

- Pass a value for **every** field in `sort`. Fewer values are accepted, but then paging happens on
  that prefix only and the rest of the tie group is skipped — pass them all and this can't happen.
- Cursor paging stays equally fast at any depth, and it is also more correct than `offset`: when new
  items arrive between two requests, an offset shifts the whole list and you get duplicates or gaps,
  while a cursor stays anchored to the item you left off at.
- To download a whole list, loop until the response is empty:
  `?sort=id&limit=10000` and then `cursor=<id of the last item>` on every next request.

### Offset and limit

- `?limit=` is between `1` and `10000`, `100` by default.
- `?offset=` is between `0` and `100000`. It is capped on purpose: DB materializes and throws
  away the skipped rows, so an offset costs time proportional to its value, and past a certain depth
  a single request would occupy the database for seconds.
- The cap is roughly 1000 pages of 100 items, which no interactive listing ever reaches. If you do
  hit it, you're bulk-reading — which is exactly what `cursor` is for, and it's faster anyway.
