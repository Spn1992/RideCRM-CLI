using Microsoft.EntityFrameworkCore;

namespace AmusementRideCRM.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

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
    }
}
