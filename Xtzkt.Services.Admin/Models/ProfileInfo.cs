namespace Xtzkt.Services.Admin.Models;

public class ProfileInfo
{
    public required string Type { get; set; }
    public required string Id { get; set; }
    public required string? Name { get; set; }
    public DateTime? UpdatedAt { get; set; }

    #region address
    public string? Description { get; set; }
    public string? Logo { get; set; }
    public string? LogoDark { get; set; }

    public string? Website { get; set; }
    public string? Support { get; set; }
    public string? Email { get; set; }
    public string? Telegram { get; set; }
    public string? Discord { get; set; }
    public string? Reddit { get; set; }
    public string? Slack { get; set; }
    public string? Github { get; set; }
    public string? Gitlab { get; set; }
    public string? Mailchain { get; set; }
    public string? Instagram { get; set; }
    public string? Facebook { get; set; }
    public string? X { get; set; }
    #endregion

    #region protocol
    public string? Docs { get; set; }
    #endregion

    #region software
    public DateTime? CommitDate { get; set; }
    public string? CommitHash { get; set; }
    #endregion
}
