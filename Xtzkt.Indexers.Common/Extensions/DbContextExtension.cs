using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Indexers.Common.Extensions;

public static class DbContextExtension
{
    public static void TryAttach(this DbContext db, object? obj)
    {
        if (obj != null && db.Entry(obj).State == EntityState.Detached)
            db.Attach(obj);
    }
}
