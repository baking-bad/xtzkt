using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Api.Models.Enums;

internal static class OperationStatuses
{
    public const string Applied     = "applied";
    public const string Backtracked = "backtracked";
    public const string Skipped     = "skipped";
    public const string Failed      = "failed";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { Applied,     (int)OperationStatus.Applied },
        { Backtracked, (int)OperationStatus.Backtracked },
        { Skipped,     (int)OperationStatus.Skipped },
        { Failed,      (int)OperationStatus.Failed },
    };

    public static string ToString(int value) => value switch
    {
        (int)OperationStatus.Applied     => Applied,
        (int)OperationStatus.Backtracked => Backtracked,
        (int)OperationStatus.Skipped     => Skipped,
        (int)OperationStatus.Failed      => Failed,
        _ => throw new Exception("invalid value")
    };
}
