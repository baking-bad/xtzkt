using System.Text.Json;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.L1.Services;

namespace Xtzkt.Indexers.L1.Protocols.Initiator
{
    class Validator(ProtocolHandler protocol) : IValidator
    {
        readonly CacheService Cache = protocol.Cache;

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

            if (block.Required("header").RequiredInt32("level") != 1)
                throw new ValidationException("initiator block is allowed only at level 1", true);

            return Task.CompletedTask;
        }
    }
}
