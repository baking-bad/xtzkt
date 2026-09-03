using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Models.Operations;

namespace Xtzkt.Api.Models.Abstract
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "activity")]
    [JsonDerivedType(typeof(L1TransactionOperation), $"{ActivityTypes.Transaction}:{Directions.L1}")]
    [JsonDerivedType(typeof(XEvmTransactionOperation), $"{ActivityTypes.Transaction}:{Directions.XEvm}")]
    [JsonDerivedType(typeof(XMichelsonTransactionOperation), $"{ActivityTypes.Transaction}:{Directions.XMichelson}")]
    [JsonDerivedType(typeof(XEvmMichelsonTransactionOperation), $"{ActivityTypes.Transaction}:{Directions.XEvmMichelson}")]
    [JsonDerivedType(typeof(XMichelsonEvmTransactionOperation), $"{ActivityTypes.Transaction}:{Directions.XMichelsonEvm}")]
    [JsonDerivedType(typeof(L1RevealOperation), $"{ActivityTypes.Reveal}:{Layers.L1}")]
    [JsonDerivedType(typeof(XRevealOperation), $"{ActivityTypes.Reveal}:{Layers.TezosX}")]
    [JsonDerivedType(typeof(L1IncreasePaidStorageOperation), $"{ActivityTypes.IncreasePaidStorage}:{Layers.L1}")]
    [JsonDerivedType(typeof(XIncreasePaidStorageOperation), $"{ActivityTypes.IncreasePaidStorage}:{Layers.TezosX}")]
    [JsonDerivedType(typeof(L1TransferTicketOperation), $"{ActivityTypes.TransferTicket}:{Layers.L1}")]
    [JsonDerivedType(typeof(XTransferTicketOperation), $"{ActivityTypes.TransferTicket}:{Layers.TezosX}")]
    [JsonDerivedType(typeof(L1RegisterConstantOperation), $"{ActivityTypes.RegisterConstant}:{Layers.L1}")]
    [JsonDerivedType(typeof(XRegisterConstantOperation), $"{ActivityTypes.RegisterConstant}:{Layers.TezosX}")]
    [JsonDerivedType(typeof(XMichelsonDepositOperation), $"{ActivityTypes.Deposit}:{Runtimes.Michelson}")]
    [JsonDerivedType(typeof(XEvmDepositOperation), $"{ActivityTypes.Deposit}:{Runtimes.Evm}")]
    [JsonDerivedType(typeof(MichelsonMigrationOperation), $"{ActivityTypes.Migration}:{Runtimes.Michelson}")]
    [JsonDerivedType(typeof(EvmMigrationOperation), $"{ActivityTypes.Migration}:{Runtimes.Evm}")]
    [JsonDerivedType(typeof(L1OriginationOperation), $"{ActivityTypes.Origination}:{Envs.L1}")]
    [JsonDerivedType(typeof(XEvmOriginationOperation), $"{ActivityTypes.Origination}:{Envs.XEvm}")]
    [JsonDerivedType(typeof(XMichelsonOriginationOperation), $"{ActivityTypes.Origination}:{Envs.XMichelson}")]
    [JsonDerivedType(typeof(TokenTransfer), ActivityTypes.TokenTransfer)]
    [JsonDerivedType(typeof(TicketTransfer), ActivityTypes.TicketTransfer)]
    [JsonDerivedType(typeof(BridgeTicketTransfer), ActivityTypes.BridgeTicketTransfer)]
    public interface IActivity
    {
        public ChainInfo Chain { get; }

        public long Id { get; }
        
        public int Level { get; }

        public DateTime Timestamp { get; }
    }

    [Flags]
    public enum ActivityRole
    {
        None = 0,
        Sender = 1,
        Target = 2,
        Initiator = 4,
        Mention = 8,
        All = Sender | Target | Initiator | Mention
    }
}
