namespace Xtzkt.Services.Domains.Models;

public sealed record BlockInfo(
    int ChainId,
    int Level,
    byte[] Hash);
