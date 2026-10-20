#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Pages.SafetyRecall
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? PartNumber { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SerialNumber { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? WarrantyStartFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? WarrantyStartTo { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool ActiveWarrantyOnly { get; set; }

        public List<SerializedPartUnit> RecallResults { get; set; } = new();

        public async Task OnGetAsync()
        {
            RecallResults = await SerializedPartService.SearchRecallAndWarrantyAsync(
                _context,
                PartNumber,
                SerialNumber,
                WarrantyStartFrom,
                WarrantyStartTo,
                ActiveWarrantyOnly);
        }
    }
}
