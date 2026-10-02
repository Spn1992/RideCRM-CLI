using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Configure EF Core with In-Memory Database for testing/mocking
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("AmusementRideCRM"));

var app = builder.Build();

// Seed mock data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// Serve static files
app.UseStaticFiles();

app.MapRazorPages();

app.Run();
