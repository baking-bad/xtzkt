-- An example of a workable init script: the access paths behind the main scenarios of every endpoint.
-- Use it as a starting point - the default init.pgsql carries only what the API needs to function,
-- because indexing for every filter and sort the API exposes takes hours of CONCURRENTLY builds on
-- a synced database and roughly doubles the size. Copy this file, adjust it to what your instance actually serves,
-- and point Db.InitScript at the result. Everything in init.pgsql is repeated here, so the copy is self-contained.
--
-- Conventions:
--   * a blank line separates statements - not the semicolon;
--   * the whole script runs on a single connection, so a SET at the top applies to all of it;
--   * statements are grouped by table, one group per table;
--   * name indexes AX_*, so they never collide with the indexers' IX_*;
--   * use IF NOT EXISTS to avoid redundant work;
--   * build CONCURRENTLY to avoid blocking indexers;
--   * failed build can leave an INVALID index, which IF NOT EXISTS then skips forever - drop it manually.
--

-- the api caps statement_timeout, index builds must not be
SET statement_timeout = 0;

-- extensions

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- ActivationOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_ActivationOps_Hash"
    ON "ActivationOps" ("Hash");

-- Addresses

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_Balance_Id"
    ON "Addresses" (COALESCE("Balance", "Balance18"), "Id");

-- AddressCache refreshes the addresses touched since the last notification, on every block
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_ChainId_LastLevel"
    ON "Addresses" ("ChainId", "LastLevel");

-- AliasCache loads every profile alias at startup
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_Extras"
    ON "Addresses" USING gin ("Extras" jsonb_path_ops)
    WHERE "Extras" IS NOT NULL;

-- AttestationOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_AttestationOps_Hash"
    ON "AttestationOps" ("Hash");

-- BallotOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BallotOps_Hash"
    ON "BallotOps" ("Hash");

-- BigMapKeys

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BigMapKeys_BigMapId_Id"
    ON "BigMapKeys" ("BigMapId", "Id");

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

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Blocks_Hash"
    ON "Blocks" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Blocks_MichelsonHash"
    ON "Blocks" ("MichelsonHash")
    WHERE "MichelsonHash" IS NOT NULL;

-- BlockCache resolves a timestamp filter or cursor into a block's id window
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Blocks_Timestamp_Id"
    ON "Blocks" ("Timestamp", "Id");

-- BridgeTicketBalances

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BridgeTicketBalances_TicketId_Id"
    ON "BridgeTicketBalances" ("TicketId", "Id");

