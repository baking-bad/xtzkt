using System.Text.Json;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.L1.Services;

namespace Xtzkt.Indexers.L1.Protocols.Genesis
{
    class Validator(ProtocolHandler protocol) : IValidator
    {
        readonly CacheService Cache = protocol.Cache;

        public Task ValidateBlock(JsonElement block)
        {
            var chain = Cache.Chain.Get();

            if (block.RequiredString("chain_id") != chain.ChainId)
                throw new ValidationException("invalid chain");

            if (block.Required("header").RequiredInt32("level") != 0)
                throw new ValidationException($"genesis block is expected at level {0}", true);

            return Task.CompletedTask;
        }
    }
}
