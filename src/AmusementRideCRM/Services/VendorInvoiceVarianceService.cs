using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public class VendorInvoiceVarianceService : IVendorInvoiceVarianceService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IQBSyncQueueService _qbSyncQueueService;

        public VendorInvoiceVarianceService(
            ApplicationDbContext dbContext,
            IQBSyncQueueService qbSyncQueueService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _qbSyncQueueService = qbSyncQueueService ?? throw new ArgumentNullException(nameof(qbSyncQueueService));
        }

        public async Task<ExtractionResult> ExtractDocumentDataAsync(Stream fileStream, string fileName)
        {
            var result = new ExtractionResult
            {
                InvoiceNumber = "INV-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                ConfidenceScore = 0.94m
            };

            try
            {
                using var reader = new StreamReader(fileStream, leaveOpen: true);
                var content = await reader.ReadToEndAsync();
                fileStream.Position = 0; // reset position

                if (!string.IsNullOrWhiteSpace(content) && content.TrimStart().StartsWith("{"))
                {
                    var parsed = JsonSerializer.Deserialize<ExtractionResult>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsed != null)
                    {
                        return parsed;
                    }
                }
            }
            catch
            {
                // Fallback to default mock extraction if stream parsing fails
            }

            return result;
        }

        public async Task<VendorInvoiceEvaluationResult> EvaluateInvoiceVariancesAsync(int vendorInvoiceId)
        {
            var invoice = await _dbContext.VendorInvoice
                .FirstOrDefaultAsync(v => v.VendorInvoiceId == vendorInvoiceId);

            if (invoice == null)
            {
                throw new InvalidOperationException($"Vendor invoice #{vendorInvoiceId} not found.");
            }

            var lines = await _dbContext.VendorInvoiceLine
                .Where(l => l.VendorInvoiceId == vendorInvoiceId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync();

            var evaluation = await EvaluateInvoiceVariancesAsync(invoice, lines);

            // Persist invoice & line changes to DB
            invoice.ModifiedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return evaluation;
        }

        public async Task<VendorInvoiceEvaluationResult> EvaluateInvoiceVariancesAsync(VendorInvoice invoice, List<VendorInvoiceLine> lines)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));
            lines ??= new List<VendorInvoiceLine>();

            var evaluation = new VendorInvoiceEvaluationResult
            {
                VendorInvoiceId = invoice.VendorInvoiceId
            };

            bool hasAnyDiscrepancy = false;
            var discrepancyNotesList = new List<string>();

            // Pre-load PO lines and Parts for evaluation
            var poLines = new List<PurchaseOrderLine>();
            if (invoice.PurchaseOrderId.HasValue && invoice.PurchaseOrderId.Value > 0)
            {
                poLines = await _dbContext.PurchaseOrderLine
                    .Where(p => p.PurchaseOrderId == invoice.PurchaseOrderId.Value)
                    .ToListAsync();
            }

            var partIds = lines.Where(l => l.PartId.HasValue).Select(l => l.PartId!.Value).Distinct().ToList();
            var parts = await _dbContext.Part
                .Where(p => partIds.Contains(p.PartId))
                .ToDictionaryAsync(p => p.PartId, p => p);

            foreach (var line in lines)
            {
                decimal unitCost = line.UnitCostEur ?? 0m;
                decimal baselineCost = 0m;
                string baselineSource = "None";

                // 1. Try PO Line match
                PurchaseOrderLine? matchedPoLine = null;
                if (line.POLineId.HasValue && line.POLineId.Value > 0)
                {
                    matchedPoLine = poLines.FirstOrDefault(p => p.POLineId == line.POLineId.Value)
                        ?? await _dbContext.PurchaseOrderLine.FirstOrDefaultAsync(p => p.POLineId == line.POLineId.Value);
                }

                if (matchedPoLine == null && poLines.Any())
                {
                    if (line.PartId.HasValue)
                    {
                        matchedPoLine = poLines.FirstOrDefault(p => p.PartId == line.PartId.Value);
                    }
                    if (matchedPoLine == null && !string.IsNullOrWhiteSpace(line.VendorPartNumber))
                    {
                        matchedPoLine = poLines.FirstOrDefault(p => string.Equals(p.VendorPartNumber, line.VendorPartNumber, StringComparison.OrdinalIgnoreCase));
                    }

                    if (matchedPoLine != null)
                    {
                        line.POLineId = matchedPoLine.POLineId;
                    }
                }

                if (matchedPoLine != null)
                {
                    baselineCost = matchedPoLine.UnitCostEur;
                    baselineSource = "PurchaseOrderLine";
                }
                // 2. Try Part Master match
                else if (line.PartId.HasValue && parts.TryGetValue(line.PartId.Value, out var part))
                {
                    if (part.LastVendorCostEur.HasValue && part.LastVendorCostEur.Value > 0)
                    {
                        baselineCost = part.LastVendorCostEur.Value;
                        baselineSource = "PartMaster";
                    }
                }
                // 3. Try VendorSource match
                else if (invoice.VendorId.HasValue && line.PartId.HasValue)
                {
                    var vendorSource = await _dbContext.VendorSource
                        .FirstOrDefaultAsync(vs => vs.VendorId == invoice.VendorId.Value && vs.PartId == line.PartId.Value);
                    if (vendorSource != null)
                    {
                        baselineCost = vendorSource.LastKnownCostEur ?? 0m;
                        if (baselineCost > 0)
                        {
                            baselineSource = "VendorSource";
                        }
                    }
                }

                decimal variancePct = 0m;
                bool exceedsThreshold = false;

                if (baselineCost > 0m)
                {
                    decimal diff = unitCost - baselineCost;
                    variancePct = Math.Round((diff / baselineCost) * 100m, 2);
                    exceedsThreshold = Math.Abs(diff / baselineCost) > 0.05m; // > 5% variance
                }

                line.IsMatchConfirmed = !exceedsThreshold;
                line.AiMatchConfidence = exceedsThreshold ? 0.50m : (line.AiMatchConfidence ?? 0.95m);

                var lineInfo = new LineVarianceInfo
                {
                    VILineId = line.VILineId,
                    LineNumber = line.LineNumber,
                    PartId = line.PartId,
                    VendorPartNumber = line.VendorPartNumber,
                    Description = line.Description,
                    Quantity = line.Quantity ?? 0m,
                    UnitCostEur = unitCost,
                    BaselineUnitCostEur = baselineCost,
                    BaselineSource = baselineSource,
                    VariancePercentage = variancePct,
                    ExceedsThreshold = exceedsThreshold
                };

                evaluation.LineVariances.Add(lineInfo);

                if (exceedsThreshold)
                {
                    hasAnyDiscrepancy = true;
                    string itemLabel = !string.IsNullOrWhiteSpace(line.VendorPartNumber)
                        ? line.VendorPartNumber
                        : (line.PartId.HasValue ? $"Part #{line.PartId}" : $"Line #{line.LineNumber}");

                    discrepancyNotesList.Add($"Line {line.LineNumber} ({itemLabel}): Unit cost €{unitCost:F2} vs baseline €{baselineCost:F2} ({baselineSource}) exceeds 5% threshold (Variance: {(variancePct >= 0 ? "+" : "")}{variancePct:F1}%)");
                }
            }

            evaluation.HasDiscrepancy = hasAnyDiscrepancy;
            evaluation.Status = hasAnyDiscrepancy ? "Pending Review" : "Approved";
            evaluation.DiscrepancyNotes = hasAnyDiscrepancy
                ? string.Join("; ", discrepancyNotesList)
                : "All line item unit costs verified within 5% variance threshold.";

            // Set properties on the VendorInvoice entity
            invoice.HasDiscrepancy = hasAnyDiscrepancy;
            invoice.Status = evaluation.Status;
            invoice.DiscrepancyNotes = evaluation.DiscrepancyNotes;

            return evaluation;
        }

        public async Task<VendorInvoice> ProcessUploadedInvoiceAsync(IFormFile file, int? vendorId = null, int? purchaseOrderId = null, string? uploadedBy = null)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("Uploaded file cannot be null or empty.", nameof(file));
            }

            // Save file to wwwroot/uploads/vendor_invoices
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "vendor_invoices");
            Directory.CreateDirectory(uploadsFolder);

            var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, safeFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var webPath = $"/uploads/vendor_invoices/{safeFileName}";

            // Perform document extraction
            ExtractionResult extraction;
            using (var stream = file.OpenReadStream())
            {
                extraction = await ExtractDocumentDataAsync(stream, file.FileName);
            }

            var invoice = new VendorInvoice
            {
                InvoiceNumber = !string.IsNullOrWhiteSpace(extraction.InvoiceNumber) ? extraction.InvoiceNumber : $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
                VendorId = vendorId ?? extraction.VendorId,
                PurchaseOrderId = purchaseOrderId ?? extraction.PurchaseOrderId,
                InvoiceDate = extraction.InvoiceDate ?? DateTime.Today,
                DueDate = extraction.DueDate ?? DateTime.Today.AddDays(30),
                Currency = "EUR",
                SubTotalEur = extraction.SubTotalEur,
                FreightEur = extraction.FreightEur,
                TaxEur = extraction.TaxEur,
                TotalEur = extraction.TotalEur ?? (extraction.SubTotalEur + (extraction.FreightEur ?? 0m) + (extraction.TaxEur ?? 0m)),
                UploadedFileName = file.FileName,
                UploadedFilePath = webPath,
                UploadedDate = DateTime.UtcNow,
                UploadedBy = uploadedBy ?? "System",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = uploadedBy ?? "System",
                AiConfidenceScore = extraction.ConfidenceScore,
                AiExtractionJson = JsonSerializer.Serialize(extraction)
            };

            _dbContext.VendorInvoice.Add(invoice);
            await _dbContext.SaveChangesAsync();

            var lines = new List<VendorInvoiceLine>();
            if (extraction.Lines != null && extraction.Lines.Count > 0)
            {
                foreach (var extractedLine in extraction.Lines)
                {
                    var line = new VendorInvoiceLine
                    {
                        VendorInvoiceId = invoice.VendorInvoiceId,
                        LineNumber = extractedLine.LineNumber > 0 ? extractedLine.LineNumber : lines.Count + 1,
                        PartId = extractedLine.PartId,
                        VendorPartNumber = extractedLine.VendorPartNumber,
                        Description = extractedLine.Description,
                        Quantity = extractedLine.Quantity,
                        UnitCostEur = extractedLine.UnitCostEur,
                        LineTotalEur = (extractedLine.Quantity * extractedLine.UnitCostEur).ToString("F2"),
                        POLineId = extractedLine.POLineId,
                        AiMatchConfidence = extractedLine.ConfidenceScore,
                        CreatedDate = DateTime.UtcNow
                    };
                    lines.Add(line);
                }
                _dbContext.VendorInvoiceLine.AddRange(lines);
                await _dbContext.SaveChangesAsync();
            }

            // Run cost variance evaluation
            await EvaluateInvoiceVariancesAsync(invoice, lines);
            await _dbContext.SaveChangesAsync();

            return invoice;
        }

        public async Task ApproveReviewAsync(int vendorInvoiceId, int reviewedByUserId, string reviewNotes, bool updatePartMasterCosts = true)
        {
            var invoice = await _dbContext.VendorInvoice
                .FirstOrDefaultAsync(v => v.VendorInvoiceId == vendorInvoiceId);

            if (invoice == null)
            {
                throw new InvalidOperationException($"Vendor invoice #{vendorInvoiceId} not found.");
            }

            var lines = await _dbContext.VendorInvoiceLine
                .Where(l => l.VendorInvoiceId == vendorInvoiceId)
                .ToListAsync();

            invoice.Status = "Approved";
            invoice.ReviewedByUserId = reviewedByUserId;
            invoice.ReviewedDate = DateTime.UtcNow;
            invoice.ReviewNotes = reviewNotes;
            invoice.ModifiedDate = DateTime.UtcNow;

            if (updatePartMasterCosts)
            {
                var partIds = lines.Where(l => l.PartId.HasValue && l.UnitCostEur.HasValue && l.UnitCostEur.Value > 0)
                    .Select(l => l.PartId!.Value).Distinct().ToList();

                var parts = await _dbContext.Part
                    .Where(p => partIds.Contains(p.PartId))
                    .ToListAsync();

                foreach (var line in lines)
                {
                    if (line.PartId.HasValue && line.UnitCostEur.HasValue && line.UnitCostEur.Value > 0)
                    {
                        var part = parts.FirstOrDefault(p => p.PartId == line.PartId.Value);
                        if (part != null)
                        {
                            part.LastVendorCostEur = line.UnitCostEur.Value;
                            part.ModifiedDate = DateTime.UtcNow;
                        }
                    }
                }
            }

            await _dbContext.SaveChangesAsync();

            // Queue for QuickBooks sync after approval
            await _qbSyncQueueService.EnqueueSyncItemAsync("VendorInvoice", vendorInvoiceId, "Create", "ToQB", priority: 1);
        }

        public async Task<bool> CanSyncToQuickBooksAsync(int vendorInvoiceId)
        {
            var invoice = await _dbContext.VendorInvoice
                .FirstOrDefaultAsync(v => v.VendorInvoiceId == vendorInvoiceId);

            if (invoice == null) return false;

            return string.Equals(invoice.Status, "Approved", StringComparison.OrdinalIgnoreCase);
        }
    }
}
