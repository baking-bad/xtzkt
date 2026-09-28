namespace Xtzkt.Api.Models;

public class AccountProfile
{
    /// <summary>Name of the account.</summary>
    public required string Name { get; init; }

    /// <summary>Short description of the account.</summary>
    public string? Description { get; init; }

    /// <summary>Link to the logo.</summary>
    public string? Logo { get; init; }

    /// <summary>Link to the logo for dark themes.</summary>
    public string? LogoDark { get; init; }

    /// <summary>Link to the website.</summary>
    public string? Website { get; init; }

    /// <summary>Link to the support.</summary>
    public string? Support { get; init; }

    /// <summary>Contact email.</summary>
    public string? Email { get; init; }

    /// <summary>Telegram account.</summary>
    public string? Telegram { get; init; }

    /// <summary>Discord server.</summary>
    public string? Discord { get; init; }

    /// <summary>Reddit account.</summary>
    public string? Reddit { get; init; }

    /// <summary>Slack workspace.</summary>
    public string? Slack { get; init; }

    /// <summary>GitHub account.</summary>
    public string? Github { get; init; }

    /// <summary>GitLab account.</summary>
    public string? Gitlab { get; init; }

    /// <summary>Mailchain address.</summary>
    public string? Mailchain { get; init; }

    /// <summary>Instagram account.</summary>
    public string? Instagram { get; init; }

    /// <summary>Facebook account.</summary>
    public string? Facebook { get; init; }

    /// <summary>X (Twitter) account.</summary>
    public string? X { get; init; }
}
