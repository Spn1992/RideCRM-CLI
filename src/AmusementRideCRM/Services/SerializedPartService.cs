#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services;

public static class SerializedPartService
{
    public static async Task ValidateSerialUniquenessAsync(ApplicationDbContext context, int partId, IEnumerable<string> serialNumbers)
    {
        var serialsList = serialNumbers
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        // Check for duplicate serials within input list
        var duplicateInInput = serialsList
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateInInput != null)
        {
            throw new InvalidOperationException($"Duplicate serial number '{duplicateInInput.Key}' provided in input list.");
        }

        // Check against database
        var existing = await context.SerializedPartUnit
            .Where(u => u.PartId == partId && serialsList.Contains(u.SerialNumber))
            .Select(u => u.SerialNumber)
            .ToListAsync();

        if (existing.Any())
        {
            throw new InvalidOperationException($"Serial number(s) '{string.Join(", ", existing)}' already exist for part ID {partId}.");
        }
    }

    public static async Task<List<SerializedPartUnit>> ReceiveSerializedUnitsAsync(
        ApplicationDbContext context,
        int partId,
        int receivingId,
        int receivingLineId,
        List<string> serialNumbers,
        string createdBy)
    {
        if (serialNumbers == null || !serialNumbers.Any())
        {
            return new List<SerializedPartUnit>();
        }

        var part = await context.Part.FirstOrDefaultAsync(p => p.PartId == partId);
        if (part == null)
        {
            throw new InvalidOperationException($"Part ID {partId} not found.");
        }

        await ValidateSerialUniquenessAsync(context, partId, serialNumbers);

        var createdUnits = new List<SerializedPartUnit>();
        var now = DateTime.UtcNow;

        foreach (var rawSerial in serialNumbers)
        {
            var serial = rawSerial.Trim();
            if (string.IsNullOrEmpty(serial)) continue;

            var unit = new SerializedPartUnit
            {
                PartId = partId,
                SerialNumber = serial,
                Status = "InStock",
                ReceivingId = receivingId > 0 ? receivingId : null,
                ReceivingLineId = receivingLineId > 0 ? receivingLineId : null,
                ReceivedDate = now,
                CreatedDate = now,
                CreatedBy = createdBy
            };

            context.SerializedPartUnit.Add(unit);
            await context.SaveChangesAsync(); // Save to get unit ID for ZPL job

            createdUnits.Add(unit);

            // Generate & Queue ZPL Print Job without blocking workflow
            var zpl = ZplService.GenerateZpl(part.PartNumber, part.Description, unit.SerialNumber);
            await ZplService.QueueZplPrintJobAsync(
                context,
                partId,
                unit.SerializedPartUnitId,
                receivingId,
                zpl);
        }

        return createdUnits;
    }

    public static async Task<List<SerializedPartUnit>> ShipSerializedUnitsAsync(
        ApplicationDbContext context,
        int shipmentLineId,
        int shipmentId,
        int rideId,
        List<int> unitIds,
        string modifiedBy)
    {
        var shipmentLine = await context.ShipmentLine.FirstOrDefaultAsync(s => s.ShipmentLineId == shipmentLineId);
        if (shipmentLine == null)
        {
            throw new InvalidOperationException($"ShipmentLine ID {shipmentLineId} not found.");
        }

        var part = await context.Part.FirstOrDefaultAsync(p => p.PartId == shipmentLine.PartId);
        if (part == null)
        {
            throw new InvalidOperationException($"Part ID {shipmentLine.PartId} not found.");
        }

        var units = await context.SerializedPartUnit
            .Where(u => unitIds.Contains(u.SerializedPartUnitId))
            .ToListAsync();

        if (units.Count != unitIds.Count)
        {
            throw new InvalidOperationException("One or more selected serial units could not be found.");
        }

        var now = DateTime.UtcNow;
        var warrantyEndDate = now.AddMonths(part.WarrantyDurationMonths);

        foreach (var unit in units)
        {
            if (unit.PartId != part.PartId)
            {
                throw new InvalidOperationException($"Unit serial {unit.SerialNumber} does not match part ID {part.PartId}.");
            }

            if (unit.Status != "InStock" && unit.Status != "Received")
            {
                throw new InvalidOperationException($"Unit serial {unit.SerialNumber} is not available for shipping (Current Status: {unit.Status}).");
            }

            unit.Status = "Installed";
            unit.ShipmentLineId = shipmentLineId;
            unit.ShipmentId = shipmentId;
            unit.RideId = rideId > 0 ? rideId : null;
            unit.ShippedDate = now;
            unit.WarrantyStartDate = now;
            unit.WarrantyEndDate = warrantyEndDate;
            unit.ModifiedDate = now;
            unit.ModifiedBy = modifiedBy;
        }

        await context.SaveChangesAsync();
        return units;
    }

    public static async Task<SerializedPartUnit> TransitionUnitStatusAsync(
        ApplicationDbContext context,
        int unitId,
        string newStatus,
        string? notes,
        string modifiedBy)
    {
        var unit = await context.SerializedPartUnit
            .Include(u => u.Part)
            .FirstOrDefaultAsync(u => u.SerializedPartUnitId == unitId);

        if (unit == null)
        {
            throw new InvalidOperationException($"SerializedPartUnit ID {unitId} not found.");
        }

        // Allowed statuses: InStock, Installed, Returned, WarrantyClaim, Scrapped
        var validStatuses = new[] { "InStock", "Received", "Installed", "Returned", "WarrantyClaim", "Scrapped" };
        if (!validStatuses.Contains(newStatus))
        {
            throw new ArgumentException($"Invalid unit status '{newStatus}'.");
        }

        unit.Status = newStatus;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            unit.Notes = string.IsNullOrWhiteSpace(unit.Notes) ? notes : $"{unit.Notes} | {notes}";
        }
        unit.ModifiedDate = DateTime.UtcNow;
        unit.ModifiedBy = modifiedBy;

        await context.SaveChangesAsync();
        return unit;
    }

    public static async Task<List<SerializedPartUnit>> GetRideComponentHistoryAsync(
        ApplicationDbContext context,
        int rideId)
    {
        return await context.SerializedPartUnit
            .Include(u => u.Part)
            .Include(u => u.CustomerRide)
            .Where(u => u.RideId == rideId)
            .OrderByDescending(u => u.ShippedDate)
            .ToListAsync();
    }

    public static async Task<List<SerializedPartUnit>> SearchRecallAndWarrantyAsync(
        ApplicationDbContext context,
        string? partNumber,
        string? serialSearch,
        DateTime? warrantyStartFrom,
        DateTime? warrantyStartTo,
        bool? activeWarrantyOnly)
    {
        var query = context.SerializedPartUnit
            .Include(u => u.Part)
            .Include(u => u.CustomerRide)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(partNumber))
        {
            var pNum = partNumber.Trim();
            query = query.Where(u => u.Part != null && u.Part.PartNumber.Contains(pNum));
        }

        if (!string.IsNullOrWhiteSpace(serialSearch))
        {
            var sNum = serialSearch.Trim();
            query = query.Where(u => u.SerialNumber.Contains(sNum));
        }

        if (warrantyStartFrom.HasValue)
        {
            query = query.Where(u => u.WarrantyStartDate >= warrantyStartFrom.Value);
        }

        if (warrantyStartTo.HasValue)
        {
            query = query.Where(u => u.WarrantyStartDate <= warrantyStartTo.Value);
        }

        if (activeWarrantyOnly.HasValue && activeWarrantyOnly.Value)
        {
            var now = DateTime.UtcNow;
            query = query.Where(u => u.WarrantyEndDate.HasValue && u.WarrantyEndDate >= now);
        }

        return await query
            .OrderByDescending(u => u.CreatedDate)
            .ToListAsync();
    }
}
