-- Indexes and extensions the API needs. The indexers' migrations carry only what the indexers
-- themselves need: that keeps initial indexing fast, and lets every deployment index for its own
-- API queries. Applied at API startup, one statement at a time, in order.
--
-- Conventions:
--   * a blank line separates statements - not the semicolon;
--   * statements are grouped by table, one group per table;
--   * name indexes AX_*, so they never collide with the indexers' IX_*;
--   * use IF NOT EXISTS to avoid redundant work;
--   * build CONCURRENTLY to avoid blocking indexers;
--   * failed build can leave an INVALID index, which IF NOT EXISTS then skips forever - drop it manually.
--

-- extensions

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Addresses

-- needed for AddressCache
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_ChainId_LastLevel"
    ON "Addresses" ("ChainId", "LastLevel");

-- needed for AliasCache
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_Extras"
    ON "Addresses" USING gin ("Extras" jsonb_path_ops)
    WHERE "Extras" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_CodeHash"
    ON "Addresses" ("CodeHash")
    WHERE "CodeHash" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_TypeHash"
    ON "Addresses" ("TypeHash")
    WHERE "TypeHash" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_CreatorId"
    ON "Addresses" ("CreatorId")
    WHERE "CreatorId" IS NOT NULL;

-- BigMaps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BigMaps_Ptr"
    ON "BigMaps" ("Ptr");

-- BigMapKeys

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BigMapKeys_BigMapId_Id"
    ON "BigMapKeys" ("BigMapId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BigMapKeys_JsonKey"
    ON "BigMapKeys" USING gin ("JsonKey" jsonb_path_ops);

-- BigMapUpdates

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BigMapUpdates_TransactionId"
    ON "BigMapUpdates" ("TransactionId")
    WHERE "TransactionId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BigMapUpdates_OriginationId"
    ON "BigMapUpdates" ("OriginationId")
    WHERE "OriginationId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BigMapUpdates_MigrationId"
    ON "BigMapUpdates" ("MigrationId")
    WHERE "MigrationId" IS NOT NULL;

-- Blocks

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Blocks_Level"
    ON "Blocks" ("Level");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Blocks_ChainId_Timestamp"
    ON "Blocks" ("ChainId", "Timestamp");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Blocks_Hash"
    ON "Blocks" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Blocks_MichelsonHash"
    ON "Blocks" ("MichelsonHash")
    WHERE "MichelsonHash" IS NOT NULL;

-- DepositOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DepositOps_Hash"
    ON "DepositOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DepositOps_ReceiverId"
    ON "DepositOps" ("ReceiverId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DepositOps_ProxyId"
    ON "DepositOps" ("ProxyId")
    WHERE "ProxyId" IS NOT NULL;

-- Eip7702Delegations

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Eip7702Delegations_AuthorityId_Id"
    ON "Eip7702Delegations" ("AuthorityId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Eip7702Delegations_DelegateId_Id"
    ON "Eip7702Delegations" ("DelegateId", "Id");

-- IncreasePaidStorageOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_IncreasePaidStorageOps_Hash"
    ON "IncreasePaidStorageOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_IncreasePaidStorageOps_SenderId"
    ON "IncreasePaidStorageOps" ("SenderId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_IncreasePaidStorageOps_ContractId"
    ON "IncreasePaidStorageOps" ("ContractId");

-- Logs

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Logs_AddressId_Id"
    ON "Logs" ("AddressId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Logs_Topic0_Id"
    ON "Logs" (("Topics"[1]), "Id")
    WHERE ("Topics"[1]) IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Logs_TransactionId"
    ON "Logs" ("TransactionId")
    WHERE "TransactionId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Logs_OriginationId"
    ON "Logs" ("OriginationId")
    WHERE "OriginationId" IS NOT NULL;

-- OriginationOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_OriginationOps_Hash"
    ON "OriginationOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_OriginationOps_SenderId"
    ON "OriginationOps" ("SenderId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_OriginationOps_ContractId"
    ON "OriginationOps" ("ContractId")
    WHERE "ContractId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_OriginationOps_InitiatorId"
    ON "OriginationOps" ("InitiatorId")
    WHERE "InitiatorId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_OriginationOps_BakerId"
    ON "OriginationOps" ("BakerId")
    WHERE "BakerId" IS NOT NULL;

-- RegisterConstantOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_RegisterConstantOps_Hash"
    ON "RegisterConstantOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_RegisterConstantOps_SenderId"
    ON "RegisterConstantOps" ("SenderId");

-- RevealOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_RevealOps_Hash"
    ON "RevealOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_RevealOps_SenderId"
    ON "RevealOps" ("SenderId");

-- Storages

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Storages_TransactionId"
    ON "Storages" ("TransactionId")
    WHERE "TransactionId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Storages_OriginationId"
    ON "Storages" ("OriginationId")
    WHERE "OriginationId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Storages_MigrationId"
    ON "Storages" ("MigrationId")
    WHERE "MigrationId" IS NOT NULL;

-- Tokens

-- search by name
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Tokens_Name_trgm"
    ON "Tokens" USING gin ("Name" gin_trgm_ops)
    WHERE "Name" IS NOT NULL;

-- search by symbol
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Tokens_Symbol_trgm"
    ON "Tokens" USING gin ("Symbol" gin_trgm_ops)
    WHERE "Symbol" IS NOT NULL;

-- search by short symbol, lower("Symbol") LIKE '...%'
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Tokens_Symbol_lower"
    ON "Tokens" (lower("Symbol") text_pattern_ops)
    WHERE "Symbol" IS NOT NULL;

-- TransactionOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransactionOps_Hash"
    ON "TransactionOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransactionOps_SenderId_Id"
    ON "TransactionOps" ("SenderId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransactionOps_TargetId_Id"
    ON "TransactionOps" ("TargetId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransactionOps_InitiatorId_Id"
    ON "TransactionOps" ("InitiatorId", "Id")
    WHERE "InitiatorId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransactionOps_AliasId_Id"
    ON "TransactionOps" ("AliasId", "Id")
    WHERE "AliasId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransactionOps_GatewayId_Id"
    ON "TransactionOps" ("GatewayId", "Id")
    WHERE "GatewayId" IS NOT NULL;

-- TransferTicketOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransferTicketOps_Hash"
    ON "TransferTicketOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransferTicketOps_SenderId"
    ON "TransferTicketOps" ("SenderId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransferTicketOps_TargetId"
    ON "TransferTicketOps" ("TargetId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransferTicketOps_TicketerId"
    ON "TransferTicketOps" ("TicketerId");

-- --
