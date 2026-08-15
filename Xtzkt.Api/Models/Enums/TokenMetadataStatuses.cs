using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class TokenMetadataStatuses
{
    public const string Pending = "pending";
    public const string FailedToFetch = "failedToFetch";
    public const string FailedToDecode = "failedToDecode";
    public const string SizeLimitExceeded = "sizeLimitExceeded";
    public const string DepthLimitExceeded = "depthLimitExceeded";
    public const string InvalidJson = "invalidJson";
    public const string InvalidUri = "invalidUri";
    public const string Ok = "ok";

    public static string ToString(int status) => status switch
    {
        // 0..99 is the pending range (the value is the number of resolve attempts made so far)
        <= (int)TokenMetadataStatus.MaxRetry => Pending,
        (int)TokenMetadataStatus.FailedToFetch => FailedToFetch,
        (int)TokenMetadataStatus.FailedToDecode => FailedToDecode,
        (int)TokenMetadataStatus.SizeLimitExceeded => SizeLimitExceeded,
        (int)TokenMetadataStatus.DepthLimitExceeded => DepthLimitExceeded,
        (int)TokenMetadataStatus.InvalidJson => InvalidJson,
        (int)TokenMetadataStatus.InvalidUri => InvalidUri,
        (int)TokenMetadataStatus.Ok => Ok,
        _ => throw new InvalidOperationException($"Invalid token metadata status: {status}"),
    };
}
