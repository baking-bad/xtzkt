using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public class XMichelsonGhost() : XMichelsonAddress(AddressType.XMichelsonGhost)
{
}

public static class XMichelsonGhostModel
{
    public static void BuildXMichelsonGhostModel(this ModelBuilder modelBuilder)
    {
    }
}
