using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AmusementRideCRM.Services
{
    public class ShippingPackageDto
    {
        public int PackageId { get; set; }
        public int PackageNumber { get; set; }
        public decimal WeightLbs { get; set; }
        public decimal LengthIn { get; set; }
        public decimal WidthIn { get; set; }
        public decimal HeightIn { get; set; }
        public string PackagingType { get; set; } = "02"; // 02: Customer Supplied Package
        public decimal? InsuredValue { get; set; }
    }

    public class ShippingAddressDto
    {
        public string Name { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = "US";
    }

    public class ShippingRateRequest
    {
        public string Carrier { get; set; } = "UPS";
        public string ServiceType { get; set; } = "03"; // 03: UPS Ground
        public ShippingAddressDto ShipTo { get; set; } = new();
        public ShippingAddressDto? ShipFrom { get; set; }
        public List<ShippingPackageDto> Packages { get; set; } = new();
    }

    public class ShippingRateQuoteResult
    {
        public bool Success { get; set; }
        public string Carrier { get; set; } = "UPS";
        public string ServiceType { get; set; } = "03";
        public decimal TotalEstimatedCost { get; set; }
        public string Currency { get; set; } = "USD";
        public string? ErrorMessage { get; set; }
        public Dictionary<string, decimal>? RatesByService { get; set; }
    }

    public class PackageTrackingResult
    {
        public int PackageId { get; set; }
        public int PackageNumber { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string? LabelImageUrl { get; set; }
    }

    public class ShipmentDispatchRequest
    {
        public int ShipmentId { get; set; }
        public string ShipmentNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = "UPS";
        public string ServiceType { get; set; } = "03";
        public ShippingAddressDto ShipTo { get; set; } = new();
        public ShippingAddressDto? ShipFrom { get; set; }
        public List<ShippingPackageDto> Packages { get; set; } = new();
    }

    public class ShipmentDispatchResult
    {
        public bool Success { get; set; }
        public string? TrackingNumber { get; set; }
        public string? ShippingLabelUrl { get; set; }
        public decimal? ActualCost { get; set; }
        public List<PackageTrackingResult> PackageTrackingNumbers { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public interface IShippingService
    {
        Task<ShippingRateQuoteResult> GetRateQuoteAsync(ShippingRateRequest request, CancellationToken cancellationToken = default);
        Task<ShipmentDispatchResult> CreateShipmentLabelAsync(ShipmentDispatchRequest request, CancellationToken cancellationToken = default);
    }
}
