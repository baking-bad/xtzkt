using System.Text.Json;
using System.Text.Json.Serialization;
using Xtzkt.Data.Models;
using Xtzkt.Services.Admin.Exceptions;
using Xtzkt.Services.Admin.Models;
using Xtzkt.Services.Admin.Services;
using Xtzkt.Services.Admin.Utils;
using Xtzkt.Utils;

namespace Xtzkt.Services.Admin.Endpoints;

static class ProfileEndpoints
{
    #region static
    const int DefaultLimit = 100;
    const int MaxLimit = 10_000;
    const int MaxNameLength = 256;
    const int MaxDescriptionLength = 1024;

    static readonly Dictionary<string, ProfileType> Types = Enum.GetValues<ProfileType>()
        .ToDictionary(x => x.ToString(), StringComparer.OrdinalIgnoreCase);

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonDateTimeConverter() }
    };
    #endregion

    public static async Task<IResult> GetStateAsync(StoreService store, CancellationToken ct)
    {
        var (count, lastUpdate) = await store.GetStateAsync(ct);
        return Results.Ok(new { count, lastUpdate });
    }

    public static async Task<IResult> GetAsync(StoreService store, CancellationToken ct, string? type = null, int offset = 0, int limit = DefaultLimit)
    {
        ProfileType? filter = null;
        if (type != null)
        {
            if (!Types.TryGetValue(type, out var value))
                return Results.BadRequest($"unknown type {type}");

            filter = value;
        }

        if (offset < 0)
            return Results.BadRequest("offset must be non-negative");

        if (limit < 1 || limit > MaxLimit)
            return Results.BadRequest($"limit must be between 1 and {MaxLimit}");

        var profiles = await store.GetProfilesAsync(filter, offset, limit, ct);
        return Results.Json(profiles.ConvertAll(ToInfo), JsonOptions);
    }

    public static async Task<IResult> UpdateAsync(List<ProfileInfo?> profiles, StoreService store, CancellationToken ct)
    {
        var indexes = new Dictionary<string, int>(profiles.Count);
        var upserts = new List<Profile>(profiles.Count);
        var removes = new List<string>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < profiles.Count; i++)
        {
            if (profiles[i] is not ProfileInfo profile)
                return Results.BadRequest($"[{i}]: profile is null");

            if (string.IsNullOrWhiteSpace(profile.Id))
                return Results.BadRequest($"[{i}]: id is required");

            if (profile.Type == null || !Types.TryGetValue(profile.Type, out var type))
                return Results.BadRequest($"[{i}]: unknown type {profile.Type}");

            if (!IsValidId(type, profile.Id))
                return Results.BadRequest($"[{i}]: id doesn't match the {type.ToString().ToLowerInvariant()} format");

            if (profile.Name != null && string.IsNullOrWhiteSpace(profile.Name))
                return Results.BadRequest($"[{i}]: name must be either null or non-empty");

            if (profile.Name?.Length > MaxNameLength)
                return Results.BadRequest($"[{i}]: name must not be longer than {MaxNameLength} characters");

            if (profile.Description?.Length > MaxDescriptionLength)
                return Results.BadRequest($"[{i}]: description must not be longer than {MaxDescriptionLength} characters");

            var id = profile.Id.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? profile.Id.ToLowerInvariant()
                : profile.Id;

            if (!indexes.TryAdd(id, i))
                return Results.BadRequest($"[{i}]: duplicate id {id}");

            if (profile.Name == null)
                removes.Add(id);
            else
                upserts.Add(ToModel(profile, type, id, profile.Name, profile.UpdatedAt ?? now));
        }

        try
        {
            await store.UpdateProfilesAsync(upserts, removes, ct);
            return Results.NoContent();
        }
        catch (ProfileTypeChangeException ex)
        {
            return Results.BadRequest($"[{indexes[ex.Id]}]: type of {ex.Id} can't be changed");
        }
    }

    static bool IsValidId(ProfileType type, string id) => !id.EndsWith('\n') && type switch
    {
        ProfileType.Address => Regexes.MichelsonAddress().IsMatch(id) || Regexes.EvmAddress().IsMatch(id),
        ProfileType.Protocol => Regexes.MichelsonProtocolHash().IsMatch(id) || Regexes.KernelHash().IsMatch(id),
        ProfileType.Software => Regexes.SoftwareHash().IsMatch(id),
        _ => throw new NotSupportedException($"Unsupported profile type {type} or invalid id"),
    };

    static Profile ToModel(ProfileInfo profile, ProfileType type, string id, string name, DateTime updatedAt) => type switch
    {
        ProfileType.Address => new AddressProfile
        {
            Id = id,
            Name = name,
            UpdatedAt = updatedAt,
            Description = profile.Description,
            Logo = profile.Logo,
            LogoDark = profile.LogoDark,
            Website = profile.Website,
            Support = profile.Support,
            Email = profile.Email,
            Telegram = profile.Telegram,
            Discord = profile.Discord,
            Reddit = profile.Reddit,
            Slack = profile.Slack,
            Github = profile.Github,
            Gitlab = profile.Gitlab,
            Mailchain = profile.Mailchain,
            Instagram = profile.Instagram,
            Facebook = profile.Facebook,
            X = profile.X
        },
        ProfileType.Protocol => new ProtocolProfile
        {
            Id = id,
            Name = name,
            UpdatedAt = updatedAt,
            Docs = profile.Docs
        },
        ProfileType.Software => new SoftwareProfile
        {
            Id = id,
            Name = name,
            UpdatedAt = updatedAt,
            CommitDate = profile.CommitDate,
            CommitHash = profile.CommitHash
        },
        _ => throw new NotSupportedException($"Unsupported profile type {type}")
    };

    static ProfileInfo ToInfo(Profile profile)
    {
        var type = profile.Type.ToString().ToLowerInvariant();
        return profile switch
        {
            AddressProfile address => new ProfileInfo
            {
                Type = type,
                Id = address.Id,
                Name = address.Name,
                UpdatedAt = address.UpdatedAt,
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
                X = address.X
            },
            ProtocolProfile protocol => new ProfileInfo
            {
                Type = type,
                Id = protocol.Id,
                Name = protocol.Name,
                UpdatedAt = protocol.UpdatedAt,
                Docs = protocol.Docs
            },
            SoftwareProfile software => new ProfileInfo
            {
                Type = type,
                Id = software.Id,
                Name = software.Name,
                UpdatedAt = software.UpdatedAt,
                CommitDate = software.CommitDate,
                CommitHash = software.CommitHash
            },
            _ => throw new NotSupportedException($"Unsupported profile type {profile.GetType().Name}")
        };
    }
}
