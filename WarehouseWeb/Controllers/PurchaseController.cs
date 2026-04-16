using System;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Data;
using WarehouseWeb.Models;

namespace WarehouseWeb.Controllers
{
    public class PurchaseController : Controller
    {
        private const string DirectReceiptSupplierName = "[System] Пряме надходження (без постачальника)";

        private readonly WarehouseDbContext db;
        private readonly InventoryManager inventoryManager;

        public PurchaseController(WarehouseDbContext db, InventoryManager inventoryManager)
        {
            this.db = db;
            this.inventoryManager = inventoryManager;
        }

        public IActionResult Index()
        {
            var purchases = db.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.Contract)
                .Include(p => p.Product)
                .Include(p => p.StorageZone)
                .ThenInclude(z => z!.Warehouse)
                .OrderByDescending(p => p.ArrivalDate)
                .ToList();

            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(purchases);
        }

        [HttpGet]
        public IActionResult Create(int? supplierId, int? supplierContractId, int? productId, int? storageZoneId)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanManagePurchases(user))
                return Content("Доступ заборонено");

            LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
            return View();
        }

        [HttpPost]
        public IActionResult Create(
            int supplierId,
            int? supplierContractId,
            int productId,
            string quantity,
            string unitPrice,
            DateTime arrivalDate,
            DateTime? paymentDueDate,
            string? paymentTerms,
            PurchaseQualityStatus qualityStatus,
            string? qualityNotes,
            string? invoiceNumber,
            string? acceptanceActNumber,
            int? storageZoneId,
            bool postToInventory = false,
            bool directReceipt = false,
            string? directSourceName = null,
            ProcurementPaymentMethod? directPaymentMethod = null)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanManagePurchases(user))
                return Content("Доступ заборонено");

            if (arrivalDate == default)
            {
                arrivalDate = DateTime.Now;
            }

            if (supplierContractId.HasValue && supplierContractId.Value <= 0)
            {
                supplierContractId = null;
            }

            if (storageZoneId.HasValue && storageZoneId.Value <= 0)
            {
                storageZoneId = null;
            }

            if (!TryParseDecimalFlexible(quantity, out var parsedQuantity))
            {
                ViewBag.Error = "Некоректна кількість.";
                LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                return View();
            }

            if (!TryParseDecimalFlexible(unitPrice, out var parsedUnitPrice))
            {
                ViewBag.Error = "Некоректна ціна.";
                LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                return View();
            }

            if (parsedQuantity <= 0)
            {
                ViewBag.Error = "Кількість має бути більшою за нуль.";
                LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                return View();
            }

            if (parsedUnitPrice < 0)
            {
                ViewBag.Error = "Ціна не може бути від'ємною.";
                LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                return View();
            }

            if (paymentDueDate.HasValue && paymentDueDate.Value.Date < arrivalDate.Date)
            {
                ViewBag.Error = "Дата оплати не може бути раніше дати надходження.";
                LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                return View();
            }

            var product = db.Products
                .Include(p => p.Zone)
                .FirstOrDefault(p => p.Id == productId);

            if (product == null)
            {
                ViewBag.Error = "Помилка даних.";
                LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                return View();
            }

            var isDirectReceipt = directReceipt;

            Supplier? supplier = null;
            var resolvedDirectPaymentMethod = directPaymentMethod ?? ProcurementPaymentMethod.Cash;
            var resolvedDirectSource = directSourceName?.Trim() ?? string.Empty;

            if (isDirectReceipt)
            {
                if (string.IsNullOrWhiteSpace(resolvedDirectSource))
                {
                    ViewBag.Error = "Для прямого надходження вкажіть, від кого прийнято товар.";
                    LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                    return View();
                }

                supplier = EnsureDirectReceiptSupplier();
                supplierContractId = null;
            }
            else
            {
                if (supplierId <= 0)
                {
                    ViewBag.Error = "Оберіть постачальника.";
                    LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                    return View();
                }

                supplier = db.Suppliers.FirstOrDefault(s => s.Id == supplierId);
                if (supplier == null)
                {
                    ViewBag.Error = "Постачальника не знайдено.";
                    LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                    return View();
                }
            }

            SupplierContract? contract = null;
            if (!isDirectReceipt && supplierContractId.HasValue)
            {
                contract = db.SupplierContracts
                    .FirstOrDefault(c => c.Id == supplierContractId.Value);

                if (contract == null)
                {
                    ViewBag.Error = "Договір не знайдено.";
                    LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                    return View();
                }

                if (contract.SupplierId != supplierId)
                {
                    ViewBag.Error = "Обраний договір не належить постачальнику.";
                    LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                    return View();
                }
            }

            StorageZone? zone = null;
            if (storageZoneId.HasValue)
            {
                zone = db.StorageZones
                    .Include(z => z.Warehouse)
                    .FirstOrDefault(z => z.Id == storageZoneId.Value);

                if (zone == null)
                {
                    ViewBag.Error = "Зону не знайдено.";
                    LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                    return View();
                }
            }

            try
            {
                var resolvedPaymentTerms = paymentTerms ?? string.Empty;
                var resolvedQualityNotes = qualityNotes ?? string.Empty;

                if (isDirectReceipt)
                {
                    resolvedPaymentTerms = AppendLine(
                        resolvedPaymentTerms,
                        $"Пряме надходження. Оплата: {PaymentMethodLabel(resolvedDirectPaymentMethod)}.");

                    resolvedQualityNotes = AppendLine(
                        resolvedQualityNotes,
                        $"Джерело надходження: {resolvedDirectSource}");
                }

                var purchase = new Purchase(
                    supplier!,
                    product,
                    contract,
                    zone,
                    parsedQuantity,
                    parsedUnitPrice,
                    NormalizeToUtc(arrivalDate),
                    paymentDueDate.HasValue ? NormalizeToUtc(paymentDueDate.Value) : null,
                    resolvedPaymentTerms,
                    qualityStatus,
                    resolvedQualityNotes,
                    invoiceNumber ?? string.Empty,
                    acceptanceActNumber ?? string.Empty,
                    user.Name,
                    isDirectReceipt,
                    resolvedDirectSource,
                    isDirectReceipt ? resolvedDirectPaymentMethod : null
                );

                var shouldPostToInventory = postToInventory && qualityStatus != PurchaseQualityStatus.Rejected;

                if (shouldPostToInventory)
                {
                    product.InventoryManager = inventoryManager;

                    var destinationZone = zone ?? product.Zone;
                    var currentQuantity = product.Quantity;
                    var totalQuantityAfter = currentQuantity + parsedQuantity;
                    var weightedPrice = totalQuantityAfter > 0
                        ? Math.Round(
                            ((currentQuantity * product.Price) + (parsedQuantity * parsedUnitPrice)) / totalQuantityAfter,
                            2)
                        : parsedUnitPrice;

                    var movement = new Movement(
                        MovementType.Add,
                        parsedQuantity,
                        product,
                        null,
                        destinationZone);

                    inventoryManager.ExecuteMovement(movement, user.Name);
                    db.Movements.Add(movement);
                    product.UpdatePrice(weightedPrice, user.Name);

                    if (destinationZone != null && product.StorageZoneId != destinationZone.Id)
                    {
                        product.AssignZone(destinationZone, user.Name);
                    }

                    purchase.Movement = movement;
                    purchase.MarkInventoryPosted();
                }

                db.Purchases.Add(purchase);
                db.SaveChanges();

                TempData["Success"] = shouldPostToInventory
                    ? "Закупівлю збережено та оприбутковано на склад."
                    : "Закупівлю збережено без оприбуткування.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                LoadCreateData(supplierId, supplierContractId, productId, storageZoneId);
                return View();
            }
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanManagePurchases(user))
                return Content("Доступ заборонено");

            var purchase = db.Purchases
                .Include(p => p.Product)
                .Include(p => p.Movement)
                .ThenInclude(m => m!.Product)
                .FirstOrDefault(p => p.Id == id);

            if (purchase == null)
            {
                TempData["Error"] = "Закупівлю не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (purchase.InventoryPosted)
                {
                    if (purchase.Movement != null)
                    {
                        var movement = purchase.Movement;
                        var product = movement.Product ?? purchase.Product;

                        if (product == null)
                        {
                            TempData["Error"] = "Неможливо виконати відкат залишків: товар не знайдено.";
                            return RedirectToAction(nameof(Index));
                        }

                        if (movement.IsExecuted && product.Quantity < movement.Quantity)
                        {
                            TempData["Error"] =
                                "Неможливо видалити закупівлю: частину товару вже продано або списано.";
                            return RedirectToAction(nameof(Index));
                        }

                        product.InventoryManager = inventoryManager;

                        if (movement.IsExecuted)
                        {
                            movement.Cancel(user.Name);
                        }

                        db.Movements.Remove(movement);
                    }
                    else if (purchase.Product != null)
                    {
                        var product = purchase.Product;
                        if (product.Quantity < purchase.Quantity)
                        {
                            TempData["Error"] =
                                "Неможливо видалити закупівлю: частину товару вже продано або списано.";
                            return RedirectToAction(nameof(Index));
                        }

                        product.InventoryManager = inventoryManager;
                        product.DecreaseQuantity(purchase.Quantity, user.Name);
                    }
                }

                db.Purchases.Remove(purchase);
                db.SaveChanges();

                TempData["Success"] = "Закупівлю видалено.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Не вдалося видалити закупівлю через пов'язані записи.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Не вдалося видалити закупівлю: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private void LoadCreateData(
            int? selectedSupplierId,
            int? selectedContractId,
            int? selectedProductId,
            int? selectedStorageZoneId)
        {
            ViewBag.Suppliers = db.Suppliers
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.Contracts = db.SupplierContracts
                .Include(c => c.Supplier)
                .OrderByDescending(c => c.StartDate)
                .ThenBy(c => c.ContractNumber)
                .ToList();

            ViewBag.Products = db.Products
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.Zones = db.StorageZones
                .Include(z => z.Warehouse)
                .OrderBy(z => z.Name)
                .ToList();

            ViewBag.SelectedSupplierId = selectedSupplierId;
            ViewBag.SelectedContractId = selectedContractId;
            ViewBag.SelectedProductId = selectedProductId;
            ViewBag.SelectedStorageZoneId = selectedStorageZoneId;
        }

        private static bool CanManagePurchases(User user)
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

        private Supplier EnsureDirectReceiptSupplier()
        {
            var supplier = db.Suppliers.FirstOrDefault(s => s.Name == DirectReceiptSupplierName);
            if (supplier != null)
            {
                if (!supplier.IsActive)
                {
                    supplier.IsActive = true;
                    db.SaveChanges();
                }

                return supplier;
            }

            supplier = new Supplier(
                DirectReceiptSupplierName,
                "Без постачальника",
                string.Empty,
                string.Empty,
                "Системний постачальник для прямого надходження.",
                "Створено автоматично");

            db.Suppliers.Add(supplier);
            db.SaveChanges();

            return supplier;
        }

        private static string PaymentMethodLabel(ProcurementPaymentMethod paymentMethod)
        {
            return paymentMethod switch
            {
                ProcurementPaymentMethod.Cash => "Готівка",
                ProcurementPaymentMethod.Card => "Карта",
                _ => paymentMethod.ToString()
            };
        }

        private static string AppendLine(string currentValue, string lineToAdd)
        {
            var current = currentValue?.Trim() ?? string.Empty;
            var line = lineToAdd?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(line))
            {
                return current;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                return line;
            }

            return $"{current} {line}";
        }
    }
}
