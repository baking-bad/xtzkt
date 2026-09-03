using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class L1Ghost() : L1Address(AddressType.L1Ghost)
    {
    }

    public static class L1GhostModel
    {
        public static void BuildL1GhostModel(this ModelBuilder modelBuilder)
        {
        }
    }
}
