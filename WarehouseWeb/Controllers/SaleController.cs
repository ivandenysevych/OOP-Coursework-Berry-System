using System;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Data;
using WarehouseWeb.Models;

namespace WarehouseWeb.Controllers
{
    public class SaleController : Controller
    {
        private readonly WarehouseDbContext db;
        private readonly InventoryManager inventoryManager;

        public SaleController(WarehouseDbContext db, InventoryManager inventoryManager)
        {
            this.db = db;
            this.inventoryManager = inventoryManager;
        }

        public IActionResult Index()
        {
            var sales = db.Sales
                .Include(s => s.Product)
                .Include(s => s.Movement)
                .OrderByDescending(s => s.SaleDate)
                .ToList();

            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(sales);
        }

        [HttpGet]
        public IActionResult Create(int? productId = null)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanManageSales(user))
                return Content("Доступ заборонено");

            LoadCreateData(productId);
            return View();
        }

        [HttpPost]
        public IActionResult Create(
            int productId,
            string quantity,
            string unitPrice,
            DateTime saleDate,
            string? customerName,
            string? paymentTerms,
            string? invoiceNumber,
            string? notes)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanManageSales(user))
                return Content("Доступ заборонено");

            if (!TryParseDecimalFlexible(quantity, out var parsedQuantity))
            {
                ViewBag.Error = "Некоректна кількість.";
                LoadCreateData(productId);
                return View();
            }

            if (!TryParseDecimalFlexible(unitPrice, out var parsedUnitPrice))
            {
                ViewBag.Error = "Некоректна ціна.";
                LoadCreateData(productId);
                return View();
            }

            if (parsedQuantity <= 0)
            {
                ViewBag.Error = "Кількість має бути більшою за нуль.";
                LoadCreateData(productId);
                return View();
            }

            if (parsedUnitPrice < 0)
            {
                ViewBag.Error = "Ціна не може бути від'ємною.";
                LoadCreateData(productId);
                return View();
            }

            if (saleDate == default)
            {
                saleDate = DateTime.Now;
            }

            var product = db.Products
                .Include(p => p.Zone)
                .FirstOrDefault(p => p.Id == productId);

            if (product == null)
            {
                ViewBag.Error = "Товар не знайдено.";
                LoadCreateData(productId);
                return View();
            }

            if (product.Quantity < parsedQuantity)
            {
                ViewBag.Error = $"Недостатній залишок: доступно {product.Quantity}, потрібно {parsedQuantity}.";
                LoadCreateData(productId);
                return View();
            }

            try
            {
                var sale = new Sale(
                    product,
                    parsedQuantity,
                    parsedUnitPrice,
                    NormalizeToUtc(saleDate),
                    customerName ?? string.Empty,
                    paymentTerms ?? string.Empty,
                    invoiceNumber ?? string.Empty,
                    notes ?? string.Empty,
                    user.Name
                );

                product.InventoryManager = inventoryManager;

                var movement = new Movement(
                    MovementType.Remove,
                    parsedQuantity,
                    product,
                    product.Zone,
                    null);

                inventoryManager.ExecuteMovement(movement, user.Name);
                db.Movements.Add(movement);

                sale.Movement = movement;

                db.Sales.Add(sale);
                db.SaveChanges();

                TempData["Success"] = "Продаж збережено і залишок на складі оновлено.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                LoadCreateData(productId);
                return View();
            }
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanManageSales(user))
                return Content("Доступ заборонено");

            var sale = db.Sales
                .Include(s => s.Product)
                .Include(s => s.Movement)
                .ThenInclude(m => m!.Product)
                .FirstOrDefault(s => s.Id == id);

            if (sale == null)
            {
                TempData["Error"] = "Продаж не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (sale.Movement != null)
                {
                    var movement = sale.Movement;
                    var product = movement.Product ?? sale.Product;

                    if (product == null)
                    {
                        TempData["Error"] = "Не вдалося відновити залишок: товар не знайдено.";
                        return RedirectToAction(nameof(Index));
                    }

                    product.InventoryManager = inventoryManager;

                    if (movement.IsExecuted)
                    {
                        movement.Cancel(user.Name);
                    }

                    db.Sales.Remove(sale);
                    db.Movements.Remove(movement);
                }
                else
                {
                    if (sale.Product != null)
                    {
                        sale.Product.InventoryManager = inventoryManager;
                        sale.Product.IncreaseQuantity(sale.Quantity, user.Name);
                    }

                    db.Sales.Remove(sale);
                }

                db.SaveChanges();
                TempData["Success"] = "Продаж видалено, залишок на складі відновлено.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Не вдалося видалити продаж через пов'язані записи.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Не вдалося видалити продаж: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private void LoadCreateData(int? selectedProductId)
        {
            ViewBag.Products = db.Products
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.SelectedProductId = selectedProductId;
        }

        private static bool CanManageSales(User user)
        {
            return string.Equals(user.Role?.Name, RoleNames.Owner, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(user.Role?.Name, RoleNames.Manager, StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime NormalizeToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Local)
            {
                return value.ToUniversalTime();
            }

            return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
        }

        private static bool TryParseDecimalFlexible(string? rawValue, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            var normalized = rawValue.Trim().Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }
}
