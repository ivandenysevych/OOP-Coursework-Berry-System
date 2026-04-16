using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Data;
using WarehouseWeb.Models;

namespace WarehouseWeb.Controllers
{
    public class MovementController : Controller
    {
        private readonly WarehouseDbContext db;
        private readonly InventoryManager inventoryManager;

        public MovementController(WarehouseDbContext db, InventoryManager inventoryManager)
        {
            this.db = db;
            this.inventoryManager = inventoryManager;
        }

        public IActionResult Index()
        {
            var movements = db.Movements
                .Include(m => m.Product)
                .Include(m => m.FromZone)
                .ThenInclude(z => z!.Warehouse)
                .ThenInclude(w => w!.Company)
                .Include(m => m.ToZone)
                .ThenInclude(z => z!.Warehouse)
                .ThenInclude(w => w!.Company)
                .OrderByDescending(m => m.Date)
                .ToList();

            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(movements);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (AuthController.GetCurrentUser(HttpContext) == null)
                return RedirectToAction("Login", "Auth");

            LoadData();
            return View();
        }

        [HttpPost]
        public IActionResult Create(
            string movementType,
            decimal quantity,
            int? productId,
            int? fromZoneId,
            int? toZoneId,
            int? destinationCompanyId,
            bool createNewProduct,
            string? newProductName,
            string? newProductCategory,
            decimal? newProductPrice,
            string? newProductUnit)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!Enum.TryParse<MovementType>(movementType, true, out var type))
            {
                ViewBag.Error = "Невірно вказаний тип руху.";
                LoadData();
                return View();
            }

            var fromZone = fromZoneId.HasValue
                ? db.StorageZones
                    .Include(z => z.Products)
                    .Include(z => z.Warehouse)
                    .ThenInclude(w => w!.Company)
                    .FirstOrDefault(z => z.Id == fromZoneId.Value)
                : null;

            var toZone = toZoneId.HasValue
                ? db.StorageZones
                    .Include(z => z.Products)
                    .Include(z => z.Warehouse)
                    .ThenInclude(w => w!.Company)
                    .FirstOrDefault(z => z.Id == toZoneId.Value)
                : null;

            Product? product;

            if (createNewProduct)
            {
                if (type != MovementType.Add)
                {
                    ViewBag.Error = "Новий товар можна створити лише для «Надходження».";
                    LoadData();
                    return View();
                }

                if (string.IsNullOrWhiteSpace(newProductName) ||
                    string.IsNullOrWhiteSpace(newProductCategory) ||
                    string.IsNullOrWhiteSpace(newProductUnit) ||
                    !newProductPrice.HasValue)
                {
                    ViewBag.Error = "Заповни всі поля нового товару.";
                    LoadData();
                    return View();
                }

                var newProductZone = toZone ?? fromZone;

                try
                {
                    product = ProductFactory.CreateProduct(
                        newProductName.Trim(),
                        newProductCategory.Trim(),
                        0,
                        newProductPrice.Value,
                        newProductUnit.Trim(),
                        newProductZone,
                        user.Name
                    );

                    product.InventoryManager = inventoryManager;

                    db.Products.Add(product);
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    ViewBag.Error = ex.Message;
                    LoadData();
                    return View();
                }
            }
            else
            {
                if (!productId.HasValue)
                {
                    ViewBag.Error = "Оберіть товар.";
                    LoadData();
                    return View();
                }

                product = db.Products
                    .Include(p => p.Zone)
                    .ThenInclude(z => z!.Warehouse)
                    .ThenInclude(w => w!.Company)
                    .FirstOrDefault(p => p.Id == productId.Value);

                if (product == null)
                {
                    ViewBag.Error = "Товар не знайдено.";
                    LoadData();
                    return View();
                }

                product.InventoryManager = inventoryManager;

                if (fromZone == null)
                    fromZone = product.Zone;
            }

            if (type == MovementType.Move && toZone == null && !destinationCompanyId.HasValue)
            {
                ViewBag.Error = "Вкажи куди переміщати.";
                LoadData();
                return View();
            }

