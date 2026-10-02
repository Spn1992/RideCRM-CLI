/*
===============================================================================
  AMUSEMENT RIDE PARTS CRM & INVENTORY SYSTEM
  SQL Server Database Creation Script
  ─────────────────────────────────────────────────────────────────────────────
  Target:   SQL Server 2019+ / Azure SQL
  Stack:    ASP.NET Core 8.0 Razor Pages  ·  EF Core 8.0
  Author:   Auto-generated from Scope of Work analysis
  Date:     2026-09-20
  ─────────────────────────────────────────────────────────────────────────────
  Tables included:
    Infrastructure  – UserMaster, ErrorLogging, AuditLog, SystemConfiguration
    Domain          – Customer, CustomerContact, CustomerRide, Part,
                      VendorMaster, VendorSource, PartRideApplication,
                      CustomerQuote/Order, OrderLine, InventoryReservation,
                      PurchasingQueueItem, PurchaseOrder, PurchaseOrderLine,
                      VendorInvoice, VendorInvoiceLine, VendorPartCrossReference,
                      ReceivingRecord, ReceivingLine, Shipment, ShipmentLine,
                      ShipmentPackage, CustomerInvoice, CustomerInvoiceLine,
                      QBSyncQueue, WarehouseLocation, HoldingLocation
===============================================================================
*/

-- ============================================================================
-- 0.  DATABASE CREATION  (uncomment & customise if needed)
-- ============================================================================
/*
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'AmusementRideCRM')
BEGIN
    CREATE DATABASE [AmusementRideCRM]
    COLLATE SQL_Latin1_General_CP1_CI_AS;
END
GO
USE [AmusementRideCRM];
GO
*/

-- ============================================================================
-- 1.  INFRASTRUCTURE TABLES
-- ============================================================================

-- ──────────────────────────────────────────────
-- 1.1  UserMaster
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.UserMaster', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserMaster
    (
        UserId              INT             IDENTITY(1,1)   NOT NULL,
        UserName            NVARCHAR(100)   NOT NULL,
        NormalizedUserName  NVARCHAR(100)   NOT NULL,
        Email               NVARCHAR(256)   NOT NULL,
        NormalizedEmail     NVARCHAR(256)   NOT NULL,
        PasswordHash        NVARCHAR(MAX)   NULL,
        FirstName           NVARCHAR(100)   NOT NULL,
        LastName            NVARCHAR(100)   NOT NULL,
        DisplayName     AS  (FirstName + N' ' + LastName) PERSISTED,
        PhoneNumber         NVARCHAR(30)    NULL,
        RoleCode            NVARCHAR(50)    NOT NULL
                            CONSTRAINT DF_UserMaster_RoleCode DEFAULT N'Office',
        -- RoleCode values: Admin, Office, Purchasing, Warehouse, Accounting
        IsActive            BIT             NOT NULL
                            CONSTRAINT DF_UserMaster_IsActive DEFAULT 1,
        IsLocked            BIT             NOT NULL
                            CONSTRAINT DF_UserMaster_IsLocked DEFAULT 0,
        LockoutEnd          DATETIMEOFFSET  NULL,
        FailedLoginAttempts INT             NOT NULL
                            CONSTRAINT DF_UserMaster_FailedLogin DEFAULT 0,
        LastLoginDate       DATETIME2(2)    NULL,
        PasswordChangedDate DATETIME2(2)    NULL,
        RefreshToken        NVARCHAR(512)   NULL,
        RefreshTokenExpiry  DATETIME2(2)    NULL,
        ProfileImageUrl     NVARCHAR(500)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_UserMaster_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_UserMaster PRIMARY KEY CLUSTERED (UserId),
        CONSTRAINT UQ_UserMaster_UserName UNIQUE (NormalizedUserName),
        CONSTRAINT UQ_UserMaster_Email UNIQUE (NormalizedEmail),
        CONSTRAINT CK_UserMaster_RoleCode CHECK (
            RoleCode IN (N'Admin', N'Office', N'Purchasing', N'Warehouse', N'Accounting')
        )
    );
END;
GO

-- ──────────────────────────────────────────────
-- 1.2  ErrorLogging
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.ErrorLogging', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ErrorLogging
    (
        ErrorLogId      BIGINT          IDENTITY(1,1)   NOT NULL,
        ErrorDate       DATETIME2(3)    NOT NULL
                        CONSTRAINT DF_ErrorLogging_Date DEFAULT SYSUTCDATETIME(),
        Severity        NVARCHAR(20)    NOT NULL
                        CONSTRAINT DF_ErrorLogging_Severity DEFAULT N'Error',
        -- Severity values: Trace, Debug, Information, Warning, Error, Critical
        Source          NVARCHAR(500)   NULL,       -- Controller/Service/Method
        MachineName     NVARCHAR(100)   NULL,
        UserName        NVARCHAR(100)   NULL,
        RequestPath     NVARCHAR(2000)  NULL,
        RequestMethod   NVARCHAR(10)    NULL,
        QueryString     NVARCHAR(2000)  NULL,
        StatusCode      INT             NULL,
        ErrorMessage    NVARCHAR(MAX)   NOT NULL,
        StackTrace      NVARCHAR(MAX)   NULL,
        InnerException  NVARCHAR(MAX)   NULL,
        AdditionalData  NVARCHAR(MAX)   NULL,       -- JSON payload for extra context
        CorrelationId   UNIQUEIDENTIFIER NULL,

        CONSTRAINT PK_ErrorLogging PRIMARY KEY CLUSTERED (ErrorLogId)
    );
END;
GO

-- Partition-friendly index on ErrorDate for log cleanup
CREATE NONCLUSTERED INDEX IX_ErrorLogging_Date_Severity
    ON dbo.ErrorLogging (ErrorDate DESC, Severity)
    INCLUDE (Source, ErrorMessage);
GO

