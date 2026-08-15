# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Prerequisites
- .NET SDK 10.0
- PostgreSQL 17+

## Build & Run

```bash
# Build all projects
dotnet build Xtzkt.slnx

# Run the L1 indexer
dotnet run --project Xtzkt.Indexers.L1

# Run the TezosX indexer
dotnet run --project Xtzkt.Indexers.TezosX

# Run the API
dotnet run --project Xtzkt.Api

# Run the metadata service
dotnet run --project Xtzkt.Services.Metadata
```

> The shell here is PowerShell on Windows. The `dotnet` commands above work as-is in both PowerShell and bash; use PowerShell syntax for any scripting around them.

### Docker

Each runnable project has a `Dockerfile`, and **the build context is always the repository root**, never the project directory:

```bash
docker build -f Xtzkt.Api/Dockerfile -t xtzkt-api .

# or the whole stack
docker compose build
docker compose up -d
```

Things to keep intact when touching these files:
- The `restore` stage copies **all** `.csproj` files and restores `Xtzkt.slnx`. Restoring a single project whose `ProjectReference` targets are missing does not fail — it logs `Skipping project ... because it was not found` and leaves them unrestored, so the restore silently repeats during publish.
- `base` installs `curl` for the `HEALTHCHECK` probe, and that `RUN` has to stay **before** `USER $APP_UID` — apt needs root.
- Those two blocks are byte-identical across the four Dockerfiles, so their layers are built once and reused; keep them that way.
- There is deliberately no `dotnet build` stage: `dotnet publish` recompiles anyway.
- `base` must remain the first runtime stage — Visual Studio's `Container (Dockerfile)` launch profile runs it in fast mode.
- The app `HEALTHCHECK`s are declared in the images, not in `docker-compose.yml`, which only consumes them through `depends_on: condition: service_healthy`. Don't add duplicates there.
- `AddHealthChecks()` + `MapHealthChecks("/health")` register **no** checks — an empty report is `Healthy`, so `/health` is a plain 200 for as long as the host serves. It only starts answering once the init loop (migrations / schema check / chain init) is done, since all of that runs before `app.Run()`; that is what `--start-period=300s` covers.

## Verifying changes

There are **no test projects** in this repository. `dotnet build Xtzkt.slnx` is the only automated check — always run it after edits and confirm it succeeds before declaring work done. Correctness of indexing logic is verified by review (see the Apply/Revert invariant below) and the optional `Diagnostics` consistency checks in each protocol handler.

## Architecture

This is an aggregated Tezos blockchain indexer, combining data from multiple chains/layers (Tezos L1 and Tezos X) in a single PostgreSQL database and providing API to access the data.

### Projects

| Project | Type | Role |
|---|---|---|
| `Xtzkt.Utils` | Class library | Shared low-level utilities; has no project dependencies of its own and is referenced broadly by the other projects |
| `Xtzkt.Data` | Class library | EF Core models, DB context, migrations |
| `Xtzkt.Indexers.Common` | Class library | Shared components, helpers, extensions |
| `Xtzkt.Indexers.L1` | ASP.NET Core | Tezos L1 blockchain indexer |
| `Xtzkt.Indexers.TezosX` | ASP.NET Core | Tezos X (L2 rollup) indexer |
| `Xtzkt.Api` | ASP.NET Core | REST-like API |
| `Xtzkt.Services.Metadata` | ASP.NET Core | Standalone service that is the **single** home for all metadata indexing (token and contract, across all chains/layers). Indexers themselves no longer index metadata. |

### Data models

All data models live in `Xtzkt.Data/Models`. Every model is configured via `ModelBuilder` extension, placed in the same file. For complex models, for example `Address`, a TPH hierarchy is used.

### DB Migrations

EF Core migrations live in `Xtzkt.Data/Migrations/`. The startup project for `dotnet ef` must be either `Xtzkt.Indexers.L1` or `Xtzkt.Indexers.TezosX` (both contain `DesignTimeDbContextFactory`). Indexers auto-migrate on startup and will refuse to start if the DB schema is ahead of the code.

