#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Pages.Shipment
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IShippingService _shippingService;

        public DetailsModel(ApplicationDbContext context, IShippingService shippingService)
        {
            _context = context;
            _shippingService = shippingService;
        }

        public AmusementRideCRM.Models.Shipment Item { get; set; } = default!;
        public List<AmusementRideCRM.Models.ShipmentLine> ShipmentLines { get; set; } = new();
        public List<AmusementRideCRM.Models.ShipmentPackage> Packages { get; set; } = new();
        public Dictionary<int, AmusementRideCRM.Models.Part> PartsDict { get; set; } = new();
        public List<AmusementRideCRM.Models.Part> AvailableParts { get; set; } = new();
        public List<AmusementRideCRM.Models.CustomerRide> CustomerRides { get; set; } = new();
        public List<AmusementRideCRM.Models.SerializedPartUnit> SerializedUnits { get; set; } = new();
        public List<AmusementRideCRM.Models.SerializedPartUnit> AvailableInStockUnits { get; set; } = new();

        public ShippingRateQuoteResult? RateQuote { get; set; }

        [BindProperty]
        public AmusementRideCRM.Models.ShipmentLine NewLine { get; set; } = new();

        [BindProperty]
        public AmusementRideCRM.Models.ShipmentPackage NewPackage { get; set; } = new();

        [BindProperty]
        public string SelectedCarrier { get; set; } = "UPS";

        [BindProperty]
        public string SelectedServiceType { get; set; } = "03"; // 03: UPS Ground

        [BindProperty]
        public int RideId { get; set; }

        [BindProperty]
        public List<int> SelectedUnitIds { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.Shipment.FirstOrDefaultAsync(m => m.ShipmentId == id);
            if (item == null)
            {
                return NotFound();
            }
            Item = item;
            SelectedCarrier = string.IsNullOrWhiteSpace(item.Carrier) ? "UPS" : item.Carrier;
            SelectedServiceType = string.IsNullOrWhiteSpace(item.ServiceType) ? "03" : item.ServiceType;

            await LoadShipmentDetailsAsync(id);
            return Page();
        }

        private async Task LoadShipmentDetailsAsync(int shipmentId)
        {
            ShipmentLines = await _context.ShipmentLine
                .Where(s => s.ShipmentId == shipmentId)
                .ToListAsync();

            Packages = await _context.ShipmentPackage
                .Where(p => p.ShipmentId == shipmentId)
                .OrderBy(p => p.PackageNumber)
                .ToListAsync();

            var partIds = ShipmentLines.Select(s => s.PartId).Distinct().ToList();
            var parts = await _context.Part.Where(p => partIds.Contains(p.PartId)).ToListAsync();
            PartsDict = parts.ToDictionary(p => p.PartId);

            AvailableParts = await _context.Part.Where(p => p.IsActive || !p.IsMerged).Take(100).ToListAsync();
            CustomerRides = await _context.CustomerRide.Where(r => r.IsActive).ToListAsync();

            SerializedUnits = await _context.SerializedPartUnit
                .Include(u => u.Part)
                .Include(u => u.CustomerRide)
                .Where(u => u.ShipmentId == shipmentId)
                .ToListAsync();

            AvailableInStockUnits = await _context.SerializedPartUnit
                .Include(u => u.Part)
                .Where(u => u.Status == "InStock" || u.Status == "Received")
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAddLineAsync(int id)
        {
            var shipment = await _context.Shipment.FirstOrDefaultAsync(m => m.ShipmentId == id);
            if (shipment == null) return NotFound();

            NewLine.ShipmentId = id;
            NewLine.CreatedDate = DateTime.UtcNow;

            var part = await _context.Part.FirstOrDefaultAsync(p => p.PartId == NewLine.PartId);
            if (part == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid Part selected.");
                Item = shipment;
                await LoadShipmentDetailsAsync(id);
                return Page();
            }

            if (part.IsSerialized)
            {
                if (SelectedUnitIds == null || SelectedUnitIds.Count != (int)NewLine.QuantityShipped)
                {
                    ModelState.AddModelError(string.Empty, $"Part {part.PartNumber} is serialized. Exactly {(int)NewLine.QuantityShipped} serial number unit(s) must be selected.");
                    Item = shipment;
                    await LoadShipmentDetailsAsync(id);
                    return Page();
                }
            }

            _context.ShipmentLine.Add(NewLine);
            await _context.SaveChangesAsync();

            if (part.IsSerialized && SelectedUnitIds != null && SelectedUnitIds.Any())
            {
                try
                {
                    await SerializedPartService.ShipSerializedUnitsAsync(
                        _context,
                        NewLine.ShipmentLineId,
                        id,
                        RideId,
                        SelectedUnitIds,
                        User.Identity?.Name ?? "Shipping Operator");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                    Item = shipment;
                    await LoadShipmentDetailsAsync(id);
                    return Page();
                }
            }

            // Decrement part stock quantities
            part.QuantityOnHand = Math.Max(0, part.QuantityOnHand - NewLine.QuantityShipped);
            part.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddPackageAsync(int id)
        {
            var shipment = await _context.Shipment.FirstOrDefaultAsync(m => m.ShipmentId == id);
            if (shipment == null) return NotFound();

            if (NewPackage.WeightLbs <= 0 || NewPackage.LengthIn <= 0 || NewPackage.WidthIn <= 0 || NewPackage.HeightIn <= 0)
            {
                ModelState.AddModelError(string.Empty, "Package dimensions (Length, Width, Height) and Weight must be greater than zero.");
                Item = shipment;
                await LoadShipmentDetailsAsync(id);
                return Page();
            }

            var existingPackages = await _context.ShipmentPackage.Where(p => p.ShipmentId == id).ToListAsync();
            int nextNumber = existingPackages.Any() ? existingPackages.Max(p => p.PackageNumber) + 1 : 1;

            NewPackage.ShipmentId = id;
            NewPackage.PackageNumber = nextNumber;
            NewPackage.CreatedDate = DateTime.UtcNow;

            _context.ShipmentPackage.Add(NewPackage);
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDeletePackageAsync(int packageId, int id)
        {
            var pkg = await _context.ShipmentPackage.FirstOrDefaultAsync(p => p.PackageId == packageId && p.ShipmentId == id);
            if (pkg != null)
            {
                _context.ShipmentPackage.Remove(pkg);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostGetRateQuoteAsync(int id)
        {
            var shipment = await _context.Shipment.FirstOrDefaultAsync(m => m.ShipmentId == id);
            if (shipment == null) return NotFound();

            Item = shipment;
            shipment.Carrier = SelectedCarrier;
            shipment.ServiceType = SelectedServiceType;
            await _context.SaveChangesAsync();

            await LoadShipmentDetailsAsync(id);

            if (!Packages.Any())
            {
                ModelState.AddModelError(string.Empty, "Please add at least one package with dimensions and weight before requesting a rate quote.");
                return Page();
            }

            var order = await _context.CustomerOrder.FirstOrDefaultAsync(o => o.OrderId == shipment.OrderId);

            var rateRequest = new ShippingRateRequest
            {
                Carrier = SelectedCarrier,
                ServiceType = SelectedServiceType,
                ShipTo = new ShippingAddressDto
                {
                    Name = shipment.ShipToName ?? order?.BillingAddress1 ?? "Valued Customer",
                    AddressLine1 = shipment.ShipToAddress1 ?? order?.ShippingAddress1 ?? "123 Main St",
                    AddressLine2 = shipment.ShipToAddress2 ?? order?.ShippingAddress2,
                    City = shipment.ShipToCity ?? order?.ShippingCity ?? "Orlando",
                    State = shipment.ShipToState ?? order?.ShippingState ?? "FL",
                    PostalCode = shipment.ShipToPostalCode ?? order?.ShippingPostalCode ?? "32801",
                    Country = shipment.ShipToCountry ?? order?.ShippingCountry ?? "US"
                },
                Packages = Packages.Select(p => new ShippingPackageDto
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

            RateQuote = await _shippingService.GetRateQuoteAsync(rateRequest);
            if (!RateQuote.Success)
            {
                ModelState.AddModelError(string.Empty, $"Rate Quote Failed: {RateQuote.ErrorMessage}");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDispatchAsync(int id)
        {
            var shipment = await _context.Shipment.FirstOrDefaultAsync(m => m.ShipmentId == id);
            if (shipment == null) return NotFound();

            shipment.Carrier = SelectedCarrier;
            shipment.ServiceType = SelectedServiceType;
            await _context.SaveChangesAsync();

            var result = await ShipmentDispatchWorkflow.DispatchShipmentAsync(
                _context,
                _shippingService,
                id,
                User.Identity?.Name ?? "Dispatcher");

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, $"Shipment Dispatch Failed: {result.ErrorMessage}");
                Item = shipment;
                await LoadShipmentDetailsAsync(id);
                return Page();
            }

            TempData["SuccessMessage"] = $"Shipment successfully dispatched via {SelectedCarrier}! Master Tracking Number: {result.TrackingNumber}";
            return RedirectToPage(new { id });
        }
    }
}
