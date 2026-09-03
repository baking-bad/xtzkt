using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols
{
    public class ManagerContext(ProtocolHandler proto)
    {
        readonly ProtocolHandler Proto = proto;
        readonly List<IL1ManagerOperation> Ops = new(4);
        L1Address? Address = null;
        long Change = 0;

        public void Init(JsonElement operation)
        {
            Proto.Cache.Chain.IncreaseManagerCounter(operation.RequiredArray("contents").Count());
            Address = null;
            Change = 0;
            Ops.Clear();
        }

        public void Add(IL1ManagerOperation op)
        {
            Ops.Add(op);
        }

        public void Set(L1Address address)
        {
            Address = address;
        }

        public void Credit(long amount)
        {
            Change += amount;
        }

        public void Burn(long amount)
        {
            Change -= amount;
        }

        public void Apply()
        {
            if (Address?.Type == AddressType.L1User && (Address.Balance == 0 || Address.Balance - Change == 0))
            {
                (Address as L1User)!.Counter = Proto.Cache.Chain.GetManagerCounter();
                (Address as L1User)!.Revealed = false;
            }

            NormalizeFees();
        }

        void NormalizeFees()
        {
            if (Ops.Count < 2)
                return;

            var totalFee = 0L;
            var totalGasLimit = 0L;
            foreach (var op in Ops)
            {
                totalFee += op.BakerFee!.Value;
                totalGasLimit += op.GasLimit!.Value;
            }

            if (totalGasLimit == 0)
                return;

            var sumFee = 0L;
            foreach (var op in Ops)
            {
                op.BakerFee = totalFee * op.GasLimit!.Value / totalGasLimit;
                sumFee += op.BakerFee.Value;
            }

            Ops[0].BakerFee += totalFee - sumFee;
        }
    }
}
