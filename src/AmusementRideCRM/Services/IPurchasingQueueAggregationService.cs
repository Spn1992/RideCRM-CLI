using System.Collections.Generic;
using System.Threading.Tasks;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public class QueueValidationWarning
    {
        public int? QueueItemId { get; set; }
        public int? VendorId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class AggregationValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<QueueValidationWarning> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public int EligibleItemCount { get; set; }
    }

    public class AggregationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<int> CreatedPurchaseOrderIds { get; set; } = new();
        public List<PurchaseOrder> CreatedPurchaseOrders { get; set; } = new();
        public int ProcessedItemCount { get; set; }
        public List<QueueValidationWarning> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public interface IPurchasingQueueAggregationService
    {
        Task<AggregationValidationResult> ValidateForConsolidationAsync(IEnumerable<int> queueItemIds);
        Task<AggregationResult> ConsolidateQueueItemsAsync(IEnumerable<int> queueItemIds, string? user = null);
    }
}
