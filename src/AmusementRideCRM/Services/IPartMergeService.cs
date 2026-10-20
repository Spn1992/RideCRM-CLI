using System.Threading;
using System.Threading.Tasks;

namespace AmusementRideCRM.Services
{
    public interface IPartMergeService
    {
        Task ExecuteMergeAsync(
            int survivorPartId,
            int mergedPartId,
            string? userName = null,
            string? notes = null,
            int? userId = null,
            CancellationToken cancellationToken = default);
    }
}
