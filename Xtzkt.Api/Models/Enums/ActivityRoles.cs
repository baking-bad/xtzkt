using Xtzkt.Api.Models.Abstract;

namespace Xtzkt.Api.Models.Enums;

public class ActivityRoles
{
    public const string Sender = "sender";
    public const string Target = "target";
    public const string Initiator = "initiator";
    public const string Mention = "mention";
    public const string All = "all";

    public static bool IsValid(string value) => value switch
    {
        Sender => true,
        Target => true,
        Initiator => true,
        Mention => true,
        All => true,
        _ => false
    };

    public static readonly ActivityRole Default = ActivityRole.All;
}
