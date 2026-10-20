using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public class OrderExceptionValidationService : IOrderExceptionValidationService
    {
        private readonly ApplicationDbContext _context;

        public OrderExceptionValidationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<OrderValidationResult> ValidateAndApplyExceptionsAsync(int orderId)
        {
            var order = await _context.CustomerOrder.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
            {
                return new OrderValidationResult
                {
                    HasException = false,
                    CanConfirm = false
                };
            }

            var lines = await _context.CustomerOrderLine
                .Where(l => l.OrderId == orderId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync();

            var partIds = lines.Select(l => l.PartId).Distinct().ToList();
            var parts = await _context.Part
                .Where(p => partIds.Contains(p.PartId))
                .ToDictionaryAsync(p => p.PartId);

            // Read system configurations
            decimal minMargin = await GetConfigDecimalAsync("MinimumGrossMargin", 0.20m);
            decimal euroConversionFactor = await GetConfigDecimalAsync("EuroConversionFactor", 1.10m);
            decimal pricingMarkup = await GetConfigDecimalAsync("PricingMarkup", 1.40m);

            var orderExceptionReasons = new List<string>();

            foreach (var line in lines)
            {
                parts.TryGetValue(line.PartId, out var part);
                var lineReasons = new List<string>();

                // Calculate catalog cost in USD
                decimal costUsd = 0m;
                if (part != null && part.LastVendorCostEur.HasValue && part.LastVendorCostEur.Value > 0)
                {
                    costUsd = Math.Round(part.LastVendorCostEur.Value * euroConversionFactor, 4);
                }
                else if (part != null && part.SellingPriceUsd.HasValue && part.SellingPriceUsd.Value > 0 && pricingMarkup > 0)
                {
                    costUsd = Math.Round(part.SellingPriceUsd.Value / pricingMarkup, 4);
                }

                line.CostUsd = costUsd;

                // Validate Gross Margin
                line.HasMarginException = false;
                if (line.UnitPriceUsd <= 0)
                {
                    line.Margin = 0m;
                    line.HasMarginException = true;
                    string reason = $"Line {line.LineNumber} ({(part != null ? part.PartNumber : "Part #" + line.PartId)}): Unit price is ${line.UnitPriceUsd:F2}, yielding 0% margin, below minimum required margin of {minMargin:P0}.";
                    lineReasons.Add(reason);
                }
                else
                {
                    decimal margin = (line.UnitPriceUsd - costUsd) / line.UnitPriceUsd;
                    line.Margin = Math.Round(margin, 4);
                    if (margin < minMargin)
                    {
                        line.HasMarginException = true;
                        string reason = $"Line {line.LineNumber} ({(part != null ? part.PartNumber : "Part #" + line.PartId)}): Unit price ${line.UnitPriceUsd:F2} yields margin {margin:P1}, below minimum required margin of {minMargin:P0} (Catalog Cost: ${costUsd:F2}).";
                        lineReasons.Add(reason);
                    }
                }

                // Validate Stock Availability
                line.HasStockException = false;
                decimal availableStock = part != null ? (part.QuantityOnHand - part.ReservedQuantity) : 0m;

                if (part != null && part.IsNonStocked)
                {
                    if (line.Quantity > 0)
                    {
                        line.HasStockException = true;
                        string reason = $"Line {line.LineNumber} ({part.PartNumber}): Part is non-stocked (Available stock: 0).";
                        lineReasons.Add(reason);
                    }
                }
                else if (line.Quantity > availableStock)
                {
                    line.HasStockException = true;
                    string reason = $"Line {line.LineNumber} ({(part != null ? part.PartNumber : "Part #" + line.PartId)}): Requested quantity ({line.Quantity:G29}) exceeds available stock ({availableStock:G29}).";
                    lineReasons.Add(reason);
                }

                line.ExceptionReason = lineReasons.Count > 0 ? string.Join("; ", lineReasons) : null;
                orderExceptionReasons.AddRange(lineReasons);
            }

            if (orderExceptionReasons.Any())
            {
                order.HasException = true;
                order.ExceptionReason = string.Join(" | ", orderExceptionReasons);
                if (order.ExceptionStatus != "Approved")
                {
                    order.ExceptionStatus = "Pending Review";
                    order.Status = "Pending Exception Review";
                }
            }
            else
            {
                order.HasException = false;
                order.ExceptionReason = null;
                order.ExceptionStatus = null;
                if (order.Status == "Pending Exception Review")
                {
                    order.Status = "Draft";
                }
            }

            order.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            bool canConfirm = !order.HasException || order.ExceptionStatus == "Approved";

            return new OrderValidationResult
            {
                HasException = order.HasException,
                ExceptionReason = order.ExceptionReason,
                ExceptionStatus = order.ExceptionStatus,
                CanConfirm = canConfirm
            };
        }

        public async Task<bool> CanConfirmOrderAsync(int orderId)
        {
            var order = await _context.CustomerOrder.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return false;

            if (order.HasException && order.ExceptionStatus != "Approved")
            {
                return false;
            }
            return true;
        }

        public async Task ApproveOrderExceptionsAsync(int orderId, string supervisorUsername, string? comments)
        {
            var order = await _context.CustomerOrder.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return;

            order.ExceptionStatus = "Approved";
            order.ApprovedBy = string.IsNullOrWhiteSpace(supervisorUsername) ? "Supervisor" : supervisorUsername;
            order.ApprovedDate = DateTime.UtcNow;
            order.SupervisorComments = comments;
            order.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task RejectOrderExceptionsAsync(int orderId, string supervisorUsername, string? comments)
        {
            var order = await _context.CustomerOrder.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return;

            order.ExceptionStatus = "Rejected";
            order.ApprovedBy = string.IsNullOrWhiteSpace(supervisorUsername) ? "Supervisor" : supervisorUsername;
            order.ApprovedDate = DateTime.UtcNow;
            order.SupervisorComments = comments;
            order.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private async Task<decimal> GetConfigDecimalAsync(string key, decimal defaultValue)
        {
            var config = await _context.SystemConfiguration.FirstOrDefaultAsync(c => c.ConfigKey == key);
            if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue))
            {
                string valStr = config.ConfigValue.Trim().Replace("%", "");
                if (decimal.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
                {
                    if (val > 1.0m && key.Contains("Margin"))
                    {
                        val /= 100m; // Convert e.g. 20 to 0.20
                    }
                    return val;
                }
            }
            return defaultValue;
        }
    }
}
