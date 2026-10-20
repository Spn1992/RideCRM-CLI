using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Models;

public class ApplicationDbContext : DbContext
{
    private readonly IAuditUserProvider _auditUserProvider;
    private bool _isAuditing = false;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
        _auditUserProvider = new AuditUserProvider();
    }

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IAuditUserProvider auditUserProvider) : base(options)
    {
        _auditUserProvider = auditUserProvider ?? new AuditUserProvider();
    }

    public DbSet<UserMaster> UserMaster { get; set; }
    public DbSet<ErrorLogging> ErrorLogging { get; set; }
    public DbSet<AuditLog> AuditLog { get; set; }
    public DbSet<SystemConfiguration> SystemConfiguration { get; set; }
    public DbSet<VendorMaster> VendorMaster { get; set; }
    public DbSet<Customer> Customer { get; set; }
    public DbSet<CustomerContact> CustomerContact { get; set; }
    public DbSet<CustomerRide> CustomerRide { get; set; }
    public DbSet<WarehouseLocation> WarehouseLocation { get; set; }
    public DbSet<HoldingLocation> HoldingLocation { get; set; }
    public DbSet<Part> Part { get; set; }
    public DbSet<VendorSource> VendorSource { get; set; }
    public DbSet<PartRideApplication> PartRideApplication { get; set; }
    public DbSet<CustomerOrder> CustomerOrder { get; set; }
    public DbSet<CustomerOrderLine> CustomerOrderLine { get; set; }
    public DbSet<InventoryReservation> InventoryReservation { get; set; }
    public DbSet<PurchasingQueueItem> PurchasingQueueItem { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrder { get; set; }
    public DbSet<PurchaseOrderLine> PurchaseOrderLine { get; set; }
    public DbSet<VendorInvoice> VendorInvoice { get; set; }
    public DbSet<VendorInvoiceLine> VendorInvoiceLine { get; set; }
    public DbSet<VendorPartCrossReference> VendorPartCrossReference { get; set; }
    public DbSet<ReceivingRecord> ReceivingRecord { get; set; }
    public DbSet<ReceivingLine> ReceivingLine { get; set; }
    public DbSet<Shipment> Shipment { get; set; }
    public DbSet<ShipmentLine> ShipmentLine { get; set; }
    public DbSet<ShipmentPackage> ShipmentPackage { get; set; }
    public DbSet<CustomerInvoice> CustomerInvoice { get; set; }
    public DbSet<CustomerInvoiceLine> CustomerInvoiceLine { get; set; }
    public DbSet<QBSyncQueue> QBSyncQueue { get; set; }
    public DbSet<PartMergeHistory> PartMergeHistory { get; set; }
    public DbSet<NotificationQueue> NotificationQueue { get; set; }
    public DbSet<FileAttachment> FileAttachment { get; set; }
    public DbSet<NumberSequence> NumberSequence { get; set; }
    public DbSet<SerializedPartUnit> SerializedPartUnit { get; set; }
    public DbSet<ZplPrintJob> ZplPrintJob { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<UserMaster>().HasKey(e => e.UserId);
        modelBuilder.Entity<ErrorLogging>().HasKey(e => e.ErrorLogId);
        modelBuilder.Entity<AuditLog>().HasKey(e => e.AuditLogId);
        modelBuilder.Entity<SystemConfiguration>().HasKey(e => e.ConfigId);
        modelBuilder.Entity<VendorMaster>().HasKey(e => e.VendorId);
        modelBuilder.Entity<Customer>().HasKey(e => e.CustomerId);
        modelBuilder.Entity<CustomerContact>().HasKey(e => e.ContactId);
        modelBuilder.Entity<CustomerRide>().HasKey(e => e.RideId);
        modelBuilder.Entity<WarehouseLocation>().HasKey(e => e.LocationId);
        modelBuilder.Entity<HoldingLocation>().HasKey(e => e.HoldingLocationId);
        modelBuilder.Entity<Part>().HasKey(e => e.PartId);
        modelBuilder.Entity<VendorSource>().HasKey(e => e.VendorSourceId);
        modelBuilder.Entity<PartRideApplication>().HasKey(e => e.PartRideAppId);
        modelBuilder.Entity<CustomerOrder>().HasKey(e => e.OrderId);
        modelBuilder.Entity<CustomerOrderLine>().HasKey(e => e.OrderLineId);
        modelBuilder.Entity<InventoryReservation>().HasKey(e => e.ReservationId);
        modelBuilder.Entity<PurchasingQueueItem>().HasKey(e => e.QueueItemId);
        modelBuilder.Entity<PurchaseOrder>().HasKey(e => e.PurchaseOrderId);
        modelBuilder.Entity<PurchaseOrderLine>().HasKey(e => e.POLineId);
        modelBuilder.Entity<VendorInvoice>().HasKey(e => e.VendorInvoiceId);
        modelBuilder.Entity<VendorInvoiceLine>().HasKey(e => e.VILineId);
        modelBuilder.Entity<VendorPartCrossReference>().HasKey(e => e.CrossRefId);
        modelBuilder.Entity<ReceivingRecord>().HasKey(e => e.ReceivingId);
        modelBuilder.Entity<ReceivingLine>().HasKey(e => e.ReceivingLineId);
        modelBuilder.Entity<Shipment>().HasKey(e => e.ShipmentId);
        modelBuilder.Entity<ShipmentLine>().HasKey(e => e.ShipmentLineId);
        modelBuilder.Entity<ShipmentPackage>().HasKey(e => e.PackageId);
        modelBuilder.Entity<CustomerInvoice>().HasKey(e => e.InvoiceId);
        modelBuilder.Entity<CustomerInvoiceLine>().HasKey(e => e.InvoiceLineId);
        modelBuilder.Entity<QBSyncQueue>().HasKey(e => e.SyncId);
        modelBuilder.Entity<PartMergeHistory>().HasKey(e => e.MergeId);
        modelBuilder.Entity<NotificationQueue>().HasKey(e => e.NotificationId);
        modelBuilder.Entity<FileAttachment>().HasKey(e => e.AttachmentId);
        modelBuilder.Entity<NumberSequence>().HasNoKey();
        modelBuilder.Entity<SerializedPartUnit>().HasKey(e => e.SerializedPartUnitId);
        modelBuilder.Entity<SerializedPartUnit>()
            .HasIndex(u => new { u.PartId, u.SerialNumber })
            .IsUnique();
        modelBuilder.Entity<SerializedPartUnit>()
            .HasOne(u => u.Part)
            .WithMany()
            .HasForeignKey(u => u.PartId);
        modelBuilder.Entity<SerializedPartUnit>()
            .HasOne(u => u.CustomerRide)
            .WithMany()
            .HasForeignKey(u => u.RideId);
        modelBuilder.Entity<SerializedPartUnit>()
            .HasOne(u => u.ReceivingRecord)
            .WithMany()
            .HasForeignKey(u => u.ReceivingId);
        modelBuilder.Entity<SerializedPartUnit>()
            .HasOne(u => u.Shipment)
            .WithMany()
            .HasForeignKey(u => u.ShipmentId);

        modelBuilder.Entity<ZplPrintJob>().HasKey(e => e.PrintJobId);
        modelBuilder.Entity<ZplPrintJob>()
            .HasOne(j => j.Part)
            .WithMany()
            .HasForeignKey(j => j.PartId);
        modelBuilder.Entity<ZplPrintJob>()
            .HasOne(j => j.SerializedPartUnit)
            .WithMany()
            .HasForeignKey(j => j.SerializedPartUnitId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (_isAuditing)
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        try
        {
            _isAuditing = true;
            var (preAuditLogs, pendingAddedAudits) = PrepareAuditLogs();
            if (preAuditLogs.Count > 0)
            {
                AuditLog.AddRange(preAuditLogs);
            }

            int result = base.SaveChanges(acceptAllChangesOnSuccess);

            if (pendingAddedAudits.Count > 0)
            {
                var postAuditLogs = FinalizeAddedAuditLogs(pendingAddedAudits);
                if (postAuditLogs.Count > 0)
                {
                    AuditLog.AddRange(postAuditLogs);
                    base.SaveChanges(acceptAllChangesOnSuccess);
                }
            }

            return result;
        }
        finally
        {
            _isAuditing = false;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (_isAuditing)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        try
        {
            _isAuditing = true;
            var (preAuditLogs, pendingAddedAudits) = PrepareAuditLogs();
            if (preAuditLogs.Count > 0)
            {
                AuditLog.AddRange(preAuditLogs);
            }

            int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            if (pendingAddedAudits.Count > 0)
            {
                var postAuditLogs = FinalizeAddedAuditLogs(pendingAddedAudits);
                if (postAuditLogs.Count > 0)
                {
                    AuditLog.AddRange(postAuditLogs);
                    await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                }
            }

            return result;
        }
        finally
        {
            _isAuditing = false;
        }
    }

    private struct PendingAudit
    {
        public EntityEntry Entry { get; set; }
        public string EntityName { get; set; }
        public string PropertyName { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string ActionType { get; set; }
        public string? Description { get; set; }
        public string? RelatedEntity { get; set; }
        public string? RelatedEntityId { get; set; }
    }

    private (List<AuditLog> ImmediateLogs, List<PendingAudit> PendingAdded) PrepareAuditLogs()
    {
        var immediateLogs = new List<AuditLog>();
        var pendingAdded = new List<PendingAudit>();

        var entries = ChangeTracker.Entries()
            .Where(e => !(e.Entity is AuditLog) && !(e.Entity is ErrorLogging))
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        if (entries.Count == 0)
        {
            return (immediateLogs, pendingAdded);
        }

        int? userId = _auditUserProvider.GetUserId();
        string userName = _auditUserProvider.GetUserName();
        string? ipAddress = _auditUserProvider.GetIpAddress();
        Guid correlationId = _auditUserProvider.GetCorrelationId();
        DateTime auditDate = DateTime.UtcNow;

        var poLineEntry = entries.FirstOrDefault(e => e.Entity is PurchaseOrderLine);
        var receivingLineEntry = entries.FirstOrDefault(e => e.Entity is ReceivingLine);
        var qbSyncEntry = entries.FirstOrDefault(e => e.Entity is QBSyncQueue);

        foreach (var entry in entries)
        {
            string entityName = entry.Metadata.ClrType.Name;
            string entityId = GetPrimaryKeyString(entry);

            string? defaultRelatedEntity = null;
            string? defaultRelatedEntityId = null;
            string? defaultDescription = null;

            if (entry.Entity is Part)
            {
                if (receivingLineEntry?.Entity is ReceivingLine rl)
                {
                    defaultRelatedEntity = "ReceivingLine";
                    defaultRelatedEntityId = rl.ReceivingLineId > 0 ? rl.ReceivingLineId.ToString() : rl.POLineId.ToString();
                    defaultDescription = $"Part stock updated via receiving line #{rl.ReceivingLineId} (PO Line #{rl.POLineId})";
                }
                else if (poLineEntry?.Entity is PurchaseOrderLine pol)
                {
                    defaultRelatedEntity = "PurchaseOrderLine";
                    defaultRelatedEntityId = pol.POLineId.ToString();
                    defaultDescription = $"Part stock updated via purchase order line #{pol.POLineId}";
                }
            }
            else if (entry.Entity is ReceivingLine rl)
            {
                defaultRelatedEntity = "PurchaseOrderLine";
                defaultRelatedEntityId = rl.POLineId.ToString();
                defaultDescription = $"Receiving line for PO Line #{rl.POLineId}";
            }
            else if (entry.Entity is QBSyncQueue qb)
            {
                defaultRelatedEntity = qb.EntityType;
                defaultRelatedEntityId = qb.EntityId.ToString();
                defaultDescription = $"QB Sync queue edit for {qb.EntityType} #{qb.EntityId} (Action: {qb.SyncAction}, Status: {qb.Status})";
            }
            else if (entry.Entity is PurchaseOrderLine pol)
            {
                defaultRelatedEntity = "PurchaseOrder";
                defaultRelatedEntityId = pol.PurchaseOrderId.ToString();
                defaultDescription = $"PO line #{pol.POLineId} for PurchaseOrder #{pol.PurchaseOrderId}";
            }

            if (entry.State == EntityState.Modified)
            {
                foreach (var prop in entry.Properties)
                {
                    if (!prop.IsModified || IsSensitiveProperty(prop.Metadata.Name))
                        continue;

                    string? oldVal = prop.OriginalValue?.ToString();
                    string? newVal = prop.CurrentValue?.ToString();

                    if (oldVal == newVal)
                        continue;

                    immediateLogs.Add(new AuditLog
                    {
                        AuditLogId = Guid.NewGuid().ToString(),
                        AuditDate = auditDate,
                        UserId = userId,
                        UserName = userName,
                        ActionType = "Modified",
                        EntityName = entityName,
                        EntityId = entityId,
                        PropertyName = prop.Metadata.Name,
                        OldValue = oldVal,
                        NewValue = newVal,
                        Description = defaultDescription ?? $"Updated {prop.Metadata.Name} on {entityName} #{entityId}",
                        RelatedEntity = defaultRelatedEntity,
                        RelatedEntityId = defaultRelatedEntityId,
                        IpAddress = ipAddress,
                        CorrelationId = correlationId
                    });
                }
            }
            else if (entry.State == EntityState.Added)
            {
                bool hasValidKey = !string.IsNullOrEmpty(entityId) && entityId != "0";

                foreach (var prop in entry.Properties)
                {
                    if (IsSensitiveProperty(prop.Metadata.Name))
                        continue;

                    string? newVal = prop.CurrentValue?.ToString();
                    if (newVal == null)
                        continue;

                    var item = new PendingAudit
                    {
                        Entry = entry,
                        EntityName = entityName,
                        PropertyName = prop.Metadata.Name,
                        OldValue = null,
                        NewValue = newVal,
                        ActionType = "Added",
                        Description = defaultDescription ?? $"Created {entityName} with {prop.Metadata.Name} = {newVal}",
                        RelatedEntity = defaultRelatedEntity,
                        RelatedEntityId = defaultRelatedEntityId
                    };

                    if (hasValidKey)
                    {
                        immediateLogs.Add(new AuditLog
                        {
                            AuditLogId = Guid.NewGuid().ToString(),
                            AuditDate = auditDate,
                            UserId = userId,
                            UserName = userName,
                            ActionType = "Added",
                            EntityName = entityName,
                            EntityId = entityId,
                            PropertyName = prop.Metadata.Name,
                            OldValue = null,
                            NewValue = newVal,
                            Description = item.Description,
                            RelatedEntity = defaultRelatedEntity,
                            RelatedEntityId = defaultRelatedEntityId,
                            IpAddress = ipAddress,
                            CorrelationId = correlationId
                        });
                    }
                    else
                    {
                        pendingAdded.Add(item);
                    }
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                foreach (var prop in entry.Properties)
                {
                    if (IsSensitiveProperty(prop.Metadata.Name))
                        continue;

                    string? oldVal = prop.OriginalValue?.ToString();

                    immediateLogs.Add(new AuditLog
                    {
                        AuditLogId = Guid.NewGuid().ToString(),
                        AuditDate = auditDate,
                        UserId = userId,
                        UserName = userName,
                        ActionType = "Deleted",
                        EntityName = entityName,
                        EntityId = entityId,
                        PropertyName = prop.Metadata.Name,
                        OldValue = oldVal,
                        NewValue = null,
                        Description = defaultDescription ?? $"Deleted {entityName} #{entityId}",
                        RelatedEntity = defaultRelatedEntity,
                        RelatedEntityId = defaultRelatedEntityId,
                        IpAddress = ipAddress,
                        CorrelationId = correlationId
                    });
                }
            }
        }

        return (immediateLogs, pendingAdded);
    }

    private List<AuditLog> FinalizeAddedAuditLogs(List<PendingAudit> pendingAdded)
    {
        var logs = new List<AuditLog>();
        int? userId = _auditUserProvider.GetUserId();
        string userName = _auditUserProvider.GetUserName();
        string? ipAddress = _auditUserProvider.GetIpAddress();
        Guid correlationId = _auditUserProvider.GetCorrelationId();
        DateTime auditDate = DateTime.UtcNow;

        foreach (var pending in pendingAdded)
        {
            string entityId = GetPrimaryKeyString(pending.Entry);
            string? relatedId = pending.RelatedEntityId;
            if (pending.Entry.Entity is ReceivingLine rl && pending.RelatedEntity == "PurchaseOrderLine")
            {
                relatedId = rl.POLineId.ToString();
            }

            logs.Add(new AuditLog
            {
                AuditLogId = Guid.NewGuid().ToString(),
                AuditDate = auditDate,
                UserId = userId,
                UserName = userName,
                ActionType = "Added",
                EntityName = pending.EntityName,
                EntityId = entityId,
                PropertyName = pending.PropertyName,
                OldValue = pending.OldValue,
                NewValue = pending.NewValue,
                Description = pending.Description ?? $"Created {pending.EntityName} #{entityId}",
                RelatedEntity = pending.RelatedEntity,
                RelatedEntityId = relatedId,
                IpAddress = ipAddress,
                CorrelationId = correlationId
            });
        }

        return logs;
    }

    private static bool IsSensitiveProperty(string propertyName)
    {
        if (string.Equals(propertyName, "RowVersion", StringComparison.OrdinalIgnoreCase)) return true;
        if (propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)) return true;
        if (propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase)) return true;
        if (propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase)) return true;
        if (propertyName.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)) return true;
        if (propertyName.Contains("CreditCard", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string GetPrimaryKeyString(EntityEntry entry)
    {
        var keyProps = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProps == null || !keyProps.Any())
            return "0";

        var vals = keyProps
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "0")
            .ToArray();

        return string.Join(",", vals);
    }
}
