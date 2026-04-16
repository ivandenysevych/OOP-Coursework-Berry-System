using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Controllers;
using WarehouseWeb.Data;
using WarehouseWeb.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();

var dbDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dbDirectory);

var dbPath = Path.Combine(dbDirectory, "warehouse.db");
var connectionString = $"Data Source={dbPath}";

builder.Services.AddDbContext<WarehouseDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddSingleton(_ => WarehouseManagementSystem.Instance);
builder.Services.AddSingleton<InventoryManager>();
builder.Services.AddSingleton<AnalyticsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

EnsureDatabaseAndSeed(app.Services, dbPath);

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();

app.Use(async (context, next) =>
{
    if (IsPublicPath(context.Request.Path))
    {
        await next();
        return;
    }

    var currentUser = AuthController.GetCurrentUser(context);
    if (currentUser == null)
    {
        var returnUrl = $"{context.Request.Path}{context.Request.QueryString}";
        context.Response.Redirect($"/Auth/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    if (AuthController.IsCollector(currentUser) && !IsCollectorAllowedPath(context.Request.Path))
    {
        context.Response.Redirect("/Home/CollectorDashboard");
        return;
    }

    await next();
});

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

static void EnsureDatabaseAndSeed(IServiceProvider services, string dbPath)
{
    if (TryInitializeAndSeed(services, out var error))
    {
        return;
    }

    if (IsCorruptedDatabaseError(error))
    {
        ResetCorruptedDatabaseFile(dbPath);

        if (TryInitializeAndSeed(services, out var retryError))
        {
            return;
        }

        throw retryError ?? new InvalidOperationException("Failed to reinitialize database after reset.");
    }

    throw error ?? new InvalidOperationException("Failed to initialize database.");
}

static bool TryInitializeAndSeed(IServiceProvider services, out Exception? error)
{
    try
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        db.Database.EnsureCreated();
        SeedData.EnsureSeeded(db);
        EnsureDatabaseIntegrity(db);

        var inventoryManager = scope.ServiceProvider.GetRequiredService<InventoryManager>();
        var analyticsService = scope.ServiceProvider.GetRequiredService<AnalyticsService>();
        inventoryManager.Attach(analyticsService);

        error = null;
        return true;
    }
    catch (Exception ex)
    {
        error = ex;
        return false;
    }
}

static void ResetCorruptedDatabaseFile(string dbPath)
{
    if (File.Exists(dbPath))
    {
        File.Delete(dbPath);
    }
}

static void EnsureDatabaseIntegrity(WarehouseDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        connection.Open();
    }

    try
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";

        var result = command.ExecuteScalar()?.ToString()?.Trim();
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"database disk image is malformed (quick_check: {result ?? "unknown"})");
        }
    }
    finally
    {
        if (shouldClose)
        {
            connection.Close();
        }
    }
}

static bool IsCorruptedDatabaseError(Exception? ex)
{
    while (ex != null)
    {
        if (ex is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 11)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(ex.Message) &&
            ex.Message.Contains("database disk image is malformed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        ex = ex.InnerException;
    }

    return false;
}

static bool IsPublicPath(PathString path)
{
    if (!path.HasValue)
        return true;

    var value = path.Value ?? string.Empty;

    if (value == "/" ||
        value.StartsWith("/Home", StringComparison.OrdinalIgnoreCase))
        return true;

    if (value.StartsWith("/Auth", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("/css") ||
        value.StartsWith("/js") ||
        value.StartsWith("/lib") ||
        value.StartsWith("/images") ||
        value.StartsWith("/_"))
        return true;

    return false;
}

static bool IsCollectorAllowedPath(PathString path)
{
    if (!path.HasValue)
        return true;

    var value = path.Value ?? string.Empty;

    if (value.StartsWith("/Procurement", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("/Product", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("/Home/CollectorDashboard", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("/Home/Dashboard", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return false;
}

