using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Data;
using WarehouseWeb.Models;

namespace WarehouseWeb.Controllers
{
    public class ProductController : Controller
    {
        private readonly WarehouseDbContext db;
        private readonly InventoryManager inventoryManager;

        public ProductController(WarehouseDbContext db, InventoryManager inventoryManager)
        {
            this.db = db;
            this.inventoryManager = inventoryManager;
        }

        public IActionResult Index()
        {
            var products = db.Products
                .Include(p => p.Zone)
                .ThenInclude(z => z!.Warehouse)
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (AuthController.IsCollector(user))
                return Content("Доступ заборонено");

            LoadZones();
            return View();
        }

        [HttpPost]
        public IActionResult Create(string name, string category, decimal? basePrice, string unit, int? zoneId)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (AuthController.IsCollector(user))
                return Content("Доступ заборонено");

            var zone = zoneId.HasValue
                ? db.StorageZones.Include(z => z.Products).FirstOrDefault(z => z.Id == zoneId.Value)
                : null;

            if (zoneId.HasValue && zone == null)
            {
                ViewBag.Error = "Зона не існує.";
                LoadZones();
                return View();
            }

            var catalogPrice = basePrice ?? 0m;
            if (catalogPrice < 0)
            {
                ViewBag.Error = "Базова ціна не може бути від'ємною.";
                LoadZones(zoneId);
                return View();
            }

            try
            {
                var product = ProductFactory.CreateProduct(
                    name,
                    category,
                    0m,
                    catalogPrice,
                    unit,
                    zone,
                    user.Name
                );

                product.InventoryManager = inventoryManager;

                db.Products.Add(product);
                db.SaveChanges();

                inventoryManager.Notify(product, "Створено продукт");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                LoadZones(zoneId);
                return View();
            }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (AuthController.IsCollector(user))
                return Content("Доступ заборонено");

            var product = db.Products
                .Include(p => p.Zone)
                .FirstOrDefault(p => p.Id == id);

            if (product == null)
                return NotFound();

            LoadZones(product.StorageZoneId);
            return View(product);
        }

        [HttpPost]
        public IActionResult Edit(int id, string name, string category, decimal quantity, decimal price, string unit, int? zoneId)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (AuthController.IsCollector(user))
                return Content("Доступ заборонено");

            var product = db.Products
                .Include(p => p.Zone)
                .FirstOrDefault(p => p.Id == id);

            if (product == null)
                return NotFound();

            var zone = zoneId.HasValue
                ? db.StorageZones.Include(z => z.Products).FirstOrDefault(z => z.Id == zoneId.Value)
                : null;

            try
            {
                product.InventoryManager = inventoryManager;

                product.Update(name, category, quantity, price, unit, user.Name);

                if (zone == null)
                    product.RemoveZone(user.Name);
                else
                    product.AssignZone(zone, user.Name);

                db.SaveChanges();

                inventoryManager.Notify(product, "Оновлено продукт");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                LoadZones(zoneId);
                return View(product);
            }
        }

        public IActionResult Delete(int id)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null ||
                string.Equals(user.Role?.Name, RoleNames.Worker, StringComparison.OrdinalIgnoreCase))
            {
                return Content("Доступ заборонено");
            }

            var product = db.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                TempData["Error"] = "Товар не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var transaction = db.Database.BeginTransaction();

                var movementReferenceTables = GetTablesContainingColumn("MovementId")
                    .Where(table => !string.Equals(table, "Movements", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var table in movementReferenceTables)
                {
                    var deleteByMovementSql =
                        "DELETE FROM " + QuoteIdentifier(table) +
                        " WHERE \"MovementId\" IN (SELECT \"Id\" FROM \"Movements\" WHERE \"ProductId\" = {0});";

                    db.Database.ExecuteSqlRaw(deleteByMovementSql, id);
                }

                var productReferenceTables = GetTablesContainingColumn("ProductId")
                    .Where(table => !string.Equals(table, "Products", StringComparison.OrdinalIgnoreCase))
                    .Where(table => !string.Equals(table, "Movements", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var table in productReferenceTables)
                {
                    var deleteByProductSql =
                        "DELETE FROM " + QuoteIdentifier(table) + " WHERE \"ProductId\" = {0};";

                    db.Database.ExecuteSqlRaw(deleteByProductSql, id);
                }

                db.Database.ExecuteSqlRaw("DELETE FROM \"Movements\" WHERE \"ProductId\" = {0};", id);
                var affected = db.Database.ExecuteSqlRaw("DELETE FROM \"Products\" WHERE \"Id\" = {0};", id);

                transaction.Commit();

                TempData["Success"] = affected > 0
                    ? "Товар і всі пов'язані записи видалено."
                    : "Товар не знайдено.";
            }
            catch (Exception ex)
            {
                var details = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = $"Не вдалося видалити товар: {details}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult ClearAllOperationalData()
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null ||
                AuthController.IsCollector(user) ||
                string.Equals(user.Role?.Name, RoleNames.Worker, StringComparison.OrdinalIgnoreCase))
            {
                return Content("Доступ заборонено");
            }

            using var transaction = db.Database.BeginTransaction();

            try
            {
                db.Database.ExecuteSqlRaw(
                    """
                    DELETE FROM "SalesRecords";
                    DELETE FROM "Purchases";
                    DELETE FROM "Procurements";
                    DELETE FROM "Movements";
                    DELETE FROM "Products";
                    """);

                transaction.Commit();

                TempData["Success"] = "Операційні дані очищено: товари, рухи, закупівлі, продажі та заготівлі видалено.";
            }
            catch (Exception ex)
            {
                transaction.Rollback();

                var details = ex.InnerException?.Message;
                TempData["Error"] = string.IsNullOrWhiteSpace(details)
                    ? $"Не вдалося очистити дані: {ex.Message}"
                    : $"Не вдалося очистити дані: {details}";
            }

            return RedirectToAction(nameof(Index));
        }

        private void LoadZones(int? selectedZoneId = null)
        {
            ViewBag.Zones = db.StorageZones
                .Include(z => z.Warehouse)
                .OrderBy(z => z.Name)
                .ToList();

            ViewBag.SelectedZoneId = selectedZoneId;
        }

        private List<string> GetTablesContainingColumn(string columnName)
        {
            var tables = new List<string>();
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                var tableNames = new List<string>();

                using var tableCommand = connection.CreateCommand();
                tableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";

                using var tableReader = tableCommand.ExecuteReader();
                while (tableReader.Read())
                {
                    tableNames.Add(tableReader.GetString(0));
                }

                foreach (var tableName in tableNames)
                {
                    using var pragmaCommand = connection.CreateCommand();
                    pragmaCommand.CommandText = $"PRAGMA table_info({QuoteIdentifier(tableName)});";

                    using var pragmaReader = pragmaCommand.ExecuteReader();
                    while (pragmaReader.Read())
                    {
                        var currentColumn = pragmaReader["name"]?.ToString();
                        if (string.Equals(currentColumn, columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            tables.Add(tableName);
                            break;
                        }
                    }
                }
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }

            return tables;
        }

        private static string QuoteIdentifier(string identifier)
        {
            return $"\"{identifier.Replace("\"", "\"\"")}\"";
        }
    }
}
