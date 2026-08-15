using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class BigMapActions
{
    public const string Allocate  = "allocate";
    public const string AddKey    = "add_key";
    public const string UpdateKey = "update_key";
    public const string RemoveKey = "remove_key";
    public const string Remove    = "remove";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { Allocate,  (int)BigMapAction.Allocate },
        { AddKey,    (int)BigMapAction.AddKey },
        { UpdateKey, (int)BigMapAction.UpdateKey },
        { RemoveKey, (int)BigMapAction.RemoveKey },
        { Remove,    (int)BigMapAction.Remove },
    };

    public static string ToString(int value) => value switch
    {
        (int)BigMapAction.Allocate  => Allocate,
        (int)BigMapAction.AddKey    => AddKey,
        (int)BigMapAction.UpdateKey => UpdateKey,
        (int)BigMapAction.RemoveKey => RemoveKey,
        (int)BigMapAction.Remove    => Remove,
        _ => throw new Exception("invalid value")
    };
}
