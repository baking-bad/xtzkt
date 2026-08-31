using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace Xtzkt.Indexers.Common.Extensions;

public static class DbContextExtension
{
    public static void TryAttach(this DbContext db, object? obj)
    {
#pragma warning disable EF1001 // Internal EF Core API usage.
        if (obj != null)
        {
            var entry = ((IDbContextDependencies)db).StateManager.TryGetEntry(obj);
            if (entry == null || entry.EntityState == EntityState.Detached)
                db.Attach(obj);
        }
#pragma warning restore EF1001 // Internal EF Core API usage.
    }
}
