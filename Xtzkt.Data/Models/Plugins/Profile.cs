using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Xtzkt.Data.Models;

public abstract class Profile(ProfileType type)
{
    public ProfileType Type { get; private set; } = type;

    public required string Id { get; set; }
    public required string Name { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public enum ProfileType
{
    Address,
    Protocol,
    Software,
}

public class AddressProfile() : Profile(ProfileType.Address)
{
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
}

public class ProtocolProfile() : Profile(ProfileType.Protocol)
{
    public string? Docs { get; set; }
}

public class SoftwareProfile() : Profile(ProfileType.Software)
{
    public DateTime? CommitDate { get; set; }
    public string? CommitHash { get; set; }
}

public static class ProfileModel
{
    public static void BuildProfileModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<Profile>()
            .HasKey(x => x.Id);
        #endregion

        #region inheritance
        modelBuilder.Entity<Profile>()
            .HasDiscriminator<ProfileType>(nameof(Profile.Type))
            .HasValue<AddressProfile>(ProfileType.Address)
            .HasValue<ProtocolProfile>(ProfileType.Protocol)
            .HasValue<SoftwareProfile>(ProfileType.Software);

        modelBuilder.Entity<Profile>()
            .Property(x => x.Type)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        #endregion
    }
}
