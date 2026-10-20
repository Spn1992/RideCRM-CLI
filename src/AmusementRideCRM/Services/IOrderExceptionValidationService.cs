using System;
using System.Threading.Tasks;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public class OrderValidationResult
    {
        public bool HasException { get; set; }
        public string? ExceptionReason { get; set; }
        public string? ExceptionStatus { get; set; }
        public bool CanConfirm { get; set; }
    }

    public interface IOrderExceptionValidationService
    {
        Task<OrderValidationResult> ValidateAndApplyExceptionsAsync(int orderId);
        Task<bool> CanConfirmOrderAsync(int orderId);
        Task ApproveOrderExceptionsAsync(int orderId, string supervisorUsername, string? comments);
        Task RejectOrderExceptionsAsync(int orderId, string supervisorUsername, string? comments);
    }
}
