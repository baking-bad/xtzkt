using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Utils;

namespace Xtzkt.Api.Repositories;

public class AccountRepository(AddressCache _addressCache, AddressRepository _addressRepo)
{
    public async Task<Account?> Get(string hash)
    {
        var _addresses = await _addressCache.GetAsync(hash);
        if (_addresses.Count == 0)
            return null;

        var _ids = _addresses.Select(x => x.Id).ToHashSet();

        for (int i = 0; i < _addresses.Count; i++)
        {
            #region resolve owners
            var ownerId = _addresses[i] switch
            {
                Data.Models.XMichelsonAlias alias => alias.OwnerId,
                Data.Models.XEvmAlias alias => alias.OwnerId,
                _ => (int?)null
            };

            if (ownerId is int _ownerId && _ids.Add(_ownerId) &&
                await _addressCache.GetAsync(_ownerId) is Data.Models.Address owner)
            {
                _addresses.Add(owner);
                foreach (var ownerAlt in await _addressCache.GetAsync(owner.Hash))
                    if (_ids.Add(ownerAlt.Id))
                        _addresses.Add(ownerAlt);
            }
            #endregion

            #region resolve aliases
            if (_addresses[i] is Data.Models.XAddress xAddress && xAddress.AliasesCount != 0)
            {
                // UPDATE: add other runtimes, when implemented
                var aliasHash = xAddress is Data.Models.XEvmAddress
                    ? Runtimes.GetMichelsonAlias(xAddress.Hash)
                    : Runtimes.GetEvmAlias(xAddress.Hash);

                // an alias always lives on the same chain as its owner
                var alias = await _addressCache.GetAsync(xAddress.ChainId, aliasHash);
                if (alias != null && _ids.Add(alias.Id))
                    _addresses.Add(alias);
            }
            #endregion
        }

        var addresses = _addresses
            .OrderBy(x => x is Data.Models.XMichelsonAlias or Data.Models.XEvmAlias ? 1 : 0)
            .ThenBy(x => x.Id)
            .Select(_addressRepo.Get)
            .ToList();

        return new Account
        {
            Hash = addresses[0].Hash,
            Addresses = addresses
        };
    }
}
