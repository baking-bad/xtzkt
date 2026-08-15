using System.Numerics;
using Xtzkt.Data.Models;

namespace Xtzkt.Services.Metadata.Models;

public sealed record TokenInfo(
    long Id,
    string Contract,
    BigInteger TokenId,
    TokenTags Tags,
    TokenMetadataStatus Status);

public sealed record TokenLinkInfo(
    long Id,
    string Link,
    BigInteger TokenId,
    TokenTags Tags,
    TokenMetadataStatus Status);