-- ──────────────────────────────────────────────
-- 1.3  AuditLog  (Section 23 – Transaction History)
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog
    (
        AuditLogId      BIGINT          IDENTITY(1,1)   NOT NULL,
        AuditDate       DATETIME2(3)    NOT NULL
                        CONSTRAINT DF_AuditLog_Date DEFAULT SYSUTCDATETIME(),
        UserId          INT             NULL,
        UserName        NVARCHAR(100)   NULL,
        ActionType      NVARCHAR(50)    NOT NULL,
        -- ActionType: Create, Update, Delete, StatusChange, Merge, Sync, Receive, Ship, Reserve, PriceChange
        EntityName      NVARCHAR(128)   NOT NULL,
        EntityId        NVARCHAR(50)    NOT NULL,
        PropertyName    NVARCHAR(128)   NULL,
        OldValue        NVARCHAR(MAX)   NULL,
        NewValue        NVARCHAR(MAX)   NULL,
        Description     NVARCHAR(1000)  NULL,
        RelatedEntity   NVARCHAR(128)   NULL,
        RelatedEntityId NVARCHAR(50)    NULL,
        IpAddress       NVARCHAR(50)    NULL,
        CorrelationId   UNIQUEIDENTIFIER NULL,

        CONSTRAINT PK_AuditLog PRIMARY KEY CLUSTERED (AuditLogId)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_AuditLog_Entity
    ON dbo.AuditLog (EntityName, EntityId, AuditDate DESC);
GO

CREATE NONCLUSTERED INDEX IX_AuditLog_User_Date
    ON dbo.AuditLog (UserId, AuditDate DESC)
    INCLUDE (ActionType, EntityName);
GO

-- ──────────────────────────────────────────────
-- 1.4  SystemConfiguration  (Section 5 – Pricing)
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.SystemConfiguration', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemConfiguration
    (
        ConfigId        INT             IDENTITY(1,1)   NOT NULL,
        ConfigKey       NVARCHAR(100)   NOT NULL,
        ConfigValue     NVARCHAR(500)   NOT NULL,
        DataType        NVARCHAR(20)    NOT NULL
                        CONSTRAINT DF_SysConfig_DataType DEFAULT N'String',
        Description     NVARCHAR(500)   NULL,
        Category        NVARCHAR(100)   NULL,
        IsEditable      BIT             NOT NULL
                        CONSTRAINT DF_SysConfig_Editable DEFAULT 1,
        ModifiedDate    DATETIME2(2)    NULL,
        ModifiedBy      NVARCHAR(100)   NULL,
        RowVersion      ROWVERSION      NOT NULL,

        CONSTRAINT PK_SystemConfiguration PRIMARY KEY CLUSTERED (ConfigId),
        CONSTRAINT UQ_SystemConfiguration_Key UNIQUE (ConfigKey)
    );
END;
GO

-- ============================================================================
-- 2.  VENDOR MASTER
-- ============================================================================
IF OBJECT_ID(N'dbo.VendorMaster', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VendorMaster
    (
        VendorId        INT             IDENTITY(1,1)   NOT NULL,
        VendorName      NVARCHAR(200)   NOT NULL,
        VendorCode      NVARCHAR(50)    NULL,
        ContactName     NVARCHAR(150)   NULL,
        Email           NVARCHAR(256)   NULL,
        Phone           NVARCHAR(30)    NULL,
        Fax             NVARCHAR(30)    NULL,
        Address1        NVARCHAR(200)   NULL,
        Address2        NVARCHAR(200)   NULL,
        City            NVARCHAR(100)   NULL,
        StateProvince   NVARCHAR(100)   NULL,
        PostalCode      NVARCHAR(20)    NULL,
        Country         NVARCHAR(100)   NULL,
        Currency        NVARCHAR(3)     NOT NULL
                        CONSTRAINT DF_VendorMaster_Currency DEFAULT N'EUR',
        PaymentTerms    NVARCHAR(100)   NULL,
        Website         NVARCHAR(500)   NULL,
        Notes           NVARCHAR(MAX)   NULL,
        QBVendorListID  NVARCHAR(50)    NULL,       -- QuickBooks Desktop ListID
        IsActive        BIT             NOT NULL
                        CONSTRAINT DF_VendorMaster_Active DEFAULT 1,
        CreatedDate     DATETIME2(2)    NOT NULL
                        CONSTRAINT DF_VendorMaster_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(100)   NULL,
        ModifiedDate    DATETIME2(2)    NULL,
        ModifiedBy      NVARCHAR(100)   NULL,
        RowVersion      ROWVERSION      NOT NULL,

        CONSTRAINT PK_VendorMaster PRIMARY KEY CLUSTERED (VendorId)
    );
END;
GO

-- ============================================================================
-- 3.  CUSTOMER & CONTACTS  (Sections 1, 20)
-- ============================================================================

-- ──────────────────────────────────────────────
-- 3.1  Customer
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Customer', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customer
    (
        CustomerId          INT             IDENTITY(1,1)   NOT NULL,
        CompanyName         NVARCHAR(200)   NOT NULL,
        CustomerCode        NVARCHAR(50)    NULL,
        BillingAddress1     NVARCHAR(200)   NULL,
        BillingAddress2     NVARCHAR(200)   NULL,
        BillingCity         NVARCHAR(100)   NULL,
        BillingState        NVARCHAR(100)   NULL,
        BillingPostalCode   NVARCHAR(20)    NULL,
        BillingCountry      NVARCHAR(100)   NULL,
        ShippingAddress1    NVARCHAR(200)   NULL,
        ShippingAddress2    NVARCHAR(200)   NULL,
        ShippingCity        NVARCHAR(100)   NULL,
        ShippingState       NVARCHAR(100)   NULL,
        ShippingPostalCode  NVARCHAR(20)    NULL,
        ShippingCountry     NVARCHAR(100)   NULL,
        Phone               NVARCHAR(30)    NULL,
        Fax                 NVARCHAR(30)    NULL,
        Email               NVARCHAR(256)   NULL,
        Website             NVARCHAR(500)   NULL,
        PaymentTerms        NVARCHAR(100)   NULL,
        TaxExempt           BIT             NOT NULL
                            CONSTRAINT DF_Customer_TaxExempt DEFAULT 0,
        CreditLimit         DECIMAL(18,2)   NULL,
        Notes               NVARCHAR(MAX)   NULL,
        QBCustomerListID    NVARCHAR(50)    NULL,       -- QuickBooks Desktop ListID
        QBFullName          NVARCHAR(200)   NULL,       -- QB FullName field
        QBEditSequence      NVARCHAR(50)    NULL,       -- QB Edit Sequence for sync
        IsSyncedFromQB      BIT             NOT NULL
                            CONSTRAINT DF_Customer_SyncedFromQB DEFAULT 0,
        IsActive            BIT             NOT NULL
                            CONSTRAINT DF_Customer_Active DEFAULT 1,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Customer_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_Customer PRIMARY KEY CLUSTERED (CustomerId)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_Customer_CompanyName
    ON dbo.Customer (CompanyName);
GO

CREATE NONCLUSTERED INDEX IX_Customer_QBListID
    ON dbo.Customer (QBCustomerListID)
    WHERE QBCustomerListID IS NOT NULL;
GO

-- ──────────────────────────────────────────────
-- 3.2  CustomerContact  (Section 1 – multiple contacts)
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.CustomerContact', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerContact
    (
        ContactId       INT             IDENTITY(1,1)   NOT NULL,
        CustomerId      INT             NOT NULL,
        FirstName       NVARCHAR(100)   NOT NULL,
        LastName        NVARCHAR(100)   NULL,
        FullName    AS  (FirstName + ISNULL(N' ' + LastName, N'')) PERSISTED,
        Title           NVARCHAR(100)   NULL,
        Email           NVARCHAR(256)   NULL,
        Phone           NVARCHAR(30)    NULL,
        MobilePhone     NVARCHAR(30)    NULL,
        IsPrimary       BIT             NOT NULL
                        CONSTRAINT DF_Contact_IsPrimary DEFAULT 0,
        IsActive        BIT             NOT NULL
                        CONSTRAINT DF_Contact_Active DEFAULT 1,
        Notes           NVARCHAR(500)   NULL,
        CreatedDate     DATETIME2(2)    NOT NULL
                        CONSTRAINT DF_Contact_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(100)   NULL,
        ModifiedDate    DATETIME2(2)    NULL,
        ModifiedBy      NVARCHAR(100)   NULL,
        RowVersion      ROWVERSION      NOT NULL,

        CONSTRAINT PK_CustomerContact PRIMARY KEY CLUSTERED (ContactId),
        CONSTRAINT FK_CustomerContact_Customer
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customer (CustomerId)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_CustomerContact_CustomerId
    ON dbo.CustomerContact (CustomerId)
    INCLUDE (FirstName, LastName, Email, IsPrimary);
GO

-- ──────────────────────────────────────────────
-- 3.3  CustomerRide  (Section 2)
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.CustomerRide', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerRide
    (
        RideId              INT             IDENTITY(1,1)   NOT NULL,
        CustomerId          INT             NOT NULL,
        RideManufacturer    NVARCHAR(200)   NOT NULL,
        RideModel           NVARCHAR(200)   NOT NULL,
        SerialNumber        NVARCHAR(100)   NULL,
        YearManufactured    INT             NULL,
        Notes               NVARCHAR(500)   NULL,
        IsActive            BIT             NOT NULL
                            CONSTRAINT DF_Ride_Active DEFAULT 1,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Ride_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_CustomerRide PRIMARY KEY CLUSTERED (RideId),
        CONSTRAINT FK_CustomerRide_Customer
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customer (CustomerId)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_CustomerRide_Customer
    ON dbo.CustomerRide (CustomerId)
    INCLUDE (RideManufacturer, RideModel, SerialNumber);
GO

-- ============================================================================
-- 4.  WAREHOUSE LOCATIONS
-- ============================================================================
IF OBJECT_ID(N'dbo.WarehouseLocation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WarehouseLocation
    (
        LocationId      INT             IDENTITY(1,1)   NOT NULL,
        LocationCode    NVARCHAR(50)    NOT NULL,
        LocationName    NVARCHAR(200)   NOT NULL,
        Zone            NVARCHAR(50)    NULL,
        Aisle           NVARCHAR(20)    NULL,
        Shelf           NVARCHAR(20)    NULL,
        Bin             NVARCHAR(20)    NULL,
        IsHoldingArea   BIT             NOT NULL
                        CONSTRAINT DF_Location_Holding DEFAULT 0,
        IsActive        BIT             NOT NULL
                        CONSTRAINT DF_Location_Active DEFAULT 1,
        Notes           NVARCHAR(500)   NULL,
        CreatedDate     DATETIME2(2)    NOT NULL
                        CONSTRAINT DF_Location_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(100)   NULL,
        ModifiedDate    DATETIME2(2)    NULL,
        ModifiedBy      NVARCHAR(100)   NULL,

        CONSTRAINT PK_WarehouseLocation PRIMARY KEY CLUSTERED (LocationId),
        CONSTRAINT UQ_WarehouseLocation_Code UNIQUE (LocationCode)
    );
END;
GO

-- ──────────────────────────────────────────────
-- Holding Location  (Section 7 – customer order holding)
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.HoldingLocation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HoldingLocation
    (
        HoldingLocationId   INT             IDENTITY(1,1)   NOT NULL,
        LocationCode        NVARCHAR(50)    NOT NULL,
        LocationName        NVARCHAR(200)   NOT NULL,
        Description         NVARCHAR(500)   NULL,
        IsAvailable         BIT             NOT NULL
                            CONSTRAINT DF_HoldingLoc_Available DEFAULT 1,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_HoldingLoc_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,

        CONSTRAINT PK_HoldingLocation PRIMARY KEY CLUSTERED (HoldingLocationId),
        CONSTRAINT UQ_HoldingLocation_Code UNIQUE (LocationCode)
    );
END;
GO

-- ============================================================================
-- 5.  PARTS & INVENTORY  (Sections 5, 7, 8, 11, 12, 13)
-- ============================================================================

-- ──────────────────────────────────────────────
-- 5.1  Part (Master inventory part)
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Part', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Part
    (
        PartId              INT             IDENTITY(1,1)   NOT NULL,
        PartNumber          NVARCHAR(50)    NOT NULL,
        Description         NVARCHAR(500)   NOT NULL,
        DetailedDescription NVARCHAR(MAX)   NULL,
        CategoryCode        NVARCHAR(50)    NULL,
        UnitOfMeasure       NVARCHAR(20)    NOT NULL
                            CONSTRAINT DF_Part_UOM DEFAULT N'EA',

        -- Inventory quantities
        QuantityOnHand      DECIMAL(18,4)   NOT NULL
                            CONSTRAINT DF_Part_QOH DEFAULT 0,
        ReservedQuantity    DECIMAL(18,4)   NOT NULL
                            CONSTRAINT DF_Part_Reserved DEFAULT 0,
        AvailableQuantity AS (QuantityOnHand - ReservedQuantity) PERSISTED,

        -- Reorder thresholds  (Section 9)
        ReorderPoint        DECIMAL(18,4)   NULL,
        ReorderQuantity     DECIMAL(18,4)   NULL,
        IsNonStocked        BIT             NOT NULL
                            CONSTRAINT DF_Part_NonStocked DEFAULT 0,

        -- Pricing  (Section 5)
        LastVendorCostEur   DECIMAL(18,4)   NULL,
        SellingPriceUsd     DECIMAL(18,4)   NULL,
        -- SellingPriceUsd = LastVendorCostEur × EuroConversionFactor × Markup
        -- (recalculated via application logic using SystemConfiguration values)

        -- Warehouse
        PrimaryLocationId   INT             NULL,
        Weight              DECIMAL(18,4)   NULL,       -- lbs
        PhotoUrl            NVARCHAR(500)   NULL,

        -- Part Merge  (Section 13)
        IsMerged            BIT             NOT NULL
                            CONSTRAINT DF_Part_Merged DEFAULT 0,
        MergedIntoPartId    INT             NULL,
        MergedDate          DATETIME2(2)    NULL,
        MergedBy            NVARCHAR(100)   NULL,

        -- QuickBooks reference
        QBItemListID        NVARCHAR(50)    NULL,

        -- Standard audit columns
        IsActive            BIT             NOT NULL
                            CONSTRAINT DF_Part_Active DEFAULT 1,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Part_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,   -- Optimistic concurrency

        CONSTRAINT PK_Part PRIMARY KEY CLUSTERED (PartId),
        CONSTRAINT UQ_Part_PartNumber UNIQUE (PartNumber),
        CONSTRAINT FK_Part_PrimaryLocation
            FOREIGN KEY (PrimaryLocationId) REFERENCES dbo.WarehouseLocation (LocationId),
        CONSTRAINT FK_Part_MergedInto
            FOREIGN KEY (MergedIntoPartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT CK_Part_QOH CHECK (QuantityOnHand >= 0),
        CONSTRAINT CK_Part_Reserved CHECK (ReservedQuantity >= 0)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_Part_PartNumber
    ON dbo.Part (PartNumber)
    INCLUDE (Description, QuantityOnHand, ReservedQuantity, SellingPriceUsd, IsActive)
    WHERE IsMerged = 0;
GO

CREATE NONCLUSTERED INDEX IX_Part_Category
    ON dbo.Part (CategoryCode)
    WHERE IsActive = 1 AND IsMerged = 0;
GO

-- ──────────────────────────────────────────────
-- 5.2  VendorSource  (Section 11 – Multiple Vendor Sources)
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.VendorSource', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VendorSource
    (
        VendorSourceId      INT             IDENTITY(1,1)   NOT NULL,
        PartId              INT             NOT NULL,
        VendorId            INT             NOT NULL,
        VendorPartNumber    NVARCHAR(100)   NULL,
        VendorDescription   NVARCHAR(500)   NULL,
        LastKnownCostEur    DECIMAL(18,4)   NULL,
        LeadTimeDays        INT             NULL,
        MinOrderQuantity    DECIMAL(18,4)   NULL,
        IsPreferred         BIT             NOT NULL
                            CONSTRAINT DF_VendorSource_Preferred DEFAULT 0,
        IsActive            BIT             NOT NULL
                            CONSTRAINT DF_VendorSource_Active DEFAULT 1,
        LastUpdated         DATETIME2(2)    NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_VendorSource_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_VendorSource PRIMARY KEY CLUSTERED (VendorSourceId),
        CONSTRAINT FK_VendorSource_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_VendorSource_Vendor
            FOREIGN KEY (VendorId) REFERENCES dbo.VendorMaster (VendorId),
        CONSTRAINT UQ_VendorSource_Part_Vendor UNIQUE (PartId, VendorId)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_VendorSource_PartId
    ON dbo.VendorSource (PartId)
    INCLUDE (VendorId, VendorPartNumber, LastKnownCostEur, IsPreferred);
GO

-- ──────────────────────────────────────────────
-- 5.3  PartRideApplication  (Section 12)
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.PartRideApplication', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PartRideApplication
    (
        PartRideAppId       INT             IDENTITY(1,1)   NOT NULL,
        PartId              INT             NOT NULL,
        RideManufacturer    NVARCHAR(200)   NOT NULL,
        RideModel           NVARCHAR(200)   NOT NULL,
        Notes               NVARCHAR(500)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_PartRideApp_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,

        CONSTRAINT PK_PartRideApplication PRIMARY KEY CLUSTERED (PartRideAppId),
        CONSTRAINT FK_PartRideApp_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT UQ_PartRideApp UNIQUE (PartId, RideManufacturer, RideModel)
    );
END;
GO

-- ============================================================================
-- 6.  CUSTOMER ORDERS & QUOTES  (Sections 3, 4, 6)
-- ============================================================================
IF OBJECT_ID(N'dbo.CustomerOrder', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerOrder
    (
        OrderId                 INT             IDENTITY(1,1)   NOT NULL,
        OrderNumber             NVARCHAR(30)    NOT NULL,
        OrderType               NVARCHAR(10)    NOT NULL
                                CONSTRAINT DF_Order_Type DEFAULT N'Order',
        -- OrderType: Quote, Order

        CustomerId              INT             NOT NULL,
        ContactId               INT             NULL,
        CustomerPONumber        NVARCHAR(100)   NULL,

        -- Addresses  (copied from customer at creation; editable per order)
        BillingAddress1         NVARCHAR(200)   NULL,
        BillingAddress2         NVARCHAR(200)   NULL,
        BillingCity             NVARCHAR(100)   NULL,
        BillingState            NVARCHAR(100)   NULL,
        BillingPostalCode       NVARCHAR(20)    NULL,
        BillingCountry          NVARCHAR(100)   NULL,
        ShippingAddress1        NVARCHAR(200)   NULL,
        ShippingAddress2        NVARCHAR(200)   NULL,
        ShippingCity            NVARCHAR(100)   NULL,
        ShippingState           NVARCHAR(100)   NULL,
        ShippingPostalCode      NVARCHAR(20)    NULL,
        ShippingCountry         NVARCHAR(100)   NULL,

        -- Order details
        OrderDate               DATE            NOT NULL
                                CONSTRAINT DF_Order_Date DEFAULT CAST(SYSUTCDATETIME() AS DATE),
        RequestedDeliveryDate   DATE            NULL,
        PaymentTerms            NVARCHAR(100)   NULL,
        ShipmentPreference      NVARCHAR(20)    NOT NULL
                                CONSTRAINT DF_Order_ShipPref DEFAULT N'Complete',
        -- ShipmentPreference: Complete, Partial

        -- Status  (Section 6)
        Status                  NVARCHAR(50)    NOT NULL
                                CONSTRAINT DF_Order_Status DEFAULT N'Quote',
        /*  Status values:
            Quote
            AwaitingCustomerApproval
            OrderConfirmed
            AwaitingParts
            PartiallyReceivedHolding
            ReadyToShip
            PartiallyShipped
            Shipped
            Invoiced
            Cancelled
        */

        -- Holding Location  (Section 7)
        HoldingLocationId       INT             NULL,

        -- Quote-specific  (Section 4)
        QuoteExpirationDate     DATE            NULL,
        QuotePricingLanguage    NVARCHAR(MAX)   NULL,
        ConvertedToOrderDate    DATE            NULL,
        OriginalQuoteId         INT             NULL,   -- Self-ref: if converted from a quote

        -- Totals  (computed by application / triggers)
        SubTotal                DECIMAL(18,2)   NOT NULL
                                CONSTRAINT DF_Order_SubTotal DEFAULT 0,
        ShippingCharge          DECIMAL(18,2)   NOT NULL
                                CONSTRAINT DF_Order_Shipping DEFAULT 0,
        RushCharge              DECIMAL(18,2)   NOT NULL
                                CONSTRAINT DF_Order_Rush DEFAULT 0,
        DiscountAmount          DECIMAL(18,2)   NOT NULL
                                CONSTRAINT DF_Order_Discount DEFAULT 0,
        OtherCharges            DECIMAL(18,2)   NOT NULL
                                CONSTRAINT DF_Order_Other DEFAULT 0,
        TotalAmount         AS  (SubTotal + ShippingCharge + RushCharge - DiscountAmount + OtherCharges) PERSISTED,

        -- Notes
        CustomerNotes           NVARCHAR(MAX)   NULL,   -- Customer-facing notes
        InternalNotes           NVARCHAR(MAX)   NULL,   -- Internal notes

        -- QuickBooks reference
        QBInvoiceTxnID          NVARCHAR(50)    NULL,

        -- Audit
        CreatedDate             DATETIME2(2)    NOT NULL
                                CONSTRAINT DF_Order_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy               NVARCHAR(100)   NULL,
        ModifiedDate            DATETIME2(2)    NULL,
        ModifiedBy              NVARCHAR(100)   NULL,
        RowVersion              ROWVERSION      NOT NULL,

        CONSTRAINT PK_CustomerOrder PRIMARY KEY CLUSTERED (OrderId),
        CONSTRAINT UQ_CustomerOrder_Number UNIQUE (OrderNumber),
        CONSTRAINT FK_Order_Customer
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customer (CustomerId),
        CONSTRAINT FK_Order_Contact
            FOREIGN KEY (ContactId) REFERENCES dbo.CustomerContact (ContactId),
        CONSTRAINT FK_Order_HoldingLocation
            FOREIGN KEY (HoldingLocationId) REFERENCES dbo.HoldingLocation (HoldingLocationId),
        CONSTRAINT FK_Order_OriginalQuote
            FOREIGN KEY (OriginalQuoteId) REFERENCES dbo.CustomerOrder (OrderId),
        CONSTRAINT CK_Order_Type CHECK (OrderType IN (N'Quote', N'Order')),
        CONSTRAINT CK_Order_Status CHECK (
            Status IN (
                N'Quote', N'AwaitingCustomerApproval', N'OrderConfirmed',
                N'AwaitingParts', N'PartiallyReceivedHolding', N'ReadyToShip',
                N'PartiallyShipped', N'Shipped', N'Invoiced', N'Cancelled'
            )
        ),
        CONSTRAINT CK_Order_ShipPref CHECK (ShipmentPreference IN (N'Complete', N'Partial'))
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_CustomerOrder_Customer_Status
    ON dbo.CustomerOrder (CustomerId, Status)
    INCLUDE (OrderNumber, OrderDate);
GO

CREATE NONCLUSTERED INDEX IX_CustomerOrder_Status
    ON dbo.CustomerOrder (Status, OrderDate DESC);
GO

CREATE NONCLUSTERED INDEX IX_CustomerOrder_OrderDate
    ON dbo.CustomerOrder (OrderDate DESC)
    INCLUDE (CustomerId, Status, TotalAmount);
GO

-- ──────────────────────────────────────────────
-- 6.1  CustomerOrderLine
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.CustomerOrderLine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerOrderLine
    (
        OrderLineId         INT             IDENTITY(1,1)   NOT NULL,
        OrderId             INT             NOT NULL,
        LineNumber          INT             NOT NULL,
        PartId              INT             NOT NULL,
        RideId              INT             NULL,       -- Specific customer ride  (Section 2)
        Quantity            DECIMAL(18,4)   NOT NULL,
        UnitPriceUsd        DECIMAL(18,4)   NOT NULL,
        LineTotal       AS  (Quantity * UnitPriceUsd) PERSISTED,
        QuantityReserved    DECIMAL(18,4)   NOT NULL
                            CONSTRAINT DF_OrderLine_Reserved DEFAULT 0,
        QuantityShipped     DECIMAL(18,4)   NOT NULL
                            CONSTRAINT DF_OrderLine_Shipped DEFAULT 0,
        QuantityBackordered AS (Quantity - QuantityShipped) PERSISTED,
        LineStatus          NVARCHAR(30)    NOT NULL
                            CONSTRAINT DF_OrderLine_Status DEFAULT N'Pending',
        -- LineStatus: Pending, Reserved, PartiallyReserved, Shipped, PartiallyShipped, Cancelled
        Notes               NVARCHAR(500)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_OrderLine_Created DEFAULT SYSUTCDATETIME(),
        ModifiedDate        DATETIME2(2)    NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_CustomerOrderLine PRIMARY KEY CLUSTERED (OrderLineId),
        CONSTRAINT FK_OrderLine_Order
            FOREIGN KEY (OrderId) REFERENCES dbo.CustomerOrder (OrderId),
        CONSTRAINT FK_OrderLine_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_OrderLine_Ride
            FOREIGN KEY (RideId) REFERENCES dbo.CustomerRide (RideId),
        CONSTRAINT UQ_OrderLine_OrderLineNum UNIQUE (OrderId, LineNumber),
        CONSTRAINT CK_OrderLine_Qty CHECK (Quantity > 0)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_OrderLine_PartId
    ON dbo.CustomerOrderLine (PartId)
    INCLUDE (OrderId, Quantity, QuantityReserved, QuantityShipped);
GO

-- ============================================================================
-- 7.  INVENTORY RESERVATIONS  (Section 7)
-- ============================================================================
IF OBJECT_ID(N'dbo.InventoryReservation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryReservation
    (
        ReservationId       INT             IDENTITY(1,1)   NOT NULL,
        OrderLineId         INT             NOT NULL,
        PartId              INT             NOT NULL,
        QuantityReserved    DECIMAL(18,4)   NOT NULL,
        HoldingLocationId   INT             NULL,       -- Where physically staged
        ReservationDate     DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Reservation_Date DEFAULT SYSUTCDATETIME(),
        Status              NVARCHAR(20)    NOT NULL
                            CONSTRAINT DF_Reservation_Status DEFAULT N'Active',
        -- Status: Active, Released (shipped), Cancelled
        ReleasedDate        DATETIME2(2)    NULL,
        Notes               NVARCHAR(500)   NULL,
        CreatedBy           NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_InventoryReservation PRIMARY KEY CLUSTERED (ReservationId),
        CONSTRAINT FK_Reservation_OrderLine
            FOREIGN KEY (OrderLineId) REFERENCES dbo.CustomerOrderLine (OrderLineId),
        CONSTRAINT FK_Reservation_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_Reservation_HoldingLoc
            FOREIGN KEY (HoldingLocationId) REFERENCES dbo.HoldingLocation (HoldingLocationId),
        CONSTRAINT CK_Reservation_Qty CHECK (QuantityReserved > 0),
        CONSTRAINT CK_Reservation_Status CHECK (Status IN (N'Active', N'Released', N'Cancelled'))
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_Reservation_OrderLine
    ON dbo.InventoryReservation (OrderLineId)
    WHERE Status = N'Active';
GO

CREATE NONCLUSTERED INDEX IX_Reservation_Part
    ON dbo.InventoryReservation (PartId)
    WHERE Status = N'Active';
GO

-- ============================================================================
-- 8.  PURCHASING QUEUE  (Section 9)
-- ============================================================================
IF OBJECT_ID(N'dbo.PurchasingQueueItem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchasingQueueItem
    (
        QueueItemId         INT             IDENTITY(1,1)   NOT NULL,
        PartId              INT             NOT NULL,
        SourceType          NVARCHAR(30)    NOT NULL,
        -- SourceType: CustomerOrderDeficit, StockReplenishment
        SourceOrderLineId   INT             NULL,       -- If from a customer order line
        QuantityRequired    DECIMAL(18,4)   NOT NULL,
        QuantityOrdered     DECIMAL(18,4)   NOT NULL
                            CONSTRAINT DF_PQI_Ordered DEFAULT 0,
        SuggestedVendorId   INT             NULL,
        SuggestedCostEur    DECIMAL(18,4)   NULL,
        RideManufacturer    NVARCHAR(200)   NULL,
        RideModel           NVARCHAR(200)   NULL,
        SerialNumber        NVARCHAR(100)   NULL,
        Status              NVARCHAR(30)    NOT NULL
                            CONSTRAINT DF_PQI_Status DEFAULT N'Pending',
        -- Status: Pending, PartiallyOrdered, FullyOrdered, Cancelled
        PurchaseOrderLineId INT             NULL,       -- Link to PO line once created
        Notes               NVARCHAR(500)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_PQI_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_PurchasingQueueItem PRIMARY KEY CLUSTERED (QueueItemId),
        CONSTRAINT FK_PQI_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_PQI_SourceOrderLine
            FOREIGN KEY (SourceOrderLineId) REFERENCES dbo.CustomerOrderLine (OrderLineId),
        CONSTRAINT FK_PQI_SuggestedVendor
            FOREIGN KEY (SuggestedVendorId) REFERENCES dbo.VendorMaster (VendorId),
        CONSTRAINT CK_PQI_SourceType CHECK (SourceType IN (N'CustomerOrderDeficit', N'StockReplenishment')),
        CONSTRAINT CK_PQI_Qty CHECK (QuantityRequired > 0)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_PQI_PartId_Status
    ON dbo.PurchasingQueueItem (PartId, Status)
    WHERE Status IN (N'Pending', N'PartiallyOrdered');
GO

-- ============================================================================
-- 9.  PURCHASE ORDERS  (Section 10)
-- ============================================================================
IF OBJECT_ID(N'dbo.PurchaseOrder', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseOrder
    (
        PurchaseOrderId     INT             IDENTITY(1,1)   NOT NULL,
        PONumber            NVARCHAR(30)    NOT NULL,
        VendorId            INT             NOT NULL,
        Status              NVARCHAR(30)    NOT NULL
                            CONSTRAINT DF_PO_Status DEFAULT N'Draft',
        /*  Status values:
            Draft
            Reviewed
            SentToVendor
            PartiallyReceived
            FullyReceived
            Closed
            Cancelled
        */
        OrderDate           DATE            NOT NULL
                            CONSTRAINT DF_PO_OrderDate DEFAULT CAST(SYSUTCDATETIME() AS DATE),
        ExpectedDeliveryDate DATE           NULL,
        SentToVendorDate    DATE            NULL,
        ShippingMethod      NVARCHAR(100)   NULL,
        Currency            NVARCHAR(3)     NOT NULL
                            CONSTRAINT DF_PO_Currency DEFAULT N'EUR',
        SubTotalEur         DECIMAL(18,4)   NOT NULL
                            CONSTRAINT DF_PO_SubTotal DEFAULT 0,
        FreightEur          DECIMAL(18,4)   NOT NULL
                            CONSTRAINT DF_PO_Freight DEFAULT 0,
        TotalEur        AS  (SubTotalEur + FreightEur) PERSISTED,
        VendorNotes         NVARCHAR(MAX)   NULL,       -- Notes sent to vendor
        InternalNotes       NVARCHAR(MAX)   NULL,

        -- QuickBooks reference  (Section 20)
        QBPOTxnID           NVARCHAR(50)    NULL,

        -- Audit
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_PO_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_PurchaseOrder PRIMARY KEY CLUSTERED (PurchaseOrderId),
        CONSTRAINT UQ_PurchaseOrder_Number UNIQUE (PONumber),
        CONSTRAINT FK_PO_Vendor
            FOREIGN KEY (VendorId) REFERENCES dbo.VendorMaster (VendorId),
        CONSTRAINT CK_PO_Status CHECK (
            Status IN (N'Draft', N'Reviewed', N'SentToVendor',
                       N'PartiallyReceived', N'FullyReceived', N'Closed', N'Cancelled')
        )
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_PO_Vendor_Status
    ON dbo.PurchaseOrder (VendorId, Status)
    INCLUDE (PONumber, OrderDate);
GO

CREATE NONCLUSTERED INDEX IX_PO_Status
    ON dbo.PurchaseOrder (Status, OrderDate DESC);
GO

-- ──────────────────────────────────────────────
-- 9.1  PurchaseOrderLine
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.PurchaseOrderLine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseOrderLine
    (
        POLineId            INT             IDENTITY(1,1)   NOT NULL,
        PurchaseOrderId     INT             NOT NULL,
        LineNumber          INT             NOT NULL,
        PartId              INT             NOT NULL,
        VendorPartNumber    NVARCHAR(100)   NULL,
        VendorDescription   NVARCHAR(500)   NULL,
        Quantity            DECIMAL(18,4)   NOT NULL,
        QuantityReceived    DECIMAL(18,4)   NOT NULL
                            CONSTRAINT DF_POLine_Received DEFAULT 0,
        QuantityOutstanding AS (Quantity - QuantityReceived) PERSISTED,
        UnitCostEur         DECIMAL(18,4)   NOT NULL,
        LineTotalEur    AS  (Quantity * UnitCostEur) PERSISTED,

        -- Ride/Serial info  (required by some vendors per Section 10)
        RideManufacturer    NVARCHAR(200)   NULL,
        RideModel           NVARCHAR(200)   NULL,
        SerialNumber        NVARCHAR(100)   NULL,

        -- Order allocation tracking  (Section 9)
        OrderPurpose        NVARCHAR(30)    NULL,
        -- OrderPurpose: CustomerDemand, StockReplenishment
        SourceOrderId       INT             NULL,
        SourceOrderLineId   INT             NULL,

        Notes               NVARCHAR(500)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_POLine_Created DEFAULT SYSUTCDATETIME(),
        ModifiedDate        DATETIME2(2)    NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_PurchaseOrderLine PRIMARY KEY CLUSTERED (POLineId),
        CONSTRAINT FK_POLine_PO
            FOREIGN KEY (PurchaseOrderId) REFERENCES dbo.PurchaseOrder (PurchaseOrderId),
        CONSTRAINT FK_POLine_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_POLine_SourceOrder
            FOREIGN KEY (SourceOrderId) REFERENCES dbo.CustomerOrder (OrderId),
        CONSTRAINT FK_POLine_SourceOrderLine
            FOREIGN KEY (SourceOrderLineId) REFERENCES dbo.CustomerOrderLine (OrderLineId),
        CONSTRAINT UQ_POLine_POLineNum UNIQUE (PurchaseOrderId, LineNumber),
        CONSTRAINT CK_POLine_Qty CHECK (Quantity > 0)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_POLine_PartId
    ON dbo.PurchaseOrderLine (PartId)
    INCLUDE (PurchaseOrderId, Quantity, QuantityReceived, UnitCostEur);
GO

-- ============================================================================
-- 10.  VENDOR INVOICES  (Section 14 – AI Processing)
-- ============================================================================
IF OBJECT_ID(N'dbo.VendorInvoice', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VendorInvoice
    (
        VendorInvoiceId     INT             IDENTITY(1,1)   NOT NULL,
        InvoiceNumber       NVARCHAR(50)    NOT NULL,
        VendorId            INT             NULL,           -- NULL until matched
        PurchaseOrderId     INT             NULL,           -- Primary linked PO
        InvoiceDate         DATE            NULL,
        DueDate             DATE            NULL,
        Currency            NVARCHAR(3)     NOT NULL
                            CONSTRAINT DF_VI_Currency DEFAULT N'EUR',
        SubTotalEur         DECIMAL(18,4)   NULL,
        FreightEur          DECIMAL(18,4)   NULL,
        TaxEur              DECIMAL(18,4)   NULL,
        TotalEur            DECIMAL(18,4)   NULL,

        -- AI Processing  (Section 14)
        Status              NVARCHAR(30)    NOT NULL
                            CONSTRAINT DF_VI_Status DEFAULT N'Uploaded',
        /*  Status values:
            Uploaded
            AiExtracted
            NeedsReview
            Approved
            EnqueuedForQB
            SyncedToQB
            Rejected
        */
        UploadedFileName    NVARCHAR(500)   NULL,
        UploadedFilePath    NVARCHAR(1000)  NULL,
        AiExtractionJson    NVARCHAR(MAX)   NULL,   -- Raw AI extraction output
        AiConfidenceScore   DECIMAL(5,2)    NULL,   -- Overall confidence 0-100
        ReviewedByUserId    INT             NULL,
        ReviewedDate        DATETIME2(2)    NULL,
        ReviewNotes         NVARCHAR(MAX)   NULL,

        -- Discrepancy tracking  (Section 22)
        HasDiscrepancy      BIT             NOT NULL
                            CONSTRAINT DF_VI_Discrepancy DEFAULT 0,
        DiscrepancyNotes    NVARCHAR(MAX)   NULL,

        -- QuickBooks reference  (Section 20)
        QBBillTxnID         NVARCHAR(50)    NULL,
        QBBillEditSequence  NVARCHAR(50)    NULL,

        -- Audit
        UploadedDate        DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_VI_Uploaded DEFAULT SYSUTCDATETIME(),
        UploadedBy          NVARCHAR(100)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_VI_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_VendorInvoice PRIMARY KEY CLUSTERED (VendorInvoiceId),
        CONSTRAINT FK_VI_Vendor
            FOREIGN KEY (VendorId) REFERENCES dbo.VendorMaster (VendorId),
        CONSTRAINT FK_VI_PO
            FOREIGN KEY (PurchaseOrderId) REFERENCES dbo.PurchaseOrder (PurchaseOrderId),
        CONSTRAINT FK_VI_ReviewedBy
            FOREIGN KEY (ReviewedByUserId) REFERENCES dbo.UserMaster (UserId),
        CONSTRAINT CK_VI_Status CHECK (
            Status IN (N'Uploaded', N'AiExtracted', N'NeedsReview',
                       N'Approved', N'EnqueuedForQB', N'SyncedToQB', N'Rejected')
        )
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_VI_Status
    ON dbo.VendorInvoice (Status)
    INCLUDE (VendorId, InvoiceNumber, TotalEur);
GO

CREATE NONCLUSTERED INDEX IX_VI_Vendor
    ON dbo.VendorInvoice (VendorId, InvoiceDate DESC);
GO

-- ──────────────────────────────────────────────
-- 10.1  VendorInvoiceLine
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.VendorInvoiceLine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VendorInvoiceLine
    (
        VILineId            INT             IDENTITY(1,1)   NOT NULL,
        VendorInvoiceId     INT             NOT NULL,
        LineNumber          INT             NOT NULL,
        PartId              INT             NULL,           -- NULL until matched
        VendorPartNumber    NVARCHAR(100)   NULL,
        Description         NVARCHAR(500)   NULL,
        Quantity            DECIMAL(18,4)   NULL,
        UnitCostEur         DECIMAL(18,4)   NULL,
        LineTotalEur    AS  (Quantity * UnitCostEur) PERSISTED,
        POLineId            INT             NULL,           -- Matched PO line

        -- AI matching confidence
        AiMatchConfidence   DECIMAL(5,2)    NULL,
        IsMatchConfirmed    BIT             NOT NULL
                            CONSTRAINT DF_VILine_Confirmed DEFAULT 0,
        MatchedByUserId     INT             NULL,

        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_VILine_Created DEFAULT SYSUTCDATETIME(),
        ModifiedDate        DATETIME2(2)    NULL,

        CONSTRAINT PK_VendorInvoiceLine PRIMARY KEY CLUSTERED (VILineId),
        CONSTRAINT FK_VILine_Invoice
            FOREIGN KEY (VendorInvoiceId) REFERENCES dbo.VendorInvoice (VendorInvoiceId),
        CONSTRAINT FK_VILine_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_VILine_POLine
            FOREIGN KEY (POLineId) REFERENCES dbo.PurchaseOrderLine (POLineId),
        CONSTRAINT FK_VILine_MatchedBy
            FOREIGN KEY (MatchedByUserId) REFERENCES dbo.UserMaster (UserId)
    );
END;
GO

-- ──────────────────────────────────────────────
-- 10.2  VendorPartCrossReference  (Section 14 – confirmed AI mappings)
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.VendorPartCrossReference', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VendorPartCrossReference
    (
        CrossRefId          INT             IDENTITY(1,1)   NOT NULL,
        VendorId            INT             NOT NULL,
        VendorPartNumber    NVARCHAR(100)   NOT NULL,
        VendorDescription   NVARCHAR(500)   NULL,
        PartId              INT             NOT NULL,
        ConfidenceScore     DECIMAL(5,2)    NULL,
        IsConfirmed         BIT             NOT NULL
                            CONSTRAINT DF_XRef_Confirmed DEFAULT 1,
        ConfirmedByUserId   INT             NULL,
        ConfirmedDate       DATETIME2(2)    NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_XRef_Created DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_VendorPartCrossReference PRIMARY KEY CLUSTERED (CrossRefId),
        CONSTRAINT FK_XRef_Vendor
            FOREIGN KEY (VendorId) REFERENCES dbo.VendorMaster (VendorId),
        CONSTRAINT FK_XRef_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_XRef_ConfirmedBy
            FOREIGN KEY (ConfirmedByUserId) REFERENCES dbo.UserMaster (UserId),
        CONSTRAINT UQ_VendorPartXRef UNIQUE (VendorId, VendorPartNumber, PartId)
    );
END;
GO

-- ============================================================================
-- 11.  RECEIVING  (Section 15, 16)
-- ============================================================================
IF OBJECT_ID(N'dbo.ReceivingRecord', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReceivingRecord
    (
        ReceivingId         INT             IDENTITY(1,1)   NOT NULL,
        ReceivingNumber     NVARCHAR(30)    NOT NULL,
        PurchaseOrderId     INT             NOT NULL,
        VendorId            INT             NOT NULL,
        ReceivingDate       DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Receiving_Date DEFAULT SYSUTCDATETIME(),
        ReceivedByUserId    INT             NULL,
        WarehouseLocationId INT             NULL,       -- Default receiving location
        CarrierName         NVARCHAR(100)   NULL,
        TrackingNumber      NVARCHAR(200)   NULL,
        PackingSlipNumber   NVARCHAR(100)   NULL,
        Status              NVARCHAR(20)    NOT NULL
                            CONSTRAINT DF_Receiving_Status DEFAULT N'Completed',
        -- Status: InProgress, Completed, HasDiscrepancy
        HasDiscrepancy      BIT             NOT NULL
                            CONSTRAINT DF_Receiving_Discrepancy DEFAULT 0,
        DiscrepancyNotes    NVARCHAR(MAX)   NULL,
        Notes               NVARCHAR(MAX)   NULL,

        -- Label printing  (Section 16)
        LabelsPrinted       BIT             NOT NULL
                            CONSTRAINT DF_Receiving_Labels DEFAULT 0,
        LabelCount          INT             NULL,

        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Receiving_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_ReceivingRecord PRIMARY KEY CLUSTERED (ReceivingId),
        CONSTRAINT UQ_ReceivingRecord_Number UNIQUE (ReceivingNumber),
        CONSTRAINT FK_Receiving_PO
            FOREIGN KEY (PurchaseOrderId) REFERENCES dbo.PurchaseOrder (PurchaseOrderId),
        CONSTRAINT FK_Receiving_Vendor
            FOREIGN KEY (VendorId) REFERENCES dbo.VendorMaster (VendorId),
        CONSTRAINT FK_Receiving_User
            FOREIGN KEY (ReceivedByUserId) REFERENCES dbo.UserMaster (UserId),
        CONSTRAINT FK_Receiving_Location
            FOREIGN KEY (WarehouseLocationId) REFERENCES dbo.WarehouseLocation (LocationId)
    );
END;
GO

-- ──────────────────────────────────────────────
-- 11.1  ReceivingLine
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.ReceivingLine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReceivingLine
    (
        ReceivingLineId     INT             IDENTITY(1,1)   NOT NULL,
        ReceivingId         INT             NOT NULL,
        POLineId            INT             NOT NULL,
        PartId              INT             NOT NULL,
        QuantityExpected    DECIMAL(18,4)   NOT NULL,
        QuantityReceived    DECIMAL(18,4)   NOT NULL,
        QuantityDamaged     DECIMAL(18,4)   NOT NULL
                            CONSTRAINT DF_RcvLine_Damaged DEFAULT 0,
        LocationId          INT             NULL,       -- Actual put-away location
        HoldingLocationId   INT             NULL,       -- Customer order holding loc

        -- Discrepancy  (Section 22)
        HasDiscrepancy  AS  (CASE WHEN QuantityReceived <> QuantityExpected THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END) PERSISTED,
        DiscrepancyNotes    NVARCHAR(500)   NULL,

        -- Label defaults  (Section 16)
        LabelQty            INT             NULL,

        Notes               NVARCHAR(500)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_RcvLine_Created DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_ReceivingLine PRIMARY KEY CLUSTERED (ReceivingLineId),
        CONSTRAINT FK_RcvLine_Receiving
            FOREIGN KEY (ReceivingId) REFERENCES dbo.ReceivingRecord (ReceivingId),
        CONSTRAINT FK_RcvLine_POLine
            FOREIGN KEY (POLineId) REFERENCES dbo.PurchaseOrderLine (POLineId),
        CONSTRAINT FK_RcvLine_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_RcvLine_Location
            FOREIGN KEY (LocationId) REFERENCES dbo.WarehouseLocation (LocationId),
        CONSTRAINT FK_RcvLine_HoldingLoc
            FOREIGN KEY (HoldingLocationId) REFERENCES dbo.HoldingLocation (HoldingLocationId),
        CONSTRAINT CK_RcvLine_QtyRcvd CHECK (QuantityReceived >= 0)
    );
END;
GO

-- ============================================================================
-- 12.  SHIPPING  (Sections 17, 18)
-- ============================================================================
IF OBJECT_ID(N'dbo.Shipment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Shipment
    (
        ShipmentId          INT             IDENTITY(1,1)   NOT NULL,
        ShipmentNumber      NVARCHAR(30)    NOT NULL,
        OrderId             INT             NOT NULL,
        ShipmentDate        DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Shipment_Date DEFAULT SYSUTCDATETIME(),
        ShippedByUserId     INT             NULL,

        -- Carrier / Tracking  (Section 18)
        Carrier             NVARCHAR(50)    NULL,       -- UPS, FedEx, Freight, Other
        ServiceType         NVARCHAR(100)   NULL,       -- UPS Ground, UPS 2nd Day Air, etc.
        TrackingNumber      NVARCHAR(200)   NULL,
        ShippingLabelUrl    NVARCHAR(500)   NULL,

        -- Shipping charges
        ShippingCostActual  DECIMAL(18,2)   NULL,       -- Negotiated rate paid
        ShippingCostBilled  DECIMAL(18,2)   NULL,       -- Published rate billed to customer

        -- Shipping address  (copied from order)
        ShipToName          NVARCHAR(200)   NULL,
        ShipToAddress1      NVARCHAR(200)   NULL,
        ShipToAddress2      NVARCHAR(200)   NULL,
        ShipToCity          NVARCHAR(100)   NULL,
        ShipToState         NVARCHAR(100)   NULL,
        ShipToPostalCode    NVARCHAR(20)    NULL,
        ShipToCountry       NVARCHAR(100)   NULL,

        Status              NVARCHAR(20)    NOT NULL
                            CONSTRAINT DF_Shipment_Status DEFAULT N'Pending',
        -- Status: Pending, Packed, Shipped, Delivered, Cancelled
        IsInvoiced          BIT             NOT NULL
                            CONSTRAINT DF_Shipment_Invoiced DEFAULT 0,
        Notes               NVARCHAR(MAX)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Shipment_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_Shipment PRIMARY KEY CLUSTERED (ShipmentId),
        CONSTRAINT UQ_Shipment_Number UNIQUE (ShipmentNumber),
        CONSTRAINT FK_Shipment_Order
            FOREIGN KEY (OrderId) REFERENCES dbo.CustomerOrder (OrderId),
        CONSTRAINT FK_Shipment_User
            FOREIGN KEY (ShippedByUserId) REFERENCES dbo.UserMaster (UserId),
        CONSTRAINT CK_Shipment_Status CHECK (
            Status IN (N'Pending', N'Packed', N'Shipped', N'Delivered', N'Cancelled')
        )
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_Shipment_Order
    ON dbo.Shipment (OrderId)
    INCLUDE (ShipmentDate, Status, IsInvoiced);
GO

-- ──────────────────────────────────────────────
-- 12.1  ShipmentLine
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.ShipmentLine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShipmentLine
    (
        ShipmentLineId      INT             IDENTITY(1,1)   NOT NULL,
        ShipmentId          INT             NOT NULL,
        OrderLineId         INT             NOT NULL,
        PartId              INT             NOT NULL,
        QuantityShipped     DECIMAL(18,4)   NOT NULL,
        LocationId          INT             NULL,       -- Picked from location
        Notes               NVARCHAR(500)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_ShipLine_Created DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_ShipmentLine PRIMARY KEY CLUSTERED (ShipmentLineId),
        CONSTRAINT FK_ShipLine_Shipment
            FOREIGN KEY (ShipmentId) REFERENCES dbo.Shipment (ShipmentId),
        CONSTRAINT FK_ShipLine_OrderLine
            FOREIGN KEY (OrderLineId) REFERENCES dbo.CustomerOrderLine (OrderLineId),
        CONSTRAINT FK_ShipLine_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_ShipLine_Location
            FOREIGN KEY (LocationId) REFERENCES dbo.WarehouseLocation (LocationId),
        CONSTRAINT CK_ShipLine_Qty CHECK (QuantityShipped > 0)
    );
END;
GO

-- ──────────────────────────────────────────────
-- 12.2  ShipmentPackage  (Section 18 – UPS Integration)
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.ShipmentPackage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShipmentPackage
    (
        PackageId           INT             IDENTITY(1,1)   NOT NULL,
        ShipmentId          INT             NOT NULL,
        PackageNumber       INT             NOT NULL
                            CONSTRAINT DF_Pkg_Number DEFAULT 1,
        WeightLbs           DECIMAL(10,2)   NULL,
        LengthIn            DECIMAL(10,2)   NULL,
        WidthIn             DECIMAL(10,2)   NULL,
        HeightIn            DECIMAL(10,2)   NULL,
        TrackingNumber      NVARCHAR(200)   NULL,
        LabelImageUrl       NVARCHAR(500)   NULL,
        InsuredValue        DECIMAL(18,2)   NULL,
        Notes               NVARCHAR(500)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Pkg_Created DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_ShipmentPackage PRIMARY KEY CLUSTERED (PackageId),
        CONSTRAINT FK_Pkg_Shipment
            FOREIGN KEY (ShipmentId) REFERENCES dbo.Shipment (ShipmentId),
        CONSTRAINT UQ_ShipmentPackage UNIQUE (ShipmentId, PackageNumber)
    );
END;
GO

-- ============================================================================
-- 13.  CUSTOMER INVOICES  (Section 19)
-- ============================================================================
IF OBJECT_ID(N'dbo.CustomerInvoice', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerInvoice
    (
        InvoiceId           INT             IDENTITY(1,1)   NOT NULL,
        InvoiceNumber       NVARCHAR(30)    NOT NULL,
        OrderId             INT             NOT NULL,
        ShipmentId          INT             NULL,
        CustomerId          INT             NOT NULL,
        InvoiceDate         DATE            NOT NULL
                            CONSTRAINT DF_CustInv_Date DEFAULT CAST(SYSUTCDATETIME() AS DATE),
        DueDate             DATE            NULL,
        PaymentTerms        NVARCHAR(100)   NULL,
        Currency            NVARCHAR(3)     NOT NULL
                            CONSTRAINT DF_CustInv_Currency DEFAULT N'USD',

        -- Totals
        SubTotal            DECIMAL(18,2)   NOT NULL
                            CONSTRAINT DF_CustInv_SubTotal DEFAULT 0,
        ShippingCharge      DECIMAL(18,2)   NOT NULL
                            CONSTRAINT DF_CustInv_Shipping DEFAULT 0,
        RushCharge          DECIMAL(18,2)   NOT NULL
                            CONSTRAINT DF_CustInv_Rush DEFAULT 0,
        DiscountAmount      DECIMAL(18,2)   NOT NULL
                            CONSTRAINT DF_CustInv_Discount DEFAULT 0,
        OtherCharges        DECIMAL(18,2)   NOT NULL
                            CONSTRAINT DF_CustInv_Other DEFAULT 0,
        TaxAmount           DECIMAL(18,2)   NOT NULL
                            CONSTRAINT DF_CustInv_Tax DEFAULT 0,
        TotalAmount     AS  (SubTotal + ShippingCharge + RushCharge - DiscountAmount + OtherCharges + TaxAmount) PERSISTED,

        Status              NVARCHAR(30)    NOT NULL
                            CONSTRAINT DF_CustInv_Status DEFAULT N'Draft',
        /*  Status values:
            Draft
            PendingReview
            Approved
            EnqueuedForQB
            SyncedToQB
            Paid            -- updated from QB
            Voided
        */

        -- Billing address (copied from order)
        BillToName          NVARCHAR(200)   NULL,
        BillToAddress1      NVARCHAR(200)   NULL,
        BillToAddress2      NVARCHAR(200)   NULL,
        BillToCity          NVARCHAR(100)   NULL,
        BillToState         NVARCHAR(100)   NULL,
        BillToPostalCode    NVARCHAR(20)    NULL,
        BillToCountry       NVARCHAR(100)   NULL,

        -- QuickBooks reference  (Section 20)
        QBInvoiceTxnID      NVARCHAR(50)    NULL,
        QBEditSequence      NVARCHAR(50)    NULL,

        Notes               NVARCHAR(MAX)   NULL,
        InternalNotes       NVARCHAR(MAX)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_CustInv_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        ModifiedBy          NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_CustomerInvoice PRIMARY KEY CLUSTERED (InvoiceId),
        CONSTRAINT UQ_CustomerInvoice_Number UNIQUE (InvoiceNumber),
        CONSTRAINT FK_CustInv_Order
            FOREIGN KEY (OrderId) REFERENCES dbo.CustomerOrder (OrderId),
        CONSTRAINT FK_CustInv_Shipment
            FOREIGN KEY (ShipmentId) REFERENCES dbo.Shipment (ShipmentId),
        CONSTRAINT FK_CustInv_Customer
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customer (CustomerId),
        CONSTRAINT CK_CustInv_Status CHECK (
            Status IN (N'Draft', N'PendingReview', N'Approved',
                       N'EnqueuedForQB', N'SyncedToQB', N'Paid', N'Voided')
        )
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_CustInv_Order
    ON dbo.CustomerInvoice (OrderId)
    INCLUDE (InvoiceNumber, Status, TotalAmount);
GO

CREATE NONCLUSTERED INDEX IX_CustInv_Customer_Status
    ON dbo.CustomerInvoice (CustomerId, Status)
    INCLUDE (InvoiceNumber, InvoiceDate, TotalAmount);
GO

-- ──────────────────────────────────────────────
-- 13.1  CustomerInvoiceLine
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.CustomerInvoiceLine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerInvoiceLine
    (
        InvoiceLineId       INT             IDENTITY(1,1)   NOT NULL,
        InvoiceId           INT             NOT NULL,
        LineNumber          INT             NOT NULL,
        LineType            NVARCHAR(20)    NOT NULL
                            CONSTRAINT DF_CustInvLine_Type DEFAULT N'Part',
        -- LineType: Part, Shipping, Rush, Discount, Misc
        PartId              INT             NULL,           -- NULL for non-inventory charges
        ShipmentLineId      INT             NULL,           -- Link to shipped item
        Description         NVARCHAR(500)   NOT NULL,
        Quantity            DECIMAL(18,4)   NOT NULL
                            CONSTRAINT DF_CustInvLine_Qty DEFAULT 1,
        UnitPrice           DECIMAL(18,4)   NOT NULL,
        LineTotal       AS  (Quantity * UnitPrice) PERSISTED,
        Notes               NVARCHAR(500)   NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_CustInvLine_Created DEFAULT SYSUTCDATETIME(),
        ModifiedDate        DATETIME2(2)    NULL,

        CONSTRAINT PK_CustomerInvoiceLine PRIMARY KEY CLUSTERED (InvoiceLineId),
        CONSTRAINT FK_CustInvLine_Invoice
            FOREIGN KEY (InvoiceId) REFERENCES dbo.CustomerInvoice (InvoiceId),
        CONSTRAINT FK_CustInvLine_Part
            FOREIGN KEY (PartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_CustInvLine_ShipLine
            FOREIGN KEY (ShipmentLineId) REFERENCES dbo.ShipmentLine (ShipmentLineId),
        CONSTRAINT CK_CustInvLine_Type CHECK (
            LineType IN (N'Part', N'Shipping', N'Rush', N'Discount', N'Misc')
        )
    );
END;
GO

-- ============================================================================
-- 14.  QUICKBOOKS SYNC QUEUE  (Section 20)
-- ============================================================================
IF OBJECT_ID(N'dbo.QBSyncQueue', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QBSyncQueue
    (
        SyncId              INT             IDENTITY(1,1)   NOT NULL,
        EntityType          NVARCHAR(50)    NOT NULL,
        -- EntityType: Customer, VendorBill, CustomerInvoice, PurchaseOrder
        EntityId            INT             NOT NULL,
        SyncDirection       NVARCHAR(20)    NOT NULL
                            CONSTRAINT DF_QBSync_Direction DEFAULT N'ToQB',
        -- SyncDirection: ToQB, FromQB
        SyncAction          NVARCHAR(20)    NOT NULL
                            CONSTRAINT DF_QBSync_Action DEFAULT N'Create',
        -- SyncAction: Create, Update, Query
        Status              NVARCHAR(30)    NOT NULL
                            CONSTRAINT DF_QBSync_Status DEFAULT N'Pending',
        /*  Status values:
            Pending
            Processing
            Success
            FailedRetryable
            FailedFatal
            Archived
        */
        QBTxnID             NVARCHAR(50)    NULL,
        QBEditSequence      NVARCHAR(50)    NULL,
        QBRequestXml        NVARCHAR(MAX)   NULL,       -- qbXML request payload
        QBResponseXml       NVARCHAR(MAX)   NULL,       -- qbXML response payload
        RetryCount          INT             NOT NULL
                            CONSTRAINT DF_QBSync_Retry DEFAULT 0,
        MaxRetries          INT             NOT NULL
                            CONSTRAINT DF_QBSync_MaxRetry DEFAULT 3,
        ErrorMessage        NVARCHAR(MAX)   NULL,
        Priority            INT             NOT NULL
                            CONSTRAINT DF_QBSync_Priority DEFAULT 0,
        ScheduledDate       DATETIME2(2)    NULL,
        ProcessedDate       DATETIME2(2)    NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_QBSync_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        ModifiedDate        DATETIME2(2)    NULL,
        RowVersion          ROWVERSION      NOT NULL,

        CONSTRAINT PK_QBSyncQueue PRIMARY KEY CLUSTERED (SyncId),
        CONSTRAINT CK_QBSync_Status CHECK (
            Status IN (N'Pending', N'Processing', N'Success',
                       N'FailedRetryable', N'FailedFatal', N'Archived')
        ),
        CONSTRAINT CK_QBSync_Direction CHECK (SyncDirection IN (N'ToQB', N'FromQB')),
        CONSTRAINT CK_QBSync_Action CHECK (SyncAction IN (N'Create', N'Update', N'Query'))
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_QBSync_Status_Priority
    ON dbo.QBSyncQueue (Status, Priority DESC, CreatedDate)
    WHERE Status IN (N'Pending', N'FailedRetryable');
GO

CREATE NONCLUSTERED INDEX IX_QBSync_Entity
    ON dbo.QBSyncQueue (EntityType, EntityId)
    INCLUDE (Status, QBTxnID);
GO

-- ============================================================================
-- 15.  PART MERGE HISTORY  (Section 13 – detailed merge audit)
-- ============================================================================
IF OBJECT_ID(N'dbo.PartMergeHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PartMergeHistory
    (
        MergeId             INT             IDENTITY(1,1)   NOT NULL,
        SurvivorPartId      INT             NOT NULL,
        MergedPartId        INT             NOT NULL,
        MergedDate          DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Merge_Date DEFAULT SYSUTCDATETIME(),
        MergedByUserId      INT             NULL,
        MergedByUserName    NVARCHAR(100)   NULL,
        MergeDetails        NVARCHAR(MAX)   NULL,       -- JSON: what was consolidated
        Notes               NVARCHAR(500)   NULL,

        CONSTRAINT PK_PartMergeHistory PRIMARY KEY CLUSTERED (MergeId),
        CONSTRAINT FK_Merge_Survivor
            FOREIGN KEY (SurvivorPartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_Merge_Merged
            FOREIGN KEY (MergedPartId) REFERENCES dbo.Part (PartId),
        CONSTRAINT FK_Merge_User
            FOREIGN KEY (MergedByUserId) REFERENCES dbo.UserMaster (UserId)
    );
END;
GO

-- ============================================================================
-- 16.  NOTIFICATION QUEUE  (application-level toast / email alerts)
-- ============================================================================
IF OBJECT_ID(N'dbo.NotificationQueue', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationQueue
    (
        NotificationId      INT             IDENTITY(1,1)   NOT NULL,
        RecipientUserId     INT             NULL,
        RecipientRole       NVARCHAR(50)    NULL,       -- Broadcast to role
        NotificationType    NVARCHAR(50)    NOT NULL,
        -- NotificationType: PriceChange, StockAlert, ReceivingReady, ShipmentReady, SyncFailure, etc.
        Title               NVARCHAR(200)   NOT NULL,
        Message             NVARCHAR(MAX)   NOT NULL,
        RelatedEntity       NVARCHAR(128)   NULL,
        RelatedEntityId     INT             NULL,
        ActionUrl           NVARCHAR(500)   NULL,
        IsRead              BIT             NOT NULL
                            CONSTRAINT DF_Notif_Read DEFAULT 0,
        ReadDate            DATETIME2(2)    NULL,
        CreatedDate         DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Notif_Created DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_NotificationQueue PRIMARY KEY CLUSTERED (NotificationId),
        CONSTRAINT FK_Notif_User
            FOREIGN KEY (RecipientUserId) REFERENCES dbo.UserMaster (UserId)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_Notif_User_Unread
    ON dbo.NotificationQueue (RecipientUserId, IsRead, CreatedDate DESC)
    WHERE IsRead = 0;
GO

-- ============================================================================
-- 17.  FILE ATTACHMENTS  (generic document/photo storage)
-- ============================================================================
IF OBJECT_ID(N'dbo.FileAttachment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FileAttachment
    (
        AttachmentId        INT             IDENTITY(1,1)   NOT NULL,
        EntityType          NVARCHAR(50)    NOT NULL,
        EntityId            INT             NOT NULL,
        FileName            NVARCHAR(500)   NOT NULL,
        FileExtension       NVARCHAR(10)    NULL,
        ContentType         NVARCHAR(100)   NULL,
        FileSizeBytes       BIGINT          NULL,
        StoragePath         NVARCHAR(1000)  NOT NULL,
        Description         NVARCHAR(500)   NULL,
        UploadedDate        DATETIME2(2)    NOT NULL
                            CONSTRAINT DF_Attach_Uploaded DEFAULT SYSUTCDATETIME(),
        UploadedBy          NVARCHAR(100)   NULL,
        IsActive            BIT             NOT NULL
                            CONSTRAINT DF_Attach_Active DEFAULT 1,

        CONSTRAINT PK_FileAttachment PRIMARY KEY CLUSTERED (AttachmentId)
    );
END;
GO

CREATE NONCLUSTERED INDEX IX_Attach_Entity
    ON dbo.FileAttachment (EntityType, EntityId)
    WHERE IsActive = 1;
GO

-- ============================================================================
-- 18.  SEQUENCE / NUMBER GENERATORS
-- ============================================================================
IF OBJECT_ID(N'dbo.NumberSequence', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NumberSequence
    (
        SequenceName        NVARCHAR(50)    NOT NULL,
        Prefix              NVARCHAR(10)    NOT NULL,
        CurrentValue        INT             NOT NULL
                            CONSTRAINT DF_NumSeq_Current DEFAULT 0,
        IncrementBy         INT             NOT NULL
                            CONSTRAINT DF_NumSeq_Incr DEFAULT 1,
        PadWidth            INT             NOT NULL
                            CONSTRAINT DF_NumSeq_Pad DEFAULT 6,
        Description         NVARCHAR(200)   NULL,

        CONSTRAINT PK_NumberSequence PRIMARY KEY CLUSTERED (SequenceName)
    );
END;
GO


-- ============================================================================
-- 19.  SEED DATA
-- ============================================================================

-- ──────────────────────────────────────────────
-- 19.1  System Configuration  (Pricing parameters)
-- ──────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.SystemConfiguration WHERE ConfigKey = N'EuroConversionFactor')
BEGIN
    INSERT INTO dbo.SystemConfiguration (ConfigKey, ConfigValue, DataType, Description, Category)
    VALUES
    (N'EuroConversionFactor', N'1.10', N'Decimal', N'EUR to USD conversion factor used in pricing formula: VendorCostEUR × Factor × Markup = SellingPriceUSD', N'Pricing'),
    (N'PricingMarkup', N'1.40', N'Decimal', N'Markup multiplier used in pricing formula: VendorCostEUR × EuroConversionFactor × Markup = SellingPriceUSD', N'Pricing'),
    (N'DefaultPaymentTerms', N'Net 30', N'String', N'Default payment terms for new customer orders', N'Orders'),
    (N'QuoteExpirationDays', N'30', N'Int', N'Default number of days before a customer quote expires', N'Orders'),
    (N'QuotePricingLanguage', N'Prices quoted are valid for the period indicated. Prices are subject to change after the expiration date.', N'String', N'Standard pricing language included on customer quotes', N'Orders'),
    (N'MaxPORetryAttempts', N'3', N'Int', N'Maximum retry attempts for failed QB sync operations', N'QuickBooks'),
    (N'QBCompanyFile', N'', N'String', N'Path to QuickBooks Desktop company file for SDK connection', N'QuickBooks'),
    (N'DefaultShippingCarrier', N'UPS', N'String', N'Default shipping carrier for new shipments', N'Shipping'),
    (N'UPSAccountNumber', N'', N'String', N'UPS account number for shipping integration', N'Shipping'),
    (N'UPSAccessKey', N'', N'String', N'UPS API access key (encrypted)', N'Shipping'),
    (N'LabelPrinterName', N'', N'String', N'Network name of the Zebra/label printer for receiving labels', N'Warehouse'),
    (N'CompanyName', N'Amusement Ride Parts Inc.', N'String', N'Company name displayed on documents and invoices', N'Company'),
    (N'CompanyAddress', N'', N'String', N'Company address for documents', N'Company'),
    (N'CompanyPhone', N'', N'String', N'Company phone number', N'Company'),
    (N'CompanyEmail', N'', N'String', N'Company email for correspondence', N'Company');
END;
GO

-- ──────────────────────────────────────────────
-- 19.2  Number Sequences
-- ──────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.NumberSequence WHERE SequenceName = N'CustomerOrder')
BEGIN
    INSERT INTO dbo.NumberSequence (SequenceName, Prefix, CurrentValue, IncrementBy, PadWidth, Description)
    VALUES
    (N'CustomerOrder',    N'CO',   10000, 1, 6, N'Customer Order number sequence: CO-010001, CO-010002, ...'),
    (N'CustomerQuote',    N'CQ',   10000, 1, 6, N'Customer Quote number sequence'),
    (N'PurchaseOrder',    N'PO',   10000, 1, 6, N'Purchase Order number sequence'),
    (N'Shipment',         N'SH',   10000, 1, 6, N'Shipment number sequence'),
    (N'CustomerInvoice',  N'INV',  10000, 1, 6, N'Customer Invoice number sequence'),
    (N'ReceivingRecord',  N'RCV',  10000, 1, 6, N'Receiving record number sequence'),
    (N'VendorInvoice',    N'VI',   10000, 1, 6, N'Vendor Invoice tracking number sequence');
END;
GO

-- ──────────────────────────────────────────────
-- 19.3  Default Admin User
-- ──────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.UserMaster WHERE NormalizedUserName = N'ADMIN')
BEGIN
    INSERT INTO dbo.UserMaster
        (UserName, NormalizedUserName, Email, NormalizedEmail, FirstName, LastName, RoleCode, IsActive)
    VALUES
        (N'admin', N'ADMIN', N'admin@amusementparts.com', N'ADMIN@AMUSEMENTPARTS.COM', N'System', N'Administrator', N'Admin', 1);
END;
GO

-- ──────────────────────────────────────────────
-- 19.4  Default Warehouse Locations
-- ──────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.WarehouseLocation WHERE LocationCode = N'RECV')
BEGIN
    INSERT INTO dbo.WarehouseLocation (LocationCode, LocationName, Zone, IsHoldingArea, Notes)
    VALUES
    (N'RECV',       N'Receiving Dock',          N'Receiving',   0, N'Default receiving location'),
    (N'MAIN-A1',    N'Main Warehouse A1',       N'Main',        0, N'Primary storage zone A, aisle 1'),
    (N'MAIN-A2',    N'Main Warehouse A2',       N'Main',        0, N'Primary storage zone A, aisle 2'),
    (N'MAIN-B1',    N'Main Warehouse B1',       N'Main',        0, N'Primary storage zone B, aisle 1'),
    (N'MAIN-B2',    N'Main Warehouse B2',       N'Main',        0, N'Primary storage zone B, aisle 2'),
    (N'SHIP',       N'Shipping Staging Area',   N'Shipping',    0, N'Outbound shipping staging');
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.HoldingLocation WHERE LocationCode = N'HOLD-01')
BEGIN
    INSERT INTO dbo.HoldingLocation (LocationCode, LocationName, Description)
    VALUES
    (N'HOLD-01', N'Holding Bay 1', N'Customer order accumulation bay 1'),
    (N'HOLD-02', N'Holding Bay 2', N'Customer order accumulation bay 2'),
    (N'HOLD-03', N'Holding Bay 3', N'Customer order accumulation bay 3'),
    (N'HOLD-04', N'Holding Bay 4', N'Customer order accumulation bay 4'),
    (N'HOLD-05', N'Holding Bay 5', N'Customer order accumulation bay 5');
END;
GO


-- ============================================================================
-- 20.  STORED PROCEDURES  (Key utility procedures)
-- ============================================================================

-- ──────────────────────────────────────────────
-- 20.1  sp_GetNextSequenceNumber
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.sp_GetNextSequenceNumber', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetNextSequenceNumber;
GO

CREATE PROCEDURE dbo.sp_GetNextSequenceNumber
    @SequenceName   NVARCHAR(50),
    @NextNumber     NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CurrentVal INT, @Prefix NVARCHAR(10), @PadWidth INT, @Increment INT;

    UPDATE  dbo.NumberSequence
    SET     CurrentValue = CurrentValue + IncrementBy
    WHERE   SequenceName = @SequenceName;

    SELECT  @CurrentVal = CurrentValue,
            @Prefix     = Prefix,
            @PadWidth   = PadWidth
    FROM    dbo.NumberSequence
    WHERE   SequenceName = @SequenceName;

    IF @CurrentVal IS NULL
    BEGIN
        RAISERROR(N'Sequence "%s" not found.', 16, 1, @SequenceName);
        RETURN;
    END;

    SET @NextNumber = @Prefix + N'-' + RIGHT(REPLICATE(N'0', @PadWidth) + CAST(@CurrentVal AS NVARCHAR(20)), @PadWidth);
END;
GO

-- ──────────────────────────────────────────────
-- 20.2  sp_CalculateSellingPrice
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.sp_CalculateSellingPrice', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_CalculateSellingPrice;
GO

CREATE PROCEDURE dbo.sp_CalculateSellingPrice
    @VendorCostEur  DECIMAL(18,4),
    @SellingPrice   DECIMAL(18,4)   OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Factor DECIMAL(18,6), @Markup DECIMAL(18,6);

    SELECT @Factor = CAST(ConfigValue AS DECIMAL(18,6))
    FROM   dbo.SystemConfiguration
    WHERE  ConfigKey = N'EuroConversionFactor';

    SELECT @Markup = CAST(ConfigValue AS DECIMAL(18,6))
    FROM   dbo.SystemConfiguration
    WHERE  ConfigKey = N'PricingMarkup';

    IF @Factor IS NULL OR @Markup IS NULL
    BEGIN
        RAISERROR(N'Pricing configuration missing. Ensure EuroConversionFactor and PricingMarkup are set.', 16, 1);
        RETURN;
    END;

    -- Formula: VendorCostEUR × EuroConversionFactor × Markup
    SET @SellingPrice = ROUND(@VendorCostEur * @Factor * @Markup, 4);
END;
GO

-- ──────────────────────────────────────────────
-- 20.3  sp_LogError
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.sp_LogError', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_LogError;
GO

CREATE PROCEDURE dbo.sp_LogError
    @Severity       NVARCHAR(20)    = N'Error',
    @Source          NVARCHAR(500)   = NULL,
    @UserName       NVARCHAR(100)   = NULL,
    @RequestPath    NVARCHAR(2000)  = NULL,
    @RequestMethod  NVARCHAR(10)    = NULL,
    @StatusCode     INT             = NULL,
    @ErrorMessage   NVARCHAR(MAX),
    @StackTrace     NVARCHAR(MAX)   = NULL,
    @InnerException NVARCHAR(MAX)   = NULL,
    @AdditionalData NVARCHAR(MAX)   = NULL,
    @CorrelationId  UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ErrorLogging
        (Severity, Source, MachineName, UserName, RequestPath, RequestMethod, StatusCode,
         ErrorMessage, StackTrace, InnerException, AdditionalData, CorrelationId)
    VALUES
        (@Severity, @Source, HOST_NAME(), @UserName, @RequestPath, @RequestMethod, @StatusCode,
         @ErrorMessage, @StackTrace, @InnerException, @AdditionalData, @CorrelationId);
END;
GO

-- ──────────────────────────────────────────────
-- 20.4  sp_InsertAuditLog
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.sp_InsertAuditLog', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_InsertAuditLog;
GO

CREATE PROCEDURE dbo.sp_InsertAuditLog
    @UserId         INT             = NULL,
    @UserName       NVARCHAR(100)   = NULL,
    @ActionType     NVARCHAR(50),
    @EntityName     NVARCHAR(128),
    @EntityId       NVARCHAR(50),
    @PropertyName   NVARCHAR(128)   = NULL,
    @OldValue       NVARCHAR(MAX)   = NULL,
    @NewValue       NVARCHAR(MAX)   = NULL,
    @Description    NVARCHAR(1000)  = NULL,
    @RelatedEntity  NVARCHAR(128)   = NULL,
    @RelatedEntityId NVARCHAR(50)   = NULL,
    @IpAddress      NVARCHAR(50)    = NULL,
    @CorrelationId  UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.AuditLog
        (UserId, UserName, ActionType, EntityName, EntityId, PropertyName,
         OldValue, NewValue, Description, RelatedEntity, RelatedEntityId,
         IpAddress, CorrelationId)
    VALUES
        (@UserId, @UserName, @ActionType, @EntityName, @EntityId, @PropertyName,
         @OldValue, @NewValue, @Description, @RelatedEntity, @RelatedEntityId,
         @IpAddress, @CorrelationId);
END;
GO


-- ============================================================================
-- 21.  VIEWS  (Commonly used queries)
-- ============================================================================

-- ──────────────────────────────────────────────
-- 21.1  vw_PartInventorySummary
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.vw_PartInventorySummary', N'V') IS NOT NULL
    DROP VIEW dbo.vw_PartInventorySummary;
GO

CREATE VIEW dbo.vw_PartInventorySummary
AS
SELECT
    p.PartId,
    p.PartNumber,
    p.Description,
    p.CategoryCode,
    p.QuantityOnHand,
    p.ReservedQuantity,
    p.AvailableQuantity,
    p.ReorderPoint,
    p.ReorderQuantity,
    p.IsNonStocked,
    p.LastVendorCostEur,
    p.SellingPriceUsd,
    p.PrimaryLocationId,
    wl.LocationCode     AS PrimaryLocationCode,
    wl.LocationName     AS PrimaryLocationName,
    p.IsMerged,
    p.IsActive,
    -- Flags
    CASE
        WHEN p.IsNonStocked = 0
         AND p.ReorderPoint IS NOT NULL
         AND p.AvailableQuantity <= p.ReorderPoint
        THEN 1 ELSE 0
    END AS NeedsReorder,
    -- Count open PO lines
    (SELECT ISNULL(SUM(pol.Quantity - pol.QuantityReceived), 0)
     FROM dbo.PurchaseOrderLine pol
     INNER JOIN dbo.PurchaseOrder po ON po.PurchaseOrderId = pol.PurchaseOrderId
     WHERE pol.PartId = p.PartId
       AND po.Status IN (N'Draft', N'Reviewed', N'SentToVendor', N'PartiallyReceived')
    ) AS QuantityOnOrder
FROM dbo.Part p
LEFT JOIN dbo.WarehouseLocation wl ON wl.LocationId = p.PrimaryLocationId
WHERE p.IsMerged = 0;
GO

-- ──────────────────────────────────────────────
-- 21.2  vw_CustomerOrderDashboard
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.vw_CustomerOrderDashboard', N'V') IS NOT NULL
    DROP VIEW dbo.vw_CustomerOrderDashboard;
GO

CREATE VIEW dbo.vw_CustomerOrderDashboard
AS
SELECT
    co.OrderId,
    co.OrderNumber,
    co.OrderType,
    co.Status,
    co.OrderDate,
    co.RequestedDeliveryDate,
    co.ShipmentPreference,
    co.SubTotal,
    co.TotalAmount,
    co.CustomerPONumber,
    c.CustomerId,
    c.CompanyName       AS CustomerName,
    cc.FullName         AS ContactName,
    cc.Email            AS ContactEmail,
    cc.Phone            AS ContactPhone,
    hl.LocationCode     AS HoldingLocationCode,
    -- Line item counts
    (SELECT COUNT(*) FROM dbo.CustomerOrderLine ol WHERE ol.OrderId = co.OrderId) AS LineCount,
    (SELECT ISNULL(SUM(ol.Quantity), 0) FROM dbo.CustomerOrderLine ol WHERE ol.OrderId = co.OrderId) AS TotalQuantity,
    (SELECT ISNULL(SUM(ol.QuantityShipped), 0) FROM dbo.CustomerOrderLine ol WHERE ol.OrderId = co.OrderId) AS TotalShipped
FROM dbo.CustomerOrder co
INNER JOIN dbo.Customer c ON c.CustomerId = co.CustomerId
LEFT JOIN dbo.CustomerContact cc ON cc.ContactId = co.ContactId
LEFT JOIN dbo.HoldingLocation hl ON hl.HoldingLocationId = co.HoldingLocationId;
GO

-- ──────────────────────────────────────────────
-- 21.3  vw_PurchasingQueueSummary
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.vw_PurchasingQueueSummary', N'V') IS NOT NULL
    DROP VIEW dbo.vw_PurchasingQueueSummary;
GO

CREATE VIEW dbo.vw_PurchasingQueueSummary
AS
SELECT
    pqi.QueueItemId,
    pqi.PartId,
    p.PartNumber,
    p.Description       AS PartDescription,
    pqi.SourceType,
    pqi.QuantityRequired,
    pqi.QuantityOrdered,
    (pqi.QuantityRequired - pqi.QuantityOrdered) AS QuantityRemaining,
    pqi.Status,
    pqi.RideManufacturer,
    pqi.RideModel,
    pqi.SerialNumber,
    pqi.SuggestedVendorId,
    vm.VendorName       AS SuggestedVendorName,
    pqi.SuggestedCostEur,
    -- Source order info
    pqi.SourceOrderLineId,
    col.OrderId         AS SourceOrderId,
    co.OrderNumber      AS SourceOrderNumber,
    cust.CompanyName    AS SourceCustomerName,
    pqi.CreatedDate
FROM dbo.PurchasingQueueItem pqi
INNER JOIN dbo.Part p ON p.PartId = pqi.PartId
LEFT JOIN dbo.VendorMaster vm ON vm.VendorId = pqi.SuggestedVendorId
LEFT JOIN dbo.CustomerOrderLine col ON col.OrderLineId = pqi.SourceOrderLineId
LEFT JOIN dbo.CustomerOrder co ON co.OrderId = col.OrderId
LEFT JOIN dbo.Customer cust ON cust.CustomerId = co.CustomerId
WHERE pqi.Status IN (N'Pending', N'PartiallyOrdered');
GO

-- ──────────────────────────────────────────────
-- 21.4  vw_QBSyncStatus
-- ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.vw_QBSyncStatus', N'V') IS NOT NULL
    DROP VIEW dbo.vw_QBSyncStatus;
GO

CREATE VIEW dbo.vw_QBSyncStatus
AS
SELECT
    q.SyncId,
    q.EntityType,
    q.EntityId,
    q.SyncDirection,
    q.SyncAction,
    q.Status,
    q.QBTxnID,
    q.RetryCount,
    q.MaxRetries,
    q.ErrorMessage,
    q.CreatedDate,
    q.ProcessedDate,
    CASE q.EntityType
        WHEN N'CustomerInvoice'
            THEN (SELECT ci.InvoiceNumber FROM dbo.CustomerInvoice ci WHERE ci.InvoiceId = q.EntityId)
        WHEN N'VendorBill'
            THEN (SELECT vi.InvoiceNumber FROM dbo.VendorInvoice vi WHERE vi.VendorInvoiceId = q.EntityId)
        WHEN N'Customer'
            THEN (SELECT c.CompanyName FROM dbo.Customer c WHERE c.CustomerId = q.EntityId)
        ELSE NULL
    END AS EntityDescription
FROM dbo.QBSyncQueue q;
GO


-- ============================================================================
-- 22.  DATABASE DOCUMENTATION / TABLE SUMMARY
-- ============================================================================
/*
╔══════════════════════════════════╤═════════════════════════════════════════════════════════════════╗
║ Table Name                       │ Purpose / Scope Section                                       ║
╠══════════════════════════════════╪═════════════════════════════════════════════════════════════════╣
║ UserMaster                       │ Application users & RBAC (Admin/Office/Purchasing/Warehouse)   ║
║ ErrorLogging                     │ Centralised application error & exception log                  ║
║ AuditLog                         │ Transaction / activity history (§23)                           ║
║ SystemConfiguration              │ Configurable settings: pricing factors, QB config, etc. (§5)   ║
║ VendorMaster                     │ Vendor/supplier master list                                    ║
║ Customer                         │ Customer master, synced from/to QuickBooks (§1, §20)           ║
║ CustomerContact                  │ Multiple contacts per customer (§1)                            ║
║ CustomerRide                     │ Ride tracking: manufacturer, model, serial# (§2)               ║
║ WarehouseLocation                │ Physical warehouse storage locations                           ║
║ HoldingLocation                  │ Customer order accumulation bays (§7)                          ║
║ Part                             │ Master inventory part with QOH, reserved, pricing (§5, §7, §8) ║
║ VendorSource                     │ Multi-vendor sourcing per part (§11)                           ║
║ PartRideApplication              │ Part ↔ Ride model M:N relationship (§12)                       ║
║ CustomerOrder                    │ Customer orders & quotes with full lifecycle (§3, §4, §6)      ║
║ CustomerOrderLine                │ Order line items with part, qty, pricing                       ║
║ InventoryReservation             │ Stock reservations linked to order lines (§7)                  ║
║ PurchasingQueueItem              │ Automated purchase requirements queue (§9)                     ║
║ PurchaseOrder                    │ Vendor purchase orders (§10)                                   ║
║ PurchaseOrderLine                │ PO line items with vendor part#, ride info                     ║
║ VendorInvoice                    │ AI-processed vendor invoices (§14)                             ║
║ VendorInvoiceLine                │ Vendor invoice line items with AI matching                     ║
║ VendorPartCrossReference         │ Confirmed AI vendor↔part mappings for future matching (§14)    ║
║ ReceivingRecord                  │ Physical receiving against POs (§15)                           ║
║ ReceivingLine                    │ Receiving line items with discrepancy tracking (§15, §22)      ║
║ Shipment                         │ Customer shipments with carrier/tracking (§17, §18)            ║
║ ShipmentLine                     │ Shipment line items                                            ║
║ ShipmentPackage                  │ UPS package dimensions/tracking (§18)                          ║
║ CustomerInvoice                  │ Customer invoices synced to QB (§19)                           ║
║ CustomerInvoiceLine              │ Invoice lines incl. non-inventory charges (§19)                ║
║ QBSyncQueue                      │ QuickBooks Desktop sync queue & status tracking (§20)          ║
║ PartMergeHistory                 │ Audit trail for part merge operations (§13)                    ║
║ NotificationQueue                │ In-app notifications & alerts                                  ║
║ FileAttachment                   │ Generic document/photo attachments                             ║
║ NumberSequence                   │ Auto-incrementing number generators for orders, POs, etc.      ║
╚══════════════════════════════════╧═════════════════════════════════════════════════════════════════╝

Total: 32 tables  ·  4 views  ·  4 stored procedures
*/

PRINT N'═══════════════════════════════════════════════════════════════════';
PRINT N'  Amusement Ride CRM Database Script completed successfully.';
PRINT N'  32 tables, 4 views, 4 stored procedures created.';
PRINT N'  Seed data inserted for SystemConfiguration, NumberSequence,';
PRINT N'  WarehouseLocation, HoldingLocation, and default Admin user.';
PRINT N'═══════════════════════════════════════════════════════════════════';
GO
