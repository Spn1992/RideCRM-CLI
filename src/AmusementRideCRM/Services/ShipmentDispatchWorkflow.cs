using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public static class ShipmentDispatchWorkflow
    {
        public static async Task<ShipmentDispatchResult> DispatchShipmentAsync(
            ApplicationDbContext context,
            IShippingService shippingService,
            int shipmentId,
            string? user = null,
            CancellationToken cancellationToken = default)
        {
            var shipment = await context.Shipment.FirstOrDefaultAsync(s => s.ShipmentId == shipmentId, cancellationToken);
            if (shipment == null)
            {
                return new ShipmentDispatchResult
                {
                    Success = false,
                    ErrorMessage = $"Shipment #{shipmentId} was not found."
                };
            }

            if (shipment.Status == "Dispatched" || shipment.Status == "Shipped")
            {
                return new ShipmentDispatchResult
                {
                    Success = false,
                    ErrorMessage = $"Shipment #{shipment.ShipmentNumber} is already dispatched/shipped."
                };
            }

            var packages = await context.ShipmentPackage
                .Where(p => p.ShipmentId == shipmentId)
                .OrderBy(p => p.PackageNumber)
                .ToListAsync(cancellationToken);

            if (!packages.Any())
            {
                return new ShipmentDispatchResult
                {
                    Success = false,
                    ErrorMessage = "At least one package with weight and dimensions is required before dispatching the shipment."
                };
            }

            foreach (var pkg in packages)
            {
                if (!pkg.WeightLbs.HasValue || pkg.WeightLbs <= 0 ||
                    !pkg.LengthIn.HasValue || pkg.LengthIn <= 0 ||
                    !pkg.WidthIn.HasValue || pkg.WidthIn <= 0 ||
                    !pkg.HeightIn.HasValue || pkg.HeightIn <= 0)
                {
                    return new ShipmentDispatchResult
                    {
                        Success = false,
                        ErrorMessage = $"Package #{pkg.PackageNumber} has missing or invalid dimensions/weight. Weight, Length, Width, and Height must all be greater than zero."
                    };
                }
            }

            var shipmentLines = await context.ShipmentLine
                .Where(l => l.ShipmentId == shipmentId)
                .ToListAsync(cancellationToken);

            if (!shipmentLines.Any())
            {
                return new ShipmentDispatchResult
                {
                    Success = false,
                    ErrorMessage = "Cannot dispatch shipment with no shipped line items."
                };
            }

            var order = await context.CustomerOrder
                .FirstOrDefaultAsync(o => o.OrderId == shipment.OrderId, cancellationToken);

            var shipToName = shipment.ShipToName ?? order?.BillingAddress1 ?? "Valued Customer";
            var shipToAddr1 = shipment.ShipToAddress1 ?? order?.ShippingAddress1 ?? order?.BillingAddress1 ?? "123 Main St";
            var shipToAddr2 = shipment.ShipToAddress2 ?? order?.ShippingAddress2 ?? order?.BillingAddress2;
            var shipToCity = shipment.ShipToCity ?? order?.ShippingCity ?? order?.BillingCity ?? "Orlando";
            var shipToState = shipment.ShipToState ?? order?.ShippingState ?? order?.BillingState ?? "FL";
            var shipToPostal = shipment.ShipToPostalCode ?? order?.ShippingPostalCode ?? order?.BillingPostalCode ?? "32801";
            var shipToCountry = shipment.ShipToCountry ?? order?.ShippingCountry ?? order?.BillingCountry ?? "US";

            var dispatchRequest = new ShipmentDispatchRequest
            {
                ShipmentId = shipment.ShipmentId,
                ShipmentNumber = shipment.ShipmentNumber,
                Carrier = string.IsNullOrWhiteSpace(shipment.Carrier) ? "UPS" : shipment.Carrier,
                ServiceType = string.IsNullOrWhiteSpace(shipment.ServiceType) ? "03" : shipment.ServiceType,
                ShipTo = new ShippingAddressDto
                {
                    Name = shipToName,
                    AddressLine1 = shipToAddr1,
                    AddressLine2 = shipToAddr2,
                    City = shipToCity,
                    State = shipToState,
                    PostalCode = shipToPostal,
                    Country = shipToCountry
                },
                Packages = packages.Select(p => new ShippingPackageDto
                {
                    PackageId = p.PackageId,
                    PackageNumber = p.PackageNumber,
                    WeightLbs = p.WeightLbs ?? 1m,
                    LengthIn = p.LengthIn ?? 10m,
                    WidthIn = p.WidthIn ?? 10m,
                    HeightIn = p.HeightIn ?? 10m,
                    InsuredValue = p.InsuredValue
                }).ToList()
            };

            var dispatchResult = await shippingService.CreateShipmentLabelAsync(dispatchRequest, cancellationToken);

            if (!dispatchResult.Success)
            {
                return dispatchResult;
            }

            var isRelational = context.Database.IsRelational();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            if (isRelational)
            {
                transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            }

            try
            {
                shipment.TrackingNumber = dispatchResult.TrackingNumber;
                shipment.ShippingLabelUrl = dispatchResult.ShippingLabelUrl;
                if (dispatchResult.ActualCost.HasValue)
                {
                    shipment.ShippingCostActual = dispatchResult.ActualCost.Value;
                }
                shipment.Status = "Dispatched";
                shipment.ModifiedDate = DateTime.UtcNow;
                shipment.ModifiedBy = user ?? "System";

                if (dispatchResult.PackageTrackingNumbers != null && dispatchResult.PackageTrackingNumbers.Any())
                {
                    foreach (var pkgTrack in dispatchResult.PackageTrackingNumbers)
                    {
                        var targetPkg = packages.FirstOrDefault(p => p.PackageId == pkgTrack.PackageId || p.PackageNumber == pkgTrack.PackageNumber);
                        if (targetPkg != null)
                        {
                            targetPkg.TrackingNumber = pkgTrack.TrackingNumber;
                            targetPkg.LabelImageUrl = pkgTrack.LabelImageUrl ?? dispatchResult.ShippingLabelUrl;
                        }
                    }
                }

                foreach (var line in shipmentLines)
                {
                    CustomerOrderLine? orderLine = null;
                    if (line.OrderLineId > 0)
                    {
                        orderLine = await context.CustomerOrderLine.FirstOrDefaultAsync(l => l.OrderLineId == line.OrderLineId, cancellationToken);
                    }
                    if (orderLine == null && shipment.OrderId > 0)
                    {
                        orderLine = await context.CustomerOrderLine.FirstOrDefaultAsync(l => l.OrderId == shipment.OrderId && l.PartId == line.PartId, cancellationToken);
                    }

                    if (orderLine != null)
                    {
                        orderLine.QuantityShipped += line.QuantityShipped;
                        orderLine.QuantityReserved = Math.Max(0, orderLine.QuantityReserved - line.QuantityShipped);
                        orderLine.LineStatus = orderLine.QuantityShipped >= orderLine.Quantity ? "Shipped" : "Partially Shipped";
                        orderLine.ModifiedDate = DateTime.UtcNow;
                    }

                    List<InventoryReservation> reservations = new();
                    if (orderLine != null)
                    {
                        reservations = await context.InventoryReservation
                            .Where(r => r.OrderLineId == orderLine.OrderLineId && (r.Status == "Reserved" || r.Status == "Active"))
                            .ToListAsync(cancellationToken);
                    }
                    if (!reservations.Any())
                    {
                        reservations = await context.InventoryReservation
                            .Where(r => r.PartId == line.PartId && (r.Status == "Reserved" || r.Status == "Active"))
                            .ToListAsync(cancellationToken);
                    }

                    decimal totalReleasedForLine = 0m;
                    foreach (var res in reservations)
                    {
                        res.Status = "Released";
                        res.ReleasedDate = DateTime.UtcNow;
                        totalReleasedForLine += res.QuantityReserved;

                        if (res.HoldingLocationId.HasValue)
                        {
                            int hlocId = res.HoldingLocationId.Value;
                            var remainingActiveRes = await context.InventoryReservation
                                .AnyAsync(r => r.HoldingLocationId == hlocId && r.ReservationId != res.ReservationId && (r.Status == "Reserved" || r.Status == "Active"), cancellationToken);

                            if (!remainingActiveRes)
                            {
                                var hloc = await context.HoldingLocation.FirstOrDefaultAsync(h => h.HoldingLocationId == hlocId, cancellationToken);
                                if (hloc != null)
                                {
                                    hloc.IsAvailable = true;
                                    hloc.ModifiedDate = DateTime.UtcNow;
                                    hloc.ModifiedBy = user ?? "System";
                                }
                            }
                        }
                    }

                    var part = await context.Part.FirstOrDefaultAsync(p => p.PartId == line.PartId, cancellationToken);
                    if (part != null)
                    {
                        decimal releaseQty = totalReleasedForLine > 0 ? totalReleasedForLine : line.QuantityShipped;
                        part.ReservedQuantity = Math.Max(0, part.ReservedQuantity - releaseQty);
                        part.AvailableQuantity = part.QuantityOnHand - part.ReservedQuantity;
                        part.ModifiedDate = DateTime.UtcNow;
                        part.ModifiedBy = user ?? "System";
                    }
                }

                if (order != null)
                {
                    var allOrderLines = await context.CustomerOrderLine.Where(l => l.OrderId == order.OrderId).ToListAsync(cancellationToken);
                    if (allOrderLines.Any())
                    {
                        if (allOrderLines.All(l => l.QuantityShipped >= l.Quantity))
                        {
                            order.Status = "Shipped";
                        }
                        else if (allOrderLines.Any(l => l.QuantityShipped > 0))
                        {
                            order.Status = "Partially Shipped";
                        }
                    }
                    else
                    {
                        order.Status = "Shipped";
                    }

                    if (order.HoldingLocationId.HasValue)
                    {
                        int orderHlocId = order.HoldingLocationId.Value;
                        var activeOrderReservations = await context.InventoryReservation
                            .AnyAsync(r => r.HoldingLocationId == orderHlocId && (r.Status == "Reserved" || r.Status == "Active"), cancellationToken);

                        if (!activeOrderReservations)
                        {
                            var hloc = await context.HoldingLocation.FirstOrDefaultAsync(h => h.HoldingLocationId == orderHlocId, cancellationToken);
                            if (hloc != null)
                            {
                                hloc.IsAvailable = true;
                                hloc.ModifiedDate = DateTime.UtcNow;
                            }
                        }
                    }

                    order.ModifiedDate = DateTime.UtcNow;
                    order.ModifiedBy = user ?? "System";
                }

                await context.SaveChangesAsync(cancellationToken);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return dispatchResult;
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }
    }
}
