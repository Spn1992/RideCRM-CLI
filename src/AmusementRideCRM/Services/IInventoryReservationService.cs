using System.Threading.Tasks;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public class InventoryReservationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal QuantityRequested { get; set; }
        public decimal QuantityReserved { get; set; }
        public decimal QuantityDeficit { get; set; }
        public InventoryReservation? Reservation { get; set; }
        public PurchasingQueueItem? PurchasingQueueItem { get; set; }
    }

    public interface IInventoryReservationService
    {
        Task<InventoryReservationResult> ReserveForCustomerOrderLineAsync(int orderLineId, string? user = null);
        Task<InventoryReservationResult> CreateManualReservationAsync(InventoryReservation reservation, string? user = null);
        Task<decimal> GetAvailableStockAsync(int partId);
    }
}
