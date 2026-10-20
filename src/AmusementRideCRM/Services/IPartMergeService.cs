using System.Threading.Tasks;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public interface IPartMergeService
    {
        Task<PartMergeResult> ExecuteMergeAsync(int survivorPartId, int mergedPartId, string? mergedByUserName = null, string? notes = null, int? mergedByUserId = null, string? mergeDetails = null);
    }

    public class PartMergeResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public PartMergeHistory? MergeHistory { get; set; }
    }
}
