-- Indexes and extensions the API needs. The indexers' migrations carry only what the indexers
-- themselves need: that keeps initial indexing fast, and lets every deployment index for its own
-- API queries. Applied at API startup, one statement at a time, in order.
--
-- Conventions:
--   * a blank line separates statements - not the semicolon;
--   * name indexes AX_*, so they never collide with the indexers' IX_*;
--   * use IF NOT EXISTS to avoid redundant work;
--   * build CONCURRENTLY to avoid blocking indexers;
--   * failed build can leave an INVALID index, which IF NOT EXISTS then skips forever - drop it manually.
--


-- /v1/search

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Addresses already have IX indexes.


-- Address profiles (aliases), read by AliasCache on startup.

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_Extras"
    ON "Addresses" USING gin ("Extras" jsonb_path_ops)
    WHERE "Extras" IS NOT NULL;


CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Tokens_Symbol_trgm"
    ON "Tokens" USING gin ("Symbol" gin_trgm_ops)
    WHERE "Symbol" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Tokens_Name_trgm"
    ON "Tokens" USING gin ("Name" gin_trgm_ops)
    WHERE "Name" IS NOT NULL;

-- Queries too short for a trigram are matched against the symbol only, by equality and prefix.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Tokens_Symbol_lower"
    ON "Tokens" (lower("Symbol") text_pattern_ops)
    WHERE "Symbol" IS NOT NULL;


CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Blocks_Hash"
    ON "Blocks" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Blocks_MichelsonHash"
    ON "Blocks" ("MichelsonHash")
    WHERE "MichelsonHash" IS NOT NULL;


CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DepositOps_Hash"
    ON "DepositOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_IncreasePaidStorageOps_Hash"
    ON "IncreasePaidStorageOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_OriginationOps_Hash"
    ON "OriginationOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_RegisterConstantOps_Hash"
    ON "RegisterConstantOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_RevealOps_Hash"
    ON "RevealOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransactionOps_Hash"
    ON "TransactionOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_TransferTicketOps_Hash"
    ON "TransferTicketOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DalPublishCommitmentOps_Hash"
    ON "DalPublishCommitmentOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DelegationOps_Hash"
    ON "DelegationOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SetDelegateParametersOps_Hash"
    ON "SetDelegateParametersOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SetDepositsLimitOps_Hash"
    ON "SetDepositsLimitOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupOriginateOps_Hash"
    ON "SmartRollupOriginateOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupAddMessagesOps_Hash"
    ON "SmartRollupAddMessagesOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupCementOps_Hash"
    ON "SmartRollupCementOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupExecuteOps_Hash"
    ON "SmartRollupExecuteOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupPublishOps_Hash"
    ON "SmartRollupPublishOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupRecoverBondOps_Hash"
    ON "SmartRollupRecoverBondOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_SmartRollupRefuteOps_Hash"
    ON "SmartRollupRefuteOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_StakingOps_Hash"
    ON "StakingOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_UpdateSecondaryKeyOps_Hash"
    ON "UpdateSecondaryKeyOps" ("Hash");


CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_ActivationOps_Hash"
    ON "ActivationOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DalEntrapmentEvidenceOps_Hash"
    ON "DalEntrapmentEvidenceOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_DrainDelegateOps_Hash"
    ON "DrainDelegateOps" ("Hash");

-- DoubleBakingOps and DoubleConsensusOps already have IX indexes.

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_NonceRevelationOps_Hash"
    ON "NonceRevelationOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_VdfRevelationOps_Hash"
    ON "VdfRevelationOps" ("Hash");


CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_BallotOps_Hash"
    ON "BallotOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_ProposalOps_Hash"
    ON "ProposalOps" ("Hash");


CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_AttestationOps_Hash"
    ON "AttestationOps" ("Hash");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_PreattestationOps_Hash"
    ON "PreattestationOps" ("Hash");

-- --
