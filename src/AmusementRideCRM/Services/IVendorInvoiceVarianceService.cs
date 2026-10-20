using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AmusementRideCRM.Models;
using Microsoft.AspNetCore.Http;

namespace AmusementRideCRM.Services
{
    public class LineVarianceInfo
    {
        public int VILineId { get; set; }
        public int LineNumber { get; set; }
        public int? PartId { get; set; }
        public string? VendorPartNumber { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCostEur { get; set; }
        public decimal BaselineUnitCostEur { get; set; }
        public string BaselineSource { get; set; } = string.Empty; // "PurchaseOrderLine", "PartMaster", "VendorSource", or "None"
        public decimal VariancePercentage { get; set; }
        public bool ExceedsThreshold { get; set; } // > 5% variance
    }

    public class VendorInvoiceEvaluationResult
    {
        public int VendorInvoiceId { get; set; }
        public bool HasDiscrepancy { get; set; }
        public string Status { get; set; } = "Approved"; // "Approved" or "Pending Review"
        public string DiscrepancyNotes { get; set; } = string.Empty;
        public List<LineVarianceInfo> LineVariances { get; set; } = new();
    }

    public class ExtractedLineItem
    {
        public int LineNumber { get; set; }
        public int? PartId { get; set; }
        public string? VendorPartNumber { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCostEur { get; set; }
        public int? POLineId { get; set; }
        public decimal ConfidenceScore { get; set; } = 0.95m;
    }

    public class ExtractionResult
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public int? VendorId { get; set; }
        public int? PurchaseOrderId { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal? SubTotalEur { get; set; }
        public decimal? FreightEur { get; set; }
        public decimal? TaxEur { get; set; }
        public decimal? TotalEur { get; set; }
        public decimal ConfidenceScore { get; set; } = 0.95m;
        public List<ExtractedLineItem> Lines { get; set; } = new();
    }

    public interface IVendorInvoiceVarianceService
    {
        /// <summary>
        /// Extracts invoice header and line items from a document file/stream.
        /// </summary>
        Task<ExtractionResult> ExtractDocumentDataAsync(Stream fileStream, string fileName);

        /// <summary>
        /// Evaluates unit cost variances for an invoice and its line items against PO lines and Part master costs.
        /// Updates database entities with discrepancy flags, status (Pending Review vs Approved), and notes.
        /// </summary>
        Task<VendorInvoiceEvaluationResult> EvaluateInvoiceVariancesAsync(int vendorInvoiceId);

        /// <summary>
        /// Evaluates unit cost variances in-memory for given invoice and line items.
        /// </summary>
        Task<VendorInvoiceEvaluationResult> EvaluateInvoiceVariancesAsync(VendorInvoice invoice, List<VendorInvoiceLine> lines);

        /// <summary>
        /// Processes file upload, extracts data, creates invoice & line items, evaluates variances, and saves to database.
        /// </summary>
        Task<VendorInvoice> ProcessUploadedInvoiceAsync(IFormFile file, int? vendorId = null, int? purchaseOrderId = null, string? uploadedBy = null);

        /// <summary>
        /// Completes human review for a pending vendor invoice.
        /// Updates status to Approved, records reviewer info and notes, updates Part master costs if requested,
        /// and enqueues the invoice to QBSyncQueue for accounting sync.
        /// </summary>
        Task ApproveReviewAsync(int vendorInvoiceId, int reviewedByUserId, string reviewNotes, bool updatePartMasterCosts = true);

        /// <summary>
        /// Checks whether the invoice is clear to sync to accounting (i.e. Approved and no unreviewed discrepancies).
        /// </summary>
        Task<bool> CanSyncToQuickBooksAsync(int vendorInvoiceId);
    }
}
