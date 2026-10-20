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

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public AmusementRideCRM.Models.Shipment Item { get; set; } = default!;
        public List<AmusementRideCRM.Models.ShipmentLine> ShipmentLines { get; set; } = new();
        public Dictionary<int, AmusementRideCRM.Models.Part> PartsDict { get; set; } = new();
        public List<AmusementRideCRM.Models.Part> AvailableParts { get; set; } = new();
        public List<AmusementRideCRM.Models.CustomerRide> CustomerRides { get; set; } = new();
        public List<AmusementRideCRM.Models.SerializedPartUnit> SerializedUnits { get; set; } = new();
        public List<AmusementRideCRM.Models.SerializedPartUnit> AvailableInStockUnits { get; set; } = new();

        [BindProperty]
        public AmusementRideCRM.Models.ShipmentLine NewLine { get; set; } = new();

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

            await LoadShipmentDetailsAsync(id);
            return Page();
        }

        private async Task LoadShipmentDetailsAsync(int shipmentId)
        {
            ShipmentLines = await _context.ShipmentLine
                .Where(s => s.ShipmentId == shipmentId)
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
    }
}
