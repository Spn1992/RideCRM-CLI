using Microsoft.EntityFrameworkCore;
using SoapCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using AmusementRideCRM.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditUserProvider, AuditUserProvider>();
builder.Services.AddScoped<IOrderExceptionValidationService, OrderExceptionValidationService>();
builder.Services.AddScoped<IPurchasingQueueAggregationService, PurchasingQueueAggregationService>();
builder.Services.AddScoped<IPartMergeService, PartMergeService>();
builder.Services.AddScoped<ICurrencyExchangeService, CurrencyExchangeService>();
builder.Services.AddScoped<IVendorInvoiceVarianceService, VendorInvoiceVarianceService>();

// Configure EF Core with In-Memory Database for testing/mocking
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("AmusementRideCRM"));

// Register SoapCore & QB Sync services
builder.Services.AddSoapCore();
builder.Services.AddSingleton<IQBSessionManager, QBSessionManager>();
builder.Services.AddScoped<IQbXmlSerializer, QbXmlSerializer>();
builder.Services.AddScoped<IQBSyncQueueService, QBSyncQueueService>();
builder.Services.AddScoped<IQBWebConnectorSvc, QBWebConnectorSvc>();

var app = builder.Build();

// Seed mock data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (!context.SystemConfiguration.Any())
    {
        context.SystemConfiguration.AddRange(
            new SystemConfiguration { ConfigKey = "MinimumGrossMargin", ConfigValue = "0.20", DataType = "Decimal", Description = "Minimum Gross Margin Threshold (20%)", Category = "Pricing", IsEditable = true },
            new SystemConfiguration { ConfigKey = "EuroConversionFactor", ConfigValue = "1.10", DataType = "Decimal", Description = "EUR to USD conversion factor", Category = "Pricing", IsEditable = true },
            new SystemConfiguration { ConfigKey = "PricingMarkup", ConfigValue = "1.40", DataType = "Decimal", Description = "Markup multiplier", Category = "Pricing", IsEditable = true }
        );
        context.SaveChanges();
    }

    if (!context.CustomerOrder.Any())
    {
        context.CustomerOrder.AddRange(
            new CustomerOrder { OrderId = 1, OrderNumber = "ORD-001", OrderType = "Quote", Status = "Draft", OrderDate = DateTime.Now.AddDays(-2), TotalAmount = 1500.50m },
            new CustomerOrder { OrderId = 2, OrderNumber = "ORD-002", OrderType = "Order", Status = "Confirmed", OrderDate = DateTime.Now.AddDays(-5), TotalAmount = 450.00m },
            new CustomerOrder { OrderId = 3, OrderNumber = "ORD-003", OrderType = "Order", Status = "Shipped", OrderDate = DateTime.Now.AddDays(-10), TotalAmount = 3200.75m }
        );
        context.SaveChanges();
    }

    if (!context.Part.Any())
    {
        context.Part.AddRange(
            new Part { PartId = 1, PartNumber = "P-100", Description = "Widget A", UnitOfMeasure = "Ea" },
            new Part { PartId = 2, PartNumber = "P-101", Description = "Widget B", UnitOfMeasure = "Ea" }
        );
        context.SaveChanges();
    }

    if (!context.CurrencyExchangeRate.Any())
    {
        context.CurrencyExchangeRate.AddRange(
            new CurrencyExchangeRate { SourceCurrency = "EUR", TargetCurrency = "USD", ExchangeRate = 1.08m, EffectiveDate = DateTime.UtcNow.AddYears(-1), CreatedDate = DateTime.UtcNow, CreatedBy = "System" }
        );
        context.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.UseSoapEndpoint<IQBWebConnectorSvc>("/QBWebConnectorSvc.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);
});

// Serve static files
app.UseStaticFiles();

app.MapRazorPages();

app.Run();