Non-indexer services (`Xtzkt.Api`, `Xtzkt.Services.Metadata`) do **not** apply migrations. `Xtzkt.Services.Metadata` only verifies schema compatibility on startup: it exits with an error if its code and the DB schema have diverged, and waits (retrying) for the indexer to apply any pending migrations before proceeding.

```bash
# Add a new migration
dotnet ef migrations add <Name> --project Xtzkt.Data --startup-project Xtzkt.Indexers.L1

# Apply pending migrations manually
dotnet ef database update --project Xtzkt.Data --startup-project Xtzkt.Indexers.L1
```

### Index ownership

Indexes are split by consumer, and each consumer owns its own — an index declared in the wrong place is dead weight that the other components still pay for on every write.

| Prefix | Owner | Where it is declared |
|---|---|---|
| `IX_` | Indexers (`Xtzkt.Indexers.*`) | `HasIndex` in `Xtzkt.Data/Models`, shipped in migrations |
| `MX_` | `Xtzkt.Services.Metadata` | `CREATE INDEX CONCURRENTLY IF NOT EXISTS` in `StoreService.Ensure*ResolverIndexes`, run at startup |
| `AX_` | `Xtzkt.Api` | `init.db` script, per deployment — **not** in migrations |

So `Xtzkt.Data/Models` must carry **only** indexes that an indexer query actually uses. Before adding one there, find the query it serves; before removing one, check all three consumers. Write API queries as if their indexes existed rather than adding them to the models.

### Configuration

Indexers and services read config in this order, each source overriding the previous one: `appsettings.json` → `appsettings.{Environment}.json` → environment variables → prefixed env vars (`XTZKT_L1_*` for L1, `XTZKT_TEZOSX_*` for TezosX, `XTZKT_API_*` for the API, `XTZKT_METADATA_*` for the metadata service) → `ASPNETCORE_*` env vars → command-line args.

Every `Program.cs` builds that chain by hand after `builder.Configuration.Sources.Clear()`. The `AddEnvironmentVariables("ASPNETCORE_")` line is load-bearing and must not be dropped: `ASPNETCORE_URLS` / `ASPNETCORE_HTTP_PORTS` reach the `URLS` / `HTTP_PORTS` config keys only through that prefix, and without it the app silently ignores both and binds the default `localhost:5000` — which in a container means loopback-only, unreachable from outside.

`Chain.Id` setting must be an integer (0–7) and must be unique for every indexer instance.

`Xtzkt.Indexers.TezosX` requires two node endpoints (`TezosNode`, `EvmNode`), unlike `Xtzkt.Indexers.L1` which only needs `TezosNode`.

### Multi-chain support

Each indexer instance handles one chain identified by `Chain.Id`. All data models have `ChainId` property, linking every record to a particular `Chain.Id`, so data from multiple chains coexist in the same database with no conflicts/collisions.

### Indexer loop

The `ObserverService` (hosted background service) drives indexing:
1. Initializes app state from the `Chains` table, by getting the `Chain` with the configured `Id`
2. Starts a `HeadNotifier` (polling or streaming, configurable) that fires `OnHead` events
3. On each new head runs a `ProtocolHandler` that fetches, processes and writes block data to the DB

### Indexing logic

Indexing logic lives in a **protocol handler** (`ProtocolHandler` base, concrete subclasses `Proto01Handler`–`Proto24Handler` for L1; `Proto01Handler` for TezosX). For each Tezos protocol version there is a dedicated protocol handler. Each protocol handler contains the following components:
- `Commits` - a bunch of `*Commit` classes, processing each aspect of a block and containing symmetric apply and revert logic (revert is triggered on chain reorg, when the canonical chain diverges from already-indexed blocks)
- `Rpc` – fetches raw data from the node
- `Validator` – validates block data
- `Helpers` – shared logic
- `Diagnostics` – optional consistency checks