-- BridgeTicketTransfers

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BridgeTicketTransfers_FromId_Id"
    ON "BridgeTicketTransfers" ("FromId", "Id")
    WHERE "FromId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BridgeTicketTransfers_ToId_Id"
    ON "BridgeTicketTransfers" ("ToId", "Id")
    WHERE "ToId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BridgeTicketTransfers_TicketId_Id"
    ON "BridgeTicketTransfers" ("TicketId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BridgeTicketTransfers_TransactionId"
    ON "BridgeTicketTransfers" ("TransactionId")
    WHERE "TransactionId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BridgeTicketTransfers_DepositId"
    ON "BridgeTicketTransfers" ("DepositId")
    WHERE "DepositId" IS NOT NULL;

-- DalEntrapmentEvidenceOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DalEntrapmentEvidenceOps_Hash"
    ON "DalEntrapmentEvidenceOps" ("Hash");

-- DalPublishCommitmentOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DalPublishCommitmentOps_Hash"
    ON "DalPublishCommitmentOps" ("Hash");

-- DelegationOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DelegationOps_Hash"
    ON "DelegationOps" ("Hash");

-- DepositOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DepositOps_Hash"
    ON "DepositOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DepositOps_ReceiverId_Id"
    ON "DepositOps" ("ReceiverId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DepositOps_ProxyId_Id"
    ON "DepositOps" ("ProxyId", "Id")
    WHERE "ProxyId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DepositOps_ClaimTransactionId"
    ON "DepositOps" ("ClaimTransactionId")
    WHERE "ClaimTransactionId" IS NOT NULL;

-- DrainDelegateOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DrainDelegateOps_Hash"
    ON "DrainDelegateOps" ("Hash");

-- Eip7702Delegations

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Eip7702Delegations_SenderId_Id"
    ON "Eip7702Delegations" ("SenderId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Eip7702Delegations_AuthorityId_Id"
    ON "Eip7702Delegations" ("AuthorityId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Eip7702Delegations_DelegateId_Id"
    ON "Eip7702Delegations" ("DelegateId", "Id");

-- IncreasePaidStorageOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_IncreasePaidStorageOps_Hash"
    ON "IncreasePaidStorageOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_IncreasePaidStorageOps_SenderId_Id"
    ON "IncreasePaidStorageOps" ("SenderId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_IncreasePaidStorageOps_ContractId_Id"
    ON "IncreasePaidStorageOps" ("ContractId", "Id");

-- Logs

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Logs_AddressId_Id"
    ON "Logs" ("AddressId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Logs_Topic0_AddressId_Id"
    ON "Logs" ("Topic0", "AddressId", "Id")
    WHERE "Topic0" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Logs_TransactionId"
    ON "Logs" ("TransactionId")
    WHERE "TransactionId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Logs_OriginationId"
    ON "Logs" ("OriginationId")
    WHERE "OriginationId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Logs_DepositId"
    ON "Logs" ("DepositId")
    WHERE "DepositId" IS NOT NULL;

-- NonceRevelationOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_NonceRevelationOps_Hash"
    ON "NonceRevelationOps" ("Hash");

-- OriginationOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_OriginationOps_Hash"
    ON "OriginationOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_OriginationOps_SenderId_Id"
    ON "OriginationOps" ("SenderId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_OriginationOps_InitiatorId_Id"
    ON "OriginationOps" ("InitiatorId", "Id")
    WHERE "InitiatorId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_OriginationOps_ContractId"
    ON "OriginationOps" ("ContractId")
    WHERE "ContractId" IS NOT NULL;

-- PreattestationOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_PreattestationOps_Hash"
    ON "PreattestationOps" ("Hash");

-- ProposalOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_ProposalOps_Hash"
    ON "ProposalOps" ("Hash");

-- RegisterConstantOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_RegisterConstantOps_Hash"
    ON "RegisterConstantOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_RegisterConstantOps_SenderId_Id"
    ON "RegisterConstantOps" ("SenderId", "Id");

-- RevealOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_RevealOps_Hash"
    ON "RevealOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_RevealOps_SenderId_Id"
    ON "RevealOps" ("SenderId", "Id");

-- SetDelegateParametersOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SetDelegateParametersOps_Hash"
    ON "SetDelegateParametersOps" ("Hash");

-- SetDepositsLimitOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SetDepositsLimitOps_Hash"
    ON "SetDepositsLimitOps" ("Hash");

-- SmartRollupAddMessagesOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupAddMessagesOps_Hash"
    ON "SmartRollupAddMessagesOps" ("Hash");

-- SmartRollupCementOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupCementOps_Hash"
    ON "SmartRollupCementOps" ("Hash");

-- SmartRollupExecuteOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupExecuteOps_Hash"
    ON "SmartRollupExecuteOps" ("Hash");

-- SmartRollupOriginateOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupOriginateOps_Hash"
    ON "SmartRollupOriginateOps" ("Hash");

-- SmartRollupPublishOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupPublishOps_Hash"
    ON "SmartRollupPublishOps" ("Hash");

-- SmartRollupRecoverBondOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupRecoverBondOps_Hash"
    ON "SmartRollupRecoverBondOps" ("Hash");

-- SmartRollupRefuteOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupRefuteOps_Hash"
    ON "SmartRollupRefuteOps" ("Hash");

-- StakingOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_StakingOps_Hash"
    ON "StakingOps" ("Hash");

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

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Storages_SubsidyId"
    ON "Storages" ("SubsidyId")
    WHERE "SubsidyId" IS NOT NULL;

-- TicketBalances

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TicketBalances_TicketId_Id"
    ON "TicketBalances" ("TicketId", "Id");

-- TicketTransfers

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TicketTransfers_FromId_Id"
    ON "TicketTransfers" ("FromId", "Id")
    WHERE "FromId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TicketTransfers_ToId_Id"
    ON "TicketTransfers" ("ToId", "Id")
    WHERE "ToId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TicketTransfers_TicketId_Id"
    ON "TicketTransfers" ("TicketId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TicketTransfers_TransactionId"
    ON "TicketTransfers" ("TransactionId")
    WHERE "TransactionId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TicketTransfers_TransferTicketId"
    ON "TicketTransfers" ("TransferTicketId")
    WHERE "TransferTicketId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TicketTransfers_SmartRollupExecuteId"
    ON "TicketTransfers" ("SmartRollupExecuteId")
    WHERE "SmartRollupExecuteId" IS NOT NULL;

-- Tickets

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Tickets_TicketerId_Id"
    ON "Tickets" ("TicketerId", "Id");

-- TokenBalances

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TokenBalances_TokenId_Balance_Id"
    ON "TokenBalances" ("TokenId", "Balance", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TokenBalances_AddressId_Id"
    ON "TokenBalances" ("AddressId", "Id");

-- TokenTransfers

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TokenTransfers_FromId_Id"
    ON "TokenTransfers" ("FromId", "Id")
    WHERE "FromId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TokenTransfers_ToId_Id"
    ON "TokenTransfers" ("ToId", "Id")
    WHERE "ToId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TokenTransfers_TokenId_Id"
    ON "TokenTransfers" ("TokenId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TokenTransfers_ContractId_Id"
    ON "TokenTransfers" ("ContractId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TokenTransfers_TransactionId"
    ON "TokenTransfers" ("TransactionId")
    WHERE "TransactionId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TokenTransfers_OriginationId"
    ON "TokenTransfers" ("OriginationId")
    WHERE "OriginationId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TokenTransfers_MigrationId"
    ON "TokenTransfers" ("MigrationId")
    WHERE "MigrationId" IS NOT NULL;

-- Tokens

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Tokens_TransfersCount_Id"
    ON "Tokens" ("TransfersCount", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Tokens_HoldersCount_Id"
    ON "Tokens" ("HoldersCount", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Tokens_ContractId_Id"
    ON "Tokens" ("ContractId", "Id");

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

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransactionOps_ClaimDepositId"
    ON "TransactionOps" ("ClaimDepositId")
    WHERE "ClaimDepositId" IS NOT NULL;

-- TransferTicketOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransferTicketOps_Hash"
    ON "TransferTicketOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransferTicketOps_SenderId_Id"
    ON "TransferTicketOps" ("SenderId", "Id");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransferTicketOps_TargetId_Id"
    ON "TransferTicketOps" ("TargetId", "Id");

-- UpdateSecondaryKeyOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_UpdateSecondaryKeyOps_Hash"
    ON "UpdateSecondaryKeyOps" ("Hash");

-- VdfRevelationOps

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_VdfRevelationOps_Hash"
    ON "VdfRevelationOps" ("Hash");
