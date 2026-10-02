using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public static class ReceivingWorkflow
    {
        public static async Task ProcessReceivingLineAsync(ApplicationDbContext context, ReceivingLine receivingLine)
        {
            if (receivingLine == null || receivingLine.QuantityReceived <= 0) return;

            // 1. Update Part Stock Levels
            var part = await context.Part.FirstOrDefaultAsync(p => p.PartId == receivingLine.PartId);
            if (part != null)
            {
                part.QuantityOnHand += receivingLine.QuantityReceived;
                part.AvailableQuantity = (part.QuantityOnHand - part.ReservedQuantity).ToString("F0");
                part.ModifiedDate = DateTime.UtcNow;
            }

            // 2. Update Purchase Order Line
            if (receivingLine.POLineId > 0)
            {
                var poLine = await context.PurchaseOrderLine.FirstOrDefaultAsync(l => l.POLineId == receivingLine.POLineId);
                if (poLine != null)
                {
                    poLine.QuantityReceived += receivingLine.QuantityReceived;
                    poLine.QuantityOutstanding = Math.Max(0, poLine.Quantity - poLine.QuantityReceived).ToString("F0");
                    poLine.ModifiedDate = DateTime.UtcNow;

                    if (part != null && poLine.UnitCostEur > 0)
                    {
                        part.LastVendorCostEur = poLine.UnitCostEur;
                    }

                    // 3. Update Purchase Order Status
                    var po = await context.PurchaseOrder.FirstOrDefaultAsync(p => p.PurchaseOrderId == poLine.PurchaseOrderId);
                    if (po != null)
                    {
                        var allPoLines = await context.PurchaseOrderLine.Where(l => l.PurchaseOrderId == po.PurchaseOrderId).ToListAsync();
                        if (allPoLines.All(l => l.QuantityReceived >= l.Quantity))
                        {
                            po.Status = "Received";
                        }
                        else if (allPoLines.Any(l => l.QuantityReceived > 0))
                        {
                            po.Status = "Partially Received";
                        }
                        po.ModifiedDate = DateTime.UtcNow;
                    }
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
