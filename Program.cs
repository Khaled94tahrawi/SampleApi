using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using SampleApi.Data;
using SampleApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// OpenTelemetry
// ============================================================
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("sample-dotnet-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://192.168.1.231:14317");
        }));

// ============================================================
// Database - PostgreSQL
// ============================================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// ============================================================
// Prometheus Metrics
// ============================================================
app.UseHttpMetrics();
app.MapMetrics();

// ============================================================
// Health Check
// ============================================================
app.MapGet("/health", async (AppDbContext db) =>
{
    try
    {
        var databaseHealthy = await db.Database.CanConnectAsync();

        if (!databaseHealthy)
        {
            return Results.StatusCode(503);
        }

        return Results.Ok(new
        {
            status = "Healthy",
            database = "Connected"
        });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

// ============================================================
// GET /products
// Read all products
// ============================================================
app.MapGet("/products", async (AppDbContext db) =>
{
    var products = await db.Products.ToListAsync();

    return Results.Ok(products);
});

// ============================================================
// POST /products
// Create a new product
// ============================================================
app.MapPost("/products", async (Product product, AppDbContext db) =>
{
    db.Products.Add(product);

    await db.SaveChangesAsync();

    return Results.Created($"/products/{product.Id}", product);
});

// ============================================================
// PUT /products/{id}
// Update an existing product
// ============================================================
app.MapPut("/products/{id}", async (
    int id,
    Product updatedProduct,
    AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);

    if (product is null)
    {
        return Results.NotFound(new
        {
            message = "Product not found"
        });
    }

    product.Name = updatedProduct.Name;
    product.Price = updatedProduct.Price;

    await db.SaveChangesAsync();

    return Results.Ok(product);
});

// ============================================================
// DELETE /products/{id}
// Delete a product
// ============================================================
app.MapDelete("/products/{id}", async (
    int id,
    AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);

    if (product is null)
    {
        return Results.NotFound(new
        {
            message = "Product not found"
        });
    }

    db.Products.Remove(product);

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "Product deleted successfully"
    });
});

// ============================================================
// HEAD /products
// Return headers without a response body
// ============================================================
app.MapMethods("/products", new[] { "HEAD" }, (HttpResponse response) =>
{
    response.Headers["Allow"] =
        "GET, POST, PUT, DELETE, HEAD, OPTIONS";

    response.Headers["X-API-Method"] = "HEAD";

    return Results.Ok();
});

// ============================================================
// OPTIONS /products
// Tell the client which methods are supported
// ============================================================
app.MapMethods("/products", new[] { "OPTIONS" }, (HttpResponse response) =>
{
    response.Headers["Allow"] =
        "GET, POST, PUT, DELETE, HEAD, OPTIONS";

    response.Headers["Access-Control-Allow-Methods"] =
        "GET, POST, PUT, DELETE, HEAD, OPTIONS";

    response.Headers["Access-Control-Allow-Headers"] =
        "Content-Type, Authorization";

    response.Headers["Access-Control-Allow-Origin"] = "*";

    response.Headers["X-API-Method"] = "OPTIONS";

    return Results.Ok(new
    {
        message = "OPTIONS request successful"
    });
});

// ============================================================
// Static files
// ============================================================
app.UseDefaultFiles();
app.UseStaticFiles();

// ============================================================
// Start application
// ============================================================
app.Run();