            if (type == MovementType.Move && fromZone != null && toZone != null && fromZone.Id == toZone.Id)
            {
                ViewBag.Error = "Зона відправлення і зона призначення мають бути різними.";
                LoadData();
                return View();
            }

            if (destinationCompanyId.HasValue)
            {
                var destinationCompany = db.Companies
                    .Include(c => c.Warehouses)
                    .FirstOrDefault(c => c.Id == destinationCompanyId.Value);

                if (destinationCompany == null)
                {
                    ViewBag.Error = "Обрана компанія призначення не знайдена.";
                    LoadData();
                    return View();
                }

                if (toZone == null)
                {
                    try
                    {
                        toZone = EnsureTransitZone(destinationCompany);
                    }
                    catch (Exception ex)
                    {
                        ViewBag.Error = ex.Message;
                        LoadData();
                        return View();
                    }
                }

                if (toZone.Warehouse?.CompanyId != destinationCompanyId.Value)
                {
                    ViewBag.Error = "Зона призначення не належить вибраній компанії.";
                    LoadData();
                    return View();
                }
            }

            try
            {
                var movement = new Movement(type, quantity, product, fromZone, toZone);

                inventoryManager.ExecuteMovement(movement, user.Name);

                db.Movements.Add(movement);
                db.SaveChanges();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                LoadData();
                return View();
            }
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (AuthController.IsCollector(user) ||
                string.Equals(user.Role?.Name, RoleNames.Worker, StringComparison.OrdinalIgnoreCase))
            {
                return Content("Доступ заборонено");
            }

            var movement = db.Movements
                .Include(m => m.Product)
                .Include(m => m.FromZone)
                .Include(m => m.ToZone)
                .FirstOrDefault(m => m.Id == id);

            if (movement == null)
            {
                TempData["Error"] = "Рух не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            var linkedPurchase = db.Purchases.Any(p => p.MovementId == id);
            if (linkedPurchase)
            {
                TempData["Error"] = "Цей рух пов'язаний із закупівлею. Спочатку видаліть закупівлю.";
                return RedirectToAction(nameof(Index));
            }

            var linkedSale = db.Sales.Any(s => s.MovementId == id);
            if (linkedSale)
            {
                TempData["Error"] = "Цей рух пов'язаний із продажем. Спочатку видаліть продаж.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (movement.Product != null)
                {
                    movement.Product.InventoryManager = inventoryManager;
                }

                if (movement.IsExecuted)
                {
                    movement.Cancel(user.Name);
                }

                db.Movements.Remove(movement);
                db.SaveChanges();

                TempData["Success"] = "Рух видалено.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Не вдалося видалити рух: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private void LoadData()
        {
            ViewBag.Products = db.Products
                .Include(p => p.Zone)
                .ThenInclude(z => z!.Warehouse)
                .ThenInclude(w => w!.Company)
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.Zones = db.StorageZones
                .Include(z => z.Warehouse)
                .ThenInclude(w => w!.Company)
                .OrderBy(z => z.Name)
                .ToList();

            ViewBag.Companies = db.Companies
                .OrderBy(c => c.Name)
                .ToList();
        }

        private StorageZone EnsureTransitZone(Company destinationCompany)
        {
            var zone = db.StorageZones
                .Include(z => z.Warehouse)
                .FirstOrDefault(z =>
                    z.Warehouse != null &&
                    z.Warehouse.CompanyId == destinationCompany.Id &&
                    (z.Type.ToLower() == "transit" || z.Type.ToLower() == "нейтральна"));

            if (zone != null)
            {
                return zone;
            }

            var warehouse = db.Warehouses
                .OrderBy(w => w.Id)
                .FirstOrDefault(w => w.CompanyId == destinationCompany.Id);

            if (warehouse == null)
            {
                throw new InvalidOperationException(
                    $"У компанії '{destinationCompany.Name}' немає складу. Спочатку створіть склад, щоб приймати міжкомпанійні поставки.");
            }

            zone = new StorageZone("Транзит", "Нейтральна", 0, warehouse);
            db.StorageZones.Add(zone);
            db.SaveChanges();

            return zone;
        }
    }
}
