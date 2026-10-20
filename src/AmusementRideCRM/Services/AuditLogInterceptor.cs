using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AmusementRideCRM.Services;

public class AuditLogInterceptor : SaveChangesInterceptor
{
    // Note: ApplicationDbContext overrides SaveChanges and SaveChangesAsync directly.
    // This interceptor class is provided for EF Core configuration extension points.
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
