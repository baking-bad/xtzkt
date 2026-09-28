using Microsoft.EntityFrameworkCore;
using Xtzkt.Api.Models;
using Xtzkt.Api.Utils;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Services.Cache;

public class ProfileCache
{
    readonly IDbContextFactory<XtzktContext> DbFactory;
    readonly ILogger Logger;
    readonly Lock Crit = new();
    readonly SemaphoreSlim Sync = new(1, 1);

    Dictionary<string, (FuzzyString Name, AccountProfile Profile)> AddressProfiles;
    Dictionary<string, string> ProtocolProfiles;
    Dictionary<string, string> SoftwareProfiles;
    KeyValuePair<string, (FuzzyString Name, AccountProfile Profile)>[]? CachedForSearch;

    public ProfileCache(IDbContextFactory<XtzktContext> dbFactory, ILogger<ProfileCache> logger)
    {
        DbFactory = dbFactory;
        Logger = logger;

        Logger.LogDebug("Initializing profile cache...");

        using var db = DbFactory.CreateDbContext();
        (AddressProfiles, ProtocolProfiles, SoftwareProfiles) = Build(db.Profiles);

        Logger.LogInformation("Profile cache initialized with {cnt} items", AddressProfiles.Count + ProtocolProfiles.Count + SoftwareProfiles.Count);
    }

    /// <summary>
    /// Returns the name from the address's profile, if it has one.
    /// </summary>
    public string? GetAddressProfile(string hash)
    {
        lock (Crit)
        {
            return AddressProfiles.TryGetValue(hash, out var profile) ? profile.Name.Original : null;
        }
    }

    /// <summary>
    /// Returns the whole profile by address, if there is some.
    /// </summary>
    public AccountProfile? GetAccountProfile(string hash)
    {
        lock (Crit)
        {
            return AddressProfiles.TryGetValue(hash, out var profile) ? profile.Profile : null;
        }
    }

    public string? GetProtocolProfile(string hash)
    {
        lock (Crit)
        {
            return ProtocolProfiles.GetValueOrDefault(hash);
        }
    }

    public string? GetSoftwareProfile(string hash)
    {
        lock (Crit)
        {
            return SoftwareProfiles.GetValueOrDefault(hash);
        }
    }

    /// <summary>
    /// Returns all the address hashes whose profile names match the query, the best matches first.
    /// </summary>
    public (string Hash, string Name, double Score)[] SearchAddressProfiles(string query)
    {
        KeyValuePair<string, (FuzzyString Name, AccountProfile Profile)>[] profiles;
        lock (Crit)
        {
            profiles = CachedForSearch ??= [.. AddressProfiles];
        }

        var matcher = new FuzzyMatcher(query);
        var matches = new List<(string Hash, FuzzyString Name, double Score)>();

        foreach (var (hash, profile) in profiles)
        {
            var score = matcher.Score(profile.Name);
            if (score > 0)
                matches.Add((hash, profile.Name, score));
        }

        matches.Sort((x, y) =>
        {
            var res = y.Score.CompareTo(x.Score);
            if (res != 0) return res;

            res = x.Name.Original.Length.CompareTo(y.Name.Original.Length);
            if (res != 0) return res;

            return string.CompareOrdinal(x.Hash, y.Hash);
        });

        return [.. matches.Select(x => (x.Hash, x.Name.Original, x.Score))];
    }

    /// <summary>
    /// Reloads all the profiles.
    /// </summary>
    public async Task ReloadAsync()
    {
        await Sync.WaitAsync();
        try
        {
            using var db = DbFactory.CreateDbContext();
            var (addressProfiles, protocolProfiles, softwareProfiles) = Build(await db.Profiles.ToListAsync());

            lock (Crit)
            {
                AddressProfiles = addressProfiles;
                ProtocolProfiles = protocolProfiles;
                SoftwareProfiles = softwareProfiles;
                CachedForSearch = null;
            }

            Logger.LogDebug("Profile cache reloaded with {cnt} items", addressProfiles.Count + protocolProfiles.Count + softwareProfiles.Count);
        }
        finally
        {
            Sync.Release();
        }
    }

    /// <summary>
    /// Reloads the changed profiles, dropping the ones that no longer exist.
    /// </summary>
    public async Task UpdateAsync((ProfileType Type, string Id)[] keys)
    {
        await Sync.WaitAsync();
        try
        {
            var ids = keys.Select(x => x.Id).Distinct().ToArray();

            using var db = DbFactory.CreateDbContext();
            var profiles = await db.Profiles
                .Where(x => ids.Contains(x.Id))
                .ToListAsync();

            lock (Crit)
            {
                foreach (var (type, id) in keys)
                    Remove(type, id);

                foreach (var profile in profiles)
                    Add(AddressProfiles, ProtocolProfiles, SoftwareProfiles, profile);

                CachedForSearch = null;
            }

            Logger.LogDebug("Profile cache updated with {cnt} changed items", keys.Length);
        }
        finally
        {
            Sync.Release();
        }
    }

    void Remove(ProfileType type, string id)
    {
        switch (type)
        {
            case ProfileType.Address:
                AddressProfiles.Remove(id);
                break;
            case ProfileType.Protocol:
                ProtocolProfiles.Remove(id);
                break;
            case ProfileType.Software:
                SoftwareProfiles.Remove(id);
                break;
            default:
                throw new NotSupportedException($"Unsupported profile type {type}");
        }
    }

    static (Dictionary<string, (FuzzyString Name, AccountProfile Profile)>, Dictionary<string, string>, Dictionary<string, string>) Build(IEnumerable<Profile> profiles)
    {
        var addressProfiles = new Dictionary<string, (FuzzyString Name, AccountProfile Profile)>();
        var protocolProfiles = new Dictionary<string, string>();
        var softwareProfiles = new Dictionary<string, string>();

        foreach (var profile in profiles)
            Add(addressProfiles, protocolProfiles, softwareProfiles, profile);

        return (addressProfiles, protocolProfiles, softwareProfiles);
    }

    static void Add(
        Dictionary<string, (FuzzyString Name, AccountProfile Profile)> addressProfiles,
        Dictionary<string, string> protocolProfiles,
        Dictionary<string, string> softwareProfiles,
        Profile profile)
    {
        switch (profile)
        {
            case AddressProfile address:
                addressProfiles[address.Id] = (new FuzzyString(address.Name), new AccountProfile
                {
                    Name = address.Name,
                    Description = address.Description,
                    Logo = address.Logo,
                    LogoDark = address.LogoDark,
                    Website = address.Website,
                    Support = address.Support,
                    Email = address.Email,
                    Telegram = address.Telegram,
                    Discord = address.Discord,
                    Reddit = address.Reddit,
                    Slack = address.Slack,
                    Github = address.Github,
                    Gitlab = address.Gitlab,
                    Mailchain = address.Mailchain,
                    Instagram = address.Instagram,
                    Facebook = address.Facebook,
                    X = address.X,
                });
                break;
            case ProtocolProfile protocol:
                protocolProfiles[protocol.Id] = protocol.Name;
                break;
            case SoftwareProfile software:
                softwareProfiles[software.Id] = software.Name;
                break;
            default:
                throw new NotSupportedException($"Unsupported profile type {profile.Type}");
        }
    }
}
