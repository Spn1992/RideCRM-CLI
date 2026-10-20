using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public class InventoryReservationService : IInventoryReservationService
    {
        private readonly ApplicationDbContext _context;

        public InventoryReservationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetAvailableStockAsync(int partId)
        {
            var part = await _context.Part.FirstOrDefaultAsync(p => p.PartId == partId);
            if (part == null || part.IsNonStocked) return 0m;
            return Math.Max(0m, part.QuantityOnHand - part.ReservedQuantity);
        }

        public async Task<InventoryReservationResult> CreateManualReservationAsync(InventoryReservation reservation, string? user = null)
        {
            if (reservation == null)
            {
                return new InventoryReservationResult
                {
                    Success = false,
                    ErrorMessage = "Reservation object cannot be null."
                };
            }

            if (reservation.QuantityReserved <= 0)
            {
                return new InventoryReservationResult
                {
                    Success = false,
                    ErrorMessage = "Reservation quantity must be greater than zero."
                };
            }

            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var isRelational = _context.Database.IsRelational();
                using var transaction = isRelational ? await _context.Database.BeginTransactionAsync() : null;

                try
                {
                    var part = await _context.Part.FirstOrDefaultAsync(p => p.PartId == reservation.PartId);
                    if (part == null)
                    {
                        return new InventoryReservationResult
                        {
                            Success = false,
                            ErrorMessage = $"Part #{reservation.PartId} not found."
                        };
                    }

                    decimal availableStock = part.IsNonStocked ? 0m : Math.Max(0m, part.QuantityOnHand - part.ReservedQuantity);
                    if (reservation.QuantityReserved > availableStock)
                    {
                        return new InventoryReservationResult
                        {
                            Success = false,
                            ErrorMessage = $"Requested reservation quantity ({reservation.QuantityReserved:G29}) exceeds available stock ({availableStock:G29})."
                        };
                    }

                    reservation.ReservationDate = reservation.ReservationDate == default ? DateTime.UtcNow : reservation.ReservationDate;
                    reservation.Status = string.IsNullOrEmpty(reservation.Status) ? "Reserved" : reservation.Status;
                    reservation.CreatedBy = user ?? reservation.CreatedBy ?? "System";

                    _context.InventoryReservation.Add(reservation);
                    part.ReservedQuantity += reservation.QuantityReserved;
                    part.ModifiedDate = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    if (transaction != null)
                    {
                        await transaction.CommitAsync();
                    }

                    return new InventoryReservationResult
                    {
                        Success = true,
                        QuantityRequested = reservation.QuantityReserved,
                        QuantityReserved = reservation.QuantityReserved,
                        QuantityDeficit = 0m,
                        Reservation = reservation
                    };
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync();
                    }

                    if (attempt == maxRetries)
                    {
                        throw;
                    }

                    _context.ChangeTracker.Clear();
                }
            }

            return new InventoryReservationResult
            {
                Success = false,
                ErrorMessage = "Failed to create reservation due to concurrent update conflicts."
            };
        }

        public async Task<InventoryReservationResult> ReserveForCustomerOrderLineAsync(int orderLineId, string? user = null)
        {
            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var isRelational = _context.Database.IsRelational();
                using var transaction = isRelational ? await _context.Database.BeginTransactionAsync() : null;

                try
                {
                    var line = await _context.CustomerOrderLine.FirstOrDefaultAsync(l => l.OrderLineId == orderLineId);
                    if (line == null)
                    {
                        return new InventoryReservationResult
                        {
                            Success = false,
                            ErrorMessage = $"Customer order line #{orderLineId} not found."
                        };
                    }

                    var part = await _context.Part.FirstOrDefaultAsync(p => p.PartId == line.PartId);
                    if (part == null)
                    {
                        return new InventoryReservationResult
                        {
                            Success = false,
                            ErrorMessage = $"Part #{line.PartId} not found."
                        };
                    }

                    decimal requested = line.Quantity;
                    if (requested <= 0)
                    {
                        return new InventoryReservationResult
                        {
                            Success = true,
                            QuantityRequested = requested,
                            QuantityReserved = 0m,
                            QuantityDeficit = 0m
                        };
                    }

                    decimal availableStock = part.IsNonStocked ? 0m : Math.Max(0m, part.QuantityOnHand - part.ReservedQuantity);
                    decimal reserveQty = Math.Min(requested, availableStock);
                    decimal deficitQty = requested - reserveQty;

                    InventoryReservation? reservation = null;
                    PurchasingQueueItem? queueItem = null;

                    if (reserveQty > 0)
                    {
                        reservation = new InventoryReservation
                        {
                            OrderLineId = line.OrderLineId,
                            PartId = part.PartId,
                            QuantityReserved = reserveQty,
                            ReservationDate = DateTime.UtcNow,
                            Status = "Reserved",
                            CreatedBy = user ?? "System"
                        };

                        _context.InventoryReservation.Add(reservation);
                        part.ReservedQuantity += reserveQty;
                        line.QuantityReserved = reserveQty;
                    }
                    else
                    {
                        line.QuantityReserved = 0m;
                    }

                    if (deficitQty > 0)
                    {
                        queueItem = new PurchasingQueueItem
                        {
                            PartId = part.PartId,
                            SourceType = "CustomerOrder",
                            SourceOrderLineId = line.OrderLineId,
                            QuantityRequired = deficitQty,
                            QuantityOrdered = 0m,
                            Status = "Pending",
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = user ?? "System"
                        };

                        if (line.RideId.HasValue && line.RideId.Value > 0)
                        {
                            var ride = await _context.CustomerRide.FirstOrDefaultAsync(r => r.RideId == line.RideId.Value);
                            if (ride != null)
                            {
                                queueItem.RideManufacturer = ride.RideManufacturer;
                                queueItem.RideModel = ride.RideModel;
                                queueItem.SerialNumber = ride.SerialNumber;
                            }
                        }

                        var crossRef = await _context.VendorPartCrossReference
                            .FirstOrDefaultAsync(x => x.PartId == part.PartId && x.IsConfirmed);
                        if (crossRef == null)
                        {
                            crossRef = await _context.VendorPartCrossReference
                                .FirstOrDefaultAsync(x => x.PartId == part.PartId);
                        }

                        if (crossRef != null)
                        {
                            queueItem.SuggestedVendorId = crossRef.VendorId;
                        }
                        if (part.LastVendorCostEur.HasValue)
                        {
                            queueItem.SuggestedCostEur = part.LastVendorCostEur;
                        }

                        _context.PurchasingQueueItem.Add(queueItem);
                        line.QuantityBackordered = deficitQty.ToString("G29");
                    }
                    else
                    {
                        line.QuantityBackordered = "0";
                    }

                    if (reserveQty >= requested)
                    {
                        line.LineStatus = "Reserved";
                    }
                    else if (reserveQty > 0)
                    {
                        line.LineStatus = "Partially Reserved";
                    }
                    else
                    {
                        line.LineStatus = "Backordered";
                    }

                    part.ModifiedDate = DateTime.UtcNow;
                    line.ModifiedDate = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    if (transaction != null)
                    {
                        await transaction.CommitAsync();
                    }

                    return new InventoryReservationResult
                    {
                        Success = true,
                        QuantityRequested = requested,
                        QuantityReserved = reserveQty,
                        QuantityDeficit = deficitQty,
                        Reservation = reservation,
                        PurchasingQueueItem = queueItem
                    };
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync();
                    }

                    if (attempt == maxRetries)
                    {
                        throw;
                    }

                    _context.ChangeTracker.Clear();
                }
            }

            return new InventoryReservationResult
            {
                Success = false,
                ErrorMessage = "Failed to reserve inventory due to concurrent update conflicts."
            };
        }
    }
}
