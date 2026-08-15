# Tezos Multichain Indexer by Baking Bad
[![Made With](https://img.shields.io/badge/made%20with-C%23-success.svg?)](https://docs.microsoft.com/en-gb/dotnet/csharp/language-reference/)
[![License: MIT](https://img.shields.io/github/license/baking-bad/netezos.svg)](https://opensource.org/licenses/MIT)

0xTzKT is a multi-chain indexer and API for the [Tezos](https://tezos.com/) blockchain, created by the [Baking Bad](https://bakingbad.dev) team with huge support from the [Tezos Foundation](https://tezos.foundation/).

It's like [TzKT](https://github.com/baking-bad/tzkt), but new and better - it indexes both Tezos L1 and Tezos X, including EVM runtime, and aggregates all the data in a single place.

## It's under development

The indexer and API is still under development, so data correctness is not guaranteed and breaking changes are possible. Use it on your own risk.

## Running in Docker

Every service has a Dockerfile, and **the build context is always the repository root**:

```bash
docker build -f Xtzkt.Api/Dockerfile -t xtzkt-api .
```

The whole stack, including PostgreSQL, comes up via Compose. It works with no `.env` at all — every variable has a default — so copying the example is only needed to change something:

```bash
cp .env.example .env	# optional, every line there repeats a default
docker compose build
docker compose up -d
```

Not every service starts by default. Compose profiles select the set:

| Command | Services |
|---|---|
| `docker compose up` | `db`, `api`, `indexer-tezosx` |
| `docker compose --profile metadata up` | + `metadata` |
| `docker compose --profile full up` | + `metadata`, `indexer-l1` |

`COMPOSE_PROFILES` in `.env` fixes the choice so `--profile` need not be repeated. A single profiled service can also be started by name: `docker compose up indexer-l1`.

Inside the containers everything listens on port 8080 (`ASPNETCORE_HTTP_PORTS`, set in the Dockerfiles). On the host, Compose publishes `api` on 5000, `indexer-l1` on 5001, `indexer-tezosx` on 5002 and `metadata` on 5003 — all overridable through `.env`.

Configuration comes from `appsettings.json`. Everything in `appsettings.json` can be overridden by environment variable.
For example, `DipDupResolver.Sources[0].Network` in `appsettings.json` can be overriden by `DipDupResolver__Sources__0__Network` env var (note double underscore `__` for nesting).

## Installation (from source)

This guide is for Ubuntu 24.04, but even if you use a different OS, the installation process will likely be the same, except for the "Install packages" part.

### Install packages

#### Install Git

````sh
sudo apt update
sudo apt install git
````

#### Install .NET

````sh
sudo apt update
sudo apt install dotnet-sdk-10.0
````

#### Install Postgresql

````sh
sudo sh -c 'echo "deb https://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list'
wget --quiet -O - https://www.postgresql.org/media/keys/ACCC4CF8.asc | sudo apt-key add -

sudo apt update
sudo apt install -y postgresql-17
````

---

### Build, configure and run indexer for Tezos X

#### Clone repo

````sh
git clone https://github.com/baking-bad/xtzkt ~/xtzkt
````

#### Build Tezos X indexer

````sh
cd ~/xtzkt/Xtzkt.Indexers.TezosX/
dotnet publish -o ~/xtzkt-indexer-tezosx
````

#### Configure Tezos X indexer

Edit the configuration file `~/xtzkt-indexer-tezosx/appsettings.json`. What you basically need is to adjust the `EvmNode.Endpoint`, `TezosNode.Endpoint` and `ConnectionStrings.DefaultConnection`, if needed.

#### Run Tezos X indexer

````sh
cd ~/xtzkt-indexer-tezosx
dotnet Xtzkt.Indexers.TezosX.dll
````

That's it. If you want to run the indexer as a daemon, take a look at this guide: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0&tabs=linux-ubuntu#create-the-service-file.

### Build, configure and run indexer for Tezos L1

Suppose, you have already cloned the repo to `~/xtzkt` during the steps above.

#### Build L1 indexer

````sh
cd ~/xtzkt/Xtzkt.Indexers.L1/
dotnet publish -o ~/xtzkt-indexer-l1
````

#### Configure L1 indexer

Edit the configuration file `~/xtzkt-indexer-l1/appsettings.json`. What you basically need is to adjust the `TezosNode.Endpoint` and `ConnectionStrings.DefaultConnection`, if needed.

#### Run L1 indexer

````sh
cd ~/xtzkt-indexer-l1
dotnet Xtzkt.Indexers.L1.dll
````

That's it. If you want to run the indexer as a daemon, take a look at this guide: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0&tabs=linux-ubuntu#create-the-service-file.


### Build, configure and run API

Suppose, you have already cloned the repo to `~/xtzkt` during the steps above.

#### Build API

````sh
cd ~/xtzkt/Xtzkt.API/
dotnet publish -o ~/xtzkt-api
````

#### Configure API

Edit the configuration file `~/xtzkt-api/appsettings.json`. What you basically need is to adjust the `ConnectionStrings.DefaultConnection`, if needed.

#### Run API

````sh
cd ~/xtzkt-api
dotnet Xtzkt.API.dll
````

That's it. If you want to run the API as a daemon, take a look at this guide: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0&tabs=linux-ubuntu#create-the-service-file.

## Have a question?

Feel free to contact us via:
- Discord: https://discord.gg/aG8XKuwsQd
- Telegram: https://t.me/baking_bad_chat
- X: https://x.com/TezosBakingBad
- Email: hello@bakingbad.dev

Cheers! 🍺
