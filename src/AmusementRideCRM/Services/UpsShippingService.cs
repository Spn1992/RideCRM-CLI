using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public class UpsShippingService : IShippingService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;

        public UpsShippingService(HttpClient httpClient, ApplicationDbContext context)
        {
            _httpClient = httpClient ?? new HttpClient();
            _context = context;
        }

        private async Task<(string clientId, string clientSecret, string accountNumber, string baseUrl, int timeoutSeconds)> GetUpsConfigAsync()
        {
            var configs = await _context.SystemConfiguration
                .Where(c => c.ConfigKey.StartsWith("UPS_"))
                .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue);

            string clientId = configs.GetValueOrDefault("UPS_ClientId") ?? "MOCK_CLIENT_ID";
            string clientSecret = configs.GetValueOrDefault("UPS_ClientSecret") ?? "MOCK_CLIENT_SECRET";
            string accountNumber = configs.GetValueOrDefault("UPS_AccountNumber") ?? "A12345";
            string baseUrl = configs.GetValueOrDefault("UPS_BaseUrl") ?? "https://wwwcie.ups.com";
            
            int timeoutSeconds = 10;
            if (configs.TryGetValue("UPS_ApiTimeoutSeconds", out var timeoutStr) && int.TryParse(timeoutStr, out var parsedTimeout))
            {
                timeoutSeconds = parsedTimeout;
            }

            return (clientId, clientSecret, accountNumber, baseUrl.TrimEnd('/'), timeoutSeconds);
        }

        private async Task<string?> GetOAuthTokenAsync(string baseUrl, string clientId, string clientSecret, int timeoutSeconds, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(clientId) || clientId == "MOCK_CLIENT_ID" || clientId == "TEST_MOCK_KEY")
            {
                return "MOCK_UPS_OAUTH_BEARER_TOKEN";
            }

            var tokenEndpoint = $"{baseUrl}/security/v1/oauth/token";
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            int maxRetries = 2;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
                    var authBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
                    
                    var contentList = new List<KeyValuePair<string, string>>
                    {
                        new("grant_type", "client_credentials")
                    };
                    request.Content = new FormUrlEncodedContent(contentList);

                    var response = await _httpClient.SendAsync(request, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(cts.Token);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("access_token", out var accessTokenElem))
                        {
                            return accessTokenElem.GetString();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception) when (attempt < maxRetries)
                {
                    await Task.Delay(200, cancellationToken);
                }
            }

            return null;
        }

        public async Task<ShippingRateQuoteResult> GetRateQuoteAsync(ShippingRateRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || request.Packages == null || !request.Packages.Any())
            {
                return new ShippingRateQuoteResult
                {
                    Success = false,
                    ErrorMessage = "At least one package with weight and dimensions is required for a rate quote."
                };
            }

            foreach (var pkg in request.Packages)
            {
                if (pkg.WeightLbs <= 0 || pkg.LengthIn <= 0 || pkg.WidthIn <= 0 || pkg.HeightIn <= 0)
                {
                    return new ShippingRateQuoteResult
                    {
                        Success = false,
                        ErrorMessage = $"Package {pkg.PackageNumber} must have positive dimensions and weight (Weight > 0, L/W/H > 0)."
                    };
                }
            }

            var (clientId, clientSecret, accountNumber, baseUrl, timeoutSeconds) = await GetUpsConfigAsync();

            if (clientId == "MOCK_CLIENT_ID" || clientId == "TEST_MOCK_KEY")
            {
                return GenerateMockRateQuote(request);
            }

            try
            {
                var token = await GetOAuthTokenAsync(baseUrl, clientId, clientSecret, timeoutSeconds, cancellationToken);
                if (string.IsNullOrEmpty(token))
                {
                    return new ShippingRateQuoteResult
                    {
                        Success = false,
                        ErrorMessage = "UPS OAuth 2.0 authentication failed. Check credentials in SystemConfiguration."
                    };
                }

                var rateEndpoint = $"{baseUrl}/api/rating/v1/shop";
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var payload = BuildUpsRatingPayload(request, accountNumber);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, rateEndpoint);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                httpRequest.Headers.Add("transId", Guid.NewGuid().ToString());
                httpRequest.Headers.Add("transactionSrc", "RideCRM");
                httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    var errContent = await response.Content.ReadAsStringAsync(cts.Token);
                    return new ShippingRateQuoteResult
                    {
                        Success = false,
                        ErrorMessage = $"UPS Rating API error ({response.StatusCode}): {errContent}"
                    };
                }

                var respJson = await response.Content.ReadAsStringAsync(cts.Token);
                return ParseUpsRatingResponse(respJson, request.ServiceType);
            }
            catch (OperationCanceledException)
            {
                return new ShippingRateQuoteResult
                {
                    Success = false,
                    ErrorMessage = $"UPS Rating API timed out after {timeoutSeconds} seconds."
                };
            }
            catch (Exception ex)
            {
                return new ShippingRateQuoteResult
                {
                    Success = false,
                    ErrorMessage = $"UPS Rating API request failed: {ex.Message}"
                };
            }
        }

        public async Task<ShipmentDispatchResult> CreateShipmentLabelAsync(ShipmentDispatchRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || request.Packages == null || !request.Packages.Any())
            {
                return new ShipmentDispatchResult
                {
                    Success = false,
                    ErrorMessage = "Cannot dispatch shipment without package details (dimensions and weight required)."
                };
            }

            foreach (var pkg in request.Packages)
            {
                if (pkg.WeightLbs <= 0 || pkg.LengthIn <= 0 || pkg.WidthIn <= 0 || pkg.HeightIn <= 0)
                {
                    return new ShipmentDispatchResult
                    {
                        Success = false,
                        ErrorMessage = $"Package #{pkg.PackageNumber} has invalid dimensions/weight. Length, Width, Height, and Weight must be greater than 0."
                    };
                }
            }

            var (clientId, clientSecret, accountNumber, baseUrl, timeoutSeconds) = await GetUpsConfigAsync();

            if (clientId == "MOCK_CLIENT_ID" || clientId == "TEST_MOCK_KEY")
            {
                return GenerateMockDispatchResult(request);
            }

            try
            {
                var token = await GetOAuthTokenAsync(baseUrl, clientId, clientSecret, timeoutSeconds, cancellationToken);
                if (string.IsNullOrEmpty(token))
                {
                    return new ShipmentDispatchResult
                    {
                        Success = false,
                        ErrorMessage = "UPS OAuth 2.0 authentication failed. Check credentials in SystemConfiguration."
                    };
                }

                var shipEndpoint = $"{baseUrl}/api/shipments/v1/ship";
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var payload = BuildUpsShipmentPayload(request, accountNumber);

                int maxRetries = 2;
                HttpResponseMessage? response = null;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        var httpRequest = new HttpRequestMessage(HttpMethod.Post, shipEndpoint);
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        httpRequest.Headers.Add("transId", Guid.NewGuid().ToString());
                        httpRequest.Headers.Add("transactionSrc", "RideCRM");
                        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                        response = await _httpClient.SendAsync(httpRequest, cts.Token);
                        if (response.IsSuccessStatusCode) break;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception) when (attempt < maxRetries)
                    {
                        await Task.Delay(250, cancellationToken);
                    }
                }

                if (response == null || !response.IsSuccessStatusCode)
                {
                    var errBody = response != null ? await response.Content.ReadAsStringAsync(cts.Token) : "No response";
                    return new ShipmentDispatchResult
                    {
                        Success = false,
                        ErrorMessage = $"UPS Shipping API error ({(response?.StatusCode.ToString() ?? "NoResponse")}): {errBody}"
                    };
                }

                var respJson = await response.Content.ReadAsStringAsync(cts.Token);
                return ParseUpsShipmentResponse(respJson, request);
            }
            catch (OperationCanceledException)
            {
                return new ShipmentDispatchResult
                {
                    Success = false,
                    ErrorMessage = $"UPS Shipping API timed out after {timeoutSeconds} seconds."
                };
            }
            catch (Exception ex)
            {
                return new ShipmentDispatchResult
                {
                    Success = false,
                    ErrorMessage = $"UPS Shipping API connection failed: {ex.Message}"
                };
            }
        }

        private object BuildUpsRatingPayload(ShippingRateRequest request, string accountNumber)
        {
            var shipTo = request.ShipTo;
            var shipFrom = request.ShipFrom ?? new ShippingAddressDto { Name = "RideCRM Warehouse", AddressLine1 = "100 Industrial Pkwy", City = "Orlando", State = "FL", PostalCode = "32801", Country = "US" };

            var packages = request.Packages.Select(p => new
            {
                PackagingType = new { Code = p.PackagingType ?? "02" },
                Dimensions = new
                {
                    UnitOfMeasurement = new { Code = "IN" },
                    Length = p.LengthIn.ToString("0.##"),
                    Width = p.WidthIn.ToString("0.##"),
                    Height = p.HeightIn.ToString("0.##")
                },
                PackageWeight = new
                {
                    UnitOfMeasurement = new { Code = "LBS" },
                    Weight = p.WeightLbs.ToString("0.##")
                }
            }).ToList();

            return new
            {
                RateRequest = new
                {
                    Request = new { RequestOption = "Shop" },
                    Shipment = new
                    {
                        Shipper = new
                        {
                            Name = shipFrom.Name,
                            ShipperNumber = accountNumber,
                            Address = new
                            {
                                AddressLine = new[] { shipFrom.AddressLine1, shipFrom.AddressLine2 }.Where(s => !string.IsNullOrEmpty(s)).ToArray(),
                                City = shipFrom.City,
                                StateProvinceCode = shipFrom.State,
                                PostalCode = shipFrom.PostalCode,
                                CountryCode = shipFrom.Country
                            }
                        },
                        ShipTo = new
                        {
                            Name = shipTo.Name,
                            Address = new
                            {
                                AddressLine = new[] { shipTo.AddressLine1, shipTo.AddressLine2 }.Where(s => !string.IsNullOrEmpty(s)).ToArray(),
                                City = shipTo.City,
                                StateProvinceCode = shipTo.State,
                                PostalCode = shipTo.PostalCode,
                                CountryCode = shipTo.Country
                            }
                        },
                        Package = packages
                    }
                }
            };
        }

        private object BuildUpsShipmentPayload(ShipmentDispatchRequest request, string accountNumber)
        {
            var shipTo = request.ShipTo;
            var shipFrom = request.ShipFrom ?? new ShippingAddressDto { Name = "RideCRM Warehouse", AddressLine1 = "100 Industrial Pkwy", City = "Orlando", State = "FL", PostalCode = "32801", Country = "US" };

            var packages = request.Packages.Select(p => new
            {
                Packaging = new { Code = p.PackagingType ?? "02" },
                Dimensions = new
                {
                    UnitOfMeasurement = new { Code = "IN" },
                    Length = p.LengthIn.ToString("0.##"),
                    Width = p.WidthIn.ToString("0.##"),
                    Height = p.HeightIn.ToString("0.##")
                },
                PackageWeight = new
                {
                    UnitOfMeasurement = new { Code = "LBS" },
                    Weight = p.WeightLbs.ToString("0.##")
                }
            }).ToList();

            return new
            {
                ShipmentRequest = new
                {
                    Request = new { RequestOption = "nonvalidate" },
                    Shipment = new
                    {
                        Description = $"Order Shipment {request.ShipmentNumber}",
                        Shipper = new
                        {
                            Name = shipFrom.Name,
                            AttentionName = "Dispatch Dept",
                            ShipperNumber = accountNumber,
                            Address = new
                            {
                                AddressLine = new[] { shipFrom.AddressLine1, shipFrom.AddressLine2 }.Where(s => !string.IsNullOrEmpty(s)).ToArray(),
                                City = shipFrom.City,
                                StateProvinceCode = shipFrom.State,
                                PostalCode = shipFrom.PostalCode,
                                CountryCode = shipFrom.Country
                            }
                        },
                        ShipTo = new
                        {
                            Name = shipTo.Name,
                            Address = new
                            {
                                AddressLine = new[] { shipTo.AddressLine1, shipTo.AddressLine2 }.Where(s => !string.IsNullOrEmpty(s)).ToArray(),
                                City = shipTo.City,
                                StateProvinceCode = shipTo.State,
                                PostalCode = shipTo.PostalCode,
                                CountryCode = shipTo.Country
                            }
                        },
                        Service = new
                        {
                            Code = request.ServiceType ?? "03",
                            Description = GetServiceName(request.ServiceType)
                        },
                        PaymentInformation = new
                        {
                            ShipmentCharge = new
                            {
                                Type = "01",
                                BillShipper = new { AccountNumber = accountNumber }
                            }
                        },
                        Package = packages,
                        LabelSpecification = new
                        {
                            LabelImageFormat = new { Code = "GIF" }
                        }
                    }
                }
            };
        }

        private ShippingRateQuoteResult ParseUpsRatingResponse(string json, string requestedService)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("RateResponse", out var rateResp) &&
                    rateResp.TryGetProperty("RatedShipment", out var ratedShipmentElem))
                {
                    decimal totalCost = 0m;
                    var ratesByService = new Dictionary<string, decimal>();

                    if (ratedShipmentElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ship in ratedShipmentElem.EnumerateArray())
                        {
                            var serviceCode = ship.GetProperty("Service").GetProperty("Code").GetString() ?? "03";
                            var charge = parseCharge(ship);
                            ratesByService[serviceCode] = charge;
                            if (serviceCode == requestedService || totalCost == 0m)
                            {
                                totalCost = charge;
                            }
                        }
                    }
                    else if (ratedShipmentElem.ValueKind == JsonValueKind.Object)
                    {
                        var serviceCode = ratedShipmentElem.GetProperty("Service").GetProperty("Code").GetString() ?? "03";
                        totalCost = parseCharge(ratedShipmentElem);
                        ratesByService[serviceCode] = totalCost;
                    }

                    return new ShippingRateQuoteResult
                    {
                        Success = true,
                        Carrier = "UPS",
                        ServiceType = requestedService,
                        TotalEstimatedCost = totalCost,
                        Currency = "USD",
                        RatesByService = ratesByService
                    };
                }
            }
            catch (Exception ex)
            {
                return new ShippingRateQuoteResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to parse UPS Rating response: {ex.Message}"
                };
            }

            return new ShippingRateQuoteResult { Success = false, ErrorMessage = "Invalid UPS Rating response payload." };

            static decimal parseCharge(JsonElement elem)
            {
                if (elem.TryGetProperty("TotalCharges", out var totalChargeElem) &&
                    totalChargeElem.TryGetProperty("MonetaryValue", out var valElem) &&
                    decimal.TryParse(valElem.GetString(), out var val))
                {
                    return val;
                }
                return 0m;
            }
        }

        private ShipmentDispatchResult ParseUpsShipmentResponse(string json, ShipmentDispatchRequest request)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("ShipmentResponse", out var shipResp) &&
                    shipResp.TryGetProperty("ShipmentResults", out var results))
                {
                    var masterTracking = results.GetProperty("ShipmentIdentificationNumber").GetString();
                    decimal totalCost = 0m;
                    if (results.TryGetProperty("ShipmentCharges", out var charges) &&
                        charges.TryGetProperty("TotalCharges", out var totalCharges) &&
                        totalCharges.TryGetProperty("MonetaryValue", out var costElem) &&
                        decimal.TryParse(costElem.GetString(), out var costVal))
                    {
                        totalCost = costVal;
                    }

                    var pkgResults = new List<PackageTrackingResult>();
                    if (results.TryGetProperty("PackageResults", out var packagesElem))
                    {
                        int index = 0;
                        var packageList = request.Packages;

                        IEnumerable<JsonElement> enumPackages = packagesElem.ValueKind == JsonValueKind.Array
                            ? packagesElem.EnumerateArray()
                            : new[] { packagesElem };

                        foreach (var pkgElem in enumPackages)
                        {
                            var pkgTracking = pkgElem.GetProperty("TrackingNumber").GetString() ?? masterTracking ?? string.Empty;
                            string? labelUrl = null;
                            if (pkgElem.TryGetProperty("ShippingLabel", out var labelElem) &&
                                labelElem.TryGetProperty("GraphicImage", out var graphicElem))
                            {
                                labelUrl = $"data:image/gif;base64,{graphicElem.GetString()}";
                            }

                            int pkgId = index < packageList.Count ? packageList[index].PackageId : 0;
                            int pkgNum = index < packageList.Count ? packageList[index].PackageNumber : index + 1;

                            pkgResults.Add(new PackageTrackingResult
                            {
                                PackageId = pkgId,
                                PackageNumber = pkgNum,
                                TrackingNumber = pkgTracking,
                                LabelImageUrl = labelUrl
                            });
                            index++;
                        }
                    }

                    var primaryLabel = pkgResults.FirstOrDefault()?.LabelImageUrl ?? $"https://www.ups.com/label/mock/{masterTracking}";

                    return new ShipmentDispatchResult
                    {
                        Success = true,
                        TrackingNumber = masterTracking,
                        ShippingLabelUrl = primaryLabel,
                        ActualCost = totalCost > 0m ? totalCost : 25.50m,
                        PackageTrackingNumbers = pkgResults
                    };
                }
            }
            catch (Exception ex)
            {
                return new ShipmentDispatchResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to parse UPS Shipping response: {ex.Message}"
                };
            }

            return new ShipmentDispatchResult { Success = false, ErrorMessage = "Invalid UPS Shipping response structure." };
        }

        private ShippingRateQuoteResult GenerateMockRateQuote(ShippingRateRequest request)
        {
            decimal totalWeight = request.Packages.Sum(p => p.WeightLbs);
            decimal baseRate = 12.50m + (totalWeight * 0.85m);
            if (request.ServiceType == "01") baseRate *= 2.2m; // Next Day Air
            else if (request.ServiceType == "02") baseRate *= 1.5m; // 2nd Day Air

            baseRate = Math.Round(baseRate, 2);

            return new ShippingRateQuoteResult
            {
                Success = true,
                Carrier = "UPS",
                ServiceType = request.ServiceType ?? "03",
                TotalEstimatedCost = baseRate,
                Currency = "USD",
                RatesByService = new Dictionary<string, decimal>
                {
                    { "03", Math.Round(12.50m + (totalWeight * 0.85m), 2) }, // Ground
                    { "02", Math.Round((12.50m + (totalWeight * 0.85m)) * 1.5m, 2) }, // 2nd Day Air
                    { "01", Math.Round((12.50m + (totalWeight * 0.85m)) * 2.2m, 2) }  // Next Day Air
                }
            };
        }

        private ShipmentDispatchResult GenerateMockDispatchResult(ShipmentDispatchRequest request)
        {
            var randomSuffix = new Random().Next(1000000, 9999999);
            string masterTracking = $"1Z999999{request.ServiceType ?? "03"}{randomSuffix}";
            decimal totalWeight = request.Packages.Sum(p => p.WeightLbs);
            decimal actualCost = Math.Round(14.00m + (totalWeight * 0.90m), 2);

            var pkgResults = new List<PackageTrackingResult>();
            for (int i = 0; i < request.Packages.Count; i++)
            {
                var pkg = request.Packages[i];
                string pkgTracking = i == 0 ? masterTracking : $"1Z999999{request.ServiceType ?? "03"}{randomSuffix + i}";
                pkgResults.Add(new PackageTrackingResult
                {
                    PackageId = pkg.PackageId,
                    PackageNumber = pkg.PackageNumber > 0 ? pkg.PackageNumber : i + 1,
                    TrackingNumber = pkgTracking,
                    LabelImageUrl = $"https://www.ups.com/shipping/label/{pkgTracking}.gif"
                });
            }

            return new ShipmentDispatchResult
            {
                Success = true,
                TrackingNumber = masterTracking,
                ShippingLabelUrl = pkgResults.FirstOrDefault()?.LabelImageUrl,
                ActualCost = actualCost,
                PackageTrackingNumbers = pkgResults
            };
        }

        private static string GetServiceName(string? code) => code switch
        {
            "01" => "UPS Next Day Air",
            "02" => "UPS 2nd Day Air",
            "03" => "UPS Ground",
            "12" => "UPS 3 Day Select",
            _ => "UPS Ground"
        };
    }
}