Protocol handler components inherit most logic from previous versions, and only override changed logic. `Proto{N}Handler` (and each of its components) derives from `Proto{N-1}`. So to find where behavior X is implemented for a given protocol, walk *down* the chain from that protocol to the first `override` — the logic in effect is the nearest override at or below the target version.

#### Apply/Revert invariant (read before touching any `*Commit`)

Every `*Commit` must have symmetric `Apply`/`Revert`: `Revert` exactly undoes the DB writes and cache mutations of `Apply`. This is the dominant source of bugs in the indexer — review both directions together.

Accepted asymmetries — these are **intentional**, do not "fix" them:
- `Cache.Statistics` (`TotalActivated`/`TotalCreated`/`TotalFrozen`/…) is incremented in `Apply` but not decremented in `Revert` — the statistics cache is rebuilt on startup.
- `LastLevel = block.Level` in `Revert` (Tickets/Tokens commits) instead of restoring the previous value — known systemic cosmetic issue.
- `contract.Tags` / `bigmap.Tags` ledger discovery is not reverted (`TokensCommit`) — architectural limitation.
- `SoftwareCommit.Revert` doesn't restore `baker.SoftwareId` / remove zero-count `Software` rows (explicit TODO, cosmetic).

### Known intentional patterns in `Xtzkt.Indexers.TezosX` (do NOT flag as bugs)

EVM semantics that the code deliberately encodes — each of these looks like an omission until you know why.

- **`CALLCODE` and `DELEGATECALL` don't move value**, even with a non-zero `value` in the trace — the callee's code runs in the caller's context. `SELFDESTRUCT` to self burns the balance only if the contract was created in the same transaction (EIP-6780); otherwise it's a no-op transfer. See `IsSelfDestructWithBurn`.
- **Self-destructed contracts are not marked destroyed.** The row keeps its `Kind`/`CodeHash`/ABI and stays counted in `creator.ContractsCount`. Verified harmless for indexing: balances stay consistent with the node, and the `ReOriginated` path in `OriginationCommit` relies on the address still being a contract — adding destruction tracking would change that path, so it's a design decision, not a fix.

### JSON number formats in `Xtzkt.Api`

| CLR type | JSON | How |
|---|---|---|
| `int` | number | default |
| `long` / `long?` | number | default |
| `long` / `long?` **entity id** (`Id` and `*Id` references) | **string** | explicit `[JsonConverter(typeof(Int64StringConverter))]` / `Int64StringNullableConverter` on the property |
| `BigInteger` / `BigInteger?` | **string** | `BigIntegerConverter` / `BigIntegerNullableConverter`, registered globally in `Program.cs` |

The reason ids are strings is that they are `bigint` in the DB and can exceed the JS client's `Number.MAX_SAFE_INTEGER` (2^53). Such a value can't be fixed client-side cheaply — precision is lost inside `JSON.parse`, before any reviver runs. Everything else that is `long` (mutez amounts, fees, counts, …) is safely below 2^53 and stays a number; the 18-decimal EVM values are `BigInteger`, so they are strings anyway.

A useful side effect: JSON type also disambiguates the decimals of an amount — 6-decimal (L1 / Michelson) amounts are numbers, 18-decimal (EVM) amounts are strings.

There is **no** global `long` → string converter, so there are two ways to silently break this — both produce valid JSON and only lose precision on large ids, in the client:
- a new model property holding an entity id must carry the `Int64StringConverter` attribute explicitly, otherwise it serializes as a number;
- in the `?select=` path values are boxed into `object?[][]`, which bypasses property attributes — ids there must be stringified at the assignment site (`row.Id.ToString()` / `row.TransactionId?.ToString()`).

### Known intentional patterns in `Xtzkt.Api` (do NOT flag as bugs)

- `ModelBindingContextExtension.TryGet*List` uses `FirstValue`: only the comma-separated form `?param.in=a,b,c` is supported, not the repeated-key form `?param.in=a&param.in=b`.
- `INormalizable.Normalize()` (on `SortParameter`, `LayerParameter`, `RuntimeParameter`, …) produces a compact cache key (e.g. `id.a` / integer enum value), NOT a round-trippable query string.
