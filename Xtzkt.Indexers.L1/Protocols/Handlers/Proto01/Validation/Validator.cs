using System.Text.Json;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.L1.Services;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class Validator(ProtocolHandler protocol) : IValidator
    {
        protected readonly CacheService Cache = protocol.Cache;

        public Task ValidateBlock(JsonElement block)
        {
            if (block.RequiredString("chain_id") != Cache.Chain.GetChainId())
                throw new ValidationException("invalid chain");

            if (block.Required("header").RequiredInt32("level") != Cache.Chain.GetNextLevel())
                throw new ValidationException("invalid block level", true);

            if (block.Required("header").RequiredString("predecessor") != Cache.Chain.GetHead())
                throw new ValidationException("invalid block predecessor", true);

            if (block.RequiredString("protocol") != Cache.Chain.GetNextProtocol())
                throw new ValidationException("invalid block protocol", true);

            return Task.CompletedTask;
        }
    }
}
