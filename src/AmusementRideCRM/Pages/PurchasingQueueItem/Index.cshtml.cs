using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Pages.PurchasingQueueItem
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IPurchasingQueueAggregationService _aggregationService;

        public IndexModel(ApplicationDbContext context, IPurchasingQueueAggregationService aggregationService)
        {
            _context = context;
            _aggregationService = aggregationService;
        }

        public IList<AmusementRideCRM.Models.PurchasingQueueItem> Items { get; set; } = new List<AmusementRideCRM.Models.PurchasingQueueItem>();
        public Dictionary<int, AmusementRideCRM.Models.VendorMaster> VendorsDict { get; set; } = new();
        public Dictionary<int, AmusementRideCRM.Models.Part> PartsDict { get; set; } = new();

        public List<AmusementRideCRM.Models.VendorMaster> AvailableVendors { get; set; } = new();
        public List<string> AvailableRideModels { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? VendorFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RideModelFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public List<QueueValidationWarning> Warnings { get; set; } = new();
        public List<int> CreatedPOIds { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            AvailableVendors = await _context.VendorMaster.OrderBy(v => v.VendorName).ToListAsync();
            AvailableRideModels = await _context.PurchasingQueueItem
                .Where(q => !string.IsNullOrEmpty(q.RideModel))
                .Select(q => q.RideModel!)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

            var query = _context.PurchasingQueueItem.AsQueryable();

            if (VendorFilter.HasValue && VendorFilter.Value > 0)
            {
                query = query.Where(q => q.SuggestedVendorId == VendorFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(RideModelFilter))
            {
                query = query.Where(q => q.RideModel == RideModelFilter);
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                if (StatusFilter == "Pending")
                {
                    query = query.Where(q => string.IsNullOrEmpty(q.Status) || (!q.Status.Equals("Consolidated") && !q.Status.Equals("Ordered")));
                }
                else
                {
                    query = query.Where(q => q.Status == StatusFilter);
                }
            }

            Items = await query.OrderByDescending(q => q.QueueItemId).Take(200).ToListAsync();

            var vendorIds = Items.Where(i => i.SuggestedVendorId.HasValue).Select(i => i.SuggestedVendorId!.Value).Distinct().ToList();
            if (vendorIds.Any())
            {
                VendorsDict = await _context.VendorMaster
                    .Where(v => vendorIds.Contains(v.VendorId))
                    .ToDictionaryAsync(v => v.VendorId);
            }

            var partIds = Items.Select(i => i.PartId).Distinct().ToList();
            if (partIds.Any())
            {
                PartsDict = await _context.Part
                    .Where(p => partIds.Contains(p.PartId))
                    .ToDictionaryAsync(p => p.PartId);
            }
        }

        public async Task<IActionResult> OnPostConsolidateAsync(List<int> selectedItemIds)
        {
            var username = User?.Identity?.Name ?? "Buyer";
            var result = await _aggregationService.ConsolidateQueueItemsAsync(selectedItemIds, username);

            Warnings = result.Warnings;

            if (result.Success)
            {
                SuccessMessage = result.Message;
                CreatedPOIds = result.CreatedPurchaseOrderIds;
            }
            else
            {
                ErrorMessage = string.Join(" | ", result.Errors);
            }

            await LoadDataAsync();
            return Page();
        }
    }
}
