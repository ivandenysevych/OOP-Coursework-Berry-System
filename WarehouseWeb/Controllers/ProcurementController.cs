using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Data;
using WarehouseWeb.Models;
using WarehouseWeb.ViewModels;

namespace WarehouseWeb.Controllers
{
    public class ProcurementController : Controller
    {
        private const string DefaultsDatePrefix = "ProcurementDefaults.Date";
        private const string DefaultsProductPrefix = "ProcurementDefaults.ProductId";
        private const string DefaultsPricePrefix = "ProcurementDefaults.UnitPrice";
        private const string DefaultsUnitPrefix = "ProcurementDefaults.Unit";
        private const string DefaultsCompanyPrefix = "ProcurementDefaults.CompanyId";

        private readonly WarehouseDbContext db;
        private readonly InventoryManager inventoryManager;

        public ProcurementController(WarehouseDbContext db, InventoryManager inventoryManager)
        {
            this.db = db;
            this.inventoryManager = inventoryManager;
        }

        public IActionResult Index()
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanAccessProcurementModule(user))
                return Content("Доступ заборонено");

            var isCollector = AuthController.IsCollector(user);
            var canTransfer = CanTransferToWarehouse(user);

            var query = db.Procurements
                .Include(p => p.Product)
                .Include(p => p.Company)
                .Include(p => p.TransferZone)
                .ThenInclude(z => z!.Warehouse)
                .Include(p => p.ExpenseAccount)
                .OrderByDescending(p => p.CollectedAt)
                .AsQueryable();

            if (isCollector)
            {
                query = query.Where(p => p.CollectorUserId == user.Id);
            }

            var procurements = query.ToList();
            var activeBaskets = procurements
                .Where(p => !p.IsTransferredToWarehouse && p.CollectorUserId.HasValue)
                .GroupBy(p => new
                {
                    CompanyId = p.CompanyId,
                    CompanyName = p.Company != null ? p.Company.Name : "Без компанії",
                    CollectorUserId = p.CollectorUserId!.Value,
                    CollectorName = p.CollectorName,
                    Category = ResolveCategory(p)
                })
                .Select(g => new ProcurementBasketSummaryViewModel
                {
                    CompanyId = g.Key.CompanyId,
                    CompanyName = g.Key.CompanyName,
                    CollectorUserId = g.Key.CollectorUserId,
                    CollectorName = g.Key.CollectorName,
                    Category = g.Key.Category,
                    QuantitySummary = string.Join(", ",
                        g.GroupBy(x => NormalizeUnitOrUnknown(x.Unit))
                            .OrderBy(x => x.Key)
                            .Select(x => $"{x.Sum(p => p.Quantity):0.###} {x.Key}")),
                    TotalCost = g.Sum(x => x.TotalCost),
                    RecordsCount = g.Count(),
                    FirstCollectedAt = g.Min(x => x.CollectedAt),
                    LastCollectedAt = g.Max(x => x.CollectedAt)
                })
                .OrderByDescending(x => x.LastCollectedAt)
                .ToList();

            var vm = new ProcurementIndexViewModel
            {
                Procurements = procurements,
                ActiveBaskets = activeBaskets,
                TransferZones = canTransfer
                    ? db.StorageZones
                        .Include(z => z.Warehouse)
                        .ThenInclude(w => w!.Company)
                        .OrderBy(z => z.Name)
                        .ToList()
                    : new(),
                IsCollector = isCollector,
                CanTransferToWarehouse = canTransfer
            };

            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(vm);
        }

        [HttpGet]
        public IActionResult Create(int? productId = null, int? companyId = null)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanAccessProcurementModule(user))
                return Content("Доступ заборонено");

            LoadCreateData(user, productId, null, companyId);
            return View();
        }

        [HttpPost]
        public IActionResult Create(
            int productId,
            int? companyId,
            string supplierPersonName,
            string quantity,
            string unit,
            string unitPrice,
            ProcurementPaymentMethod paymentMethod,
            DateTime collectedAt,
            string? notes)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanAccessProcurementModule(user))
                return Content("Доступ заборонено");

            if (string.IsNullOrWhiteSpace(supplierPersonName))
            {
                ViewBag.Error = "Вкажіть, від кого прийнято товар.";
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }

            if (!TryParseDecimalFlexible(quantity, out var parsedQuantity))
            {
                ViewBag.Error = "Некоректна кількість.";
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }

            if (!TryParseDecimalFlexible(unitPrice, out var parsedUnitPrice))
            {
                ViewBag.Error = "Некоректна ціна.";
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }

            if (parsedQuantity <= 0)
            {
                ViewBag.Error = "Кількість має бути більшою за нуль.";
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }

            if (parsedUnitPrice < 0)
            {
                ViewBag.Error = "Ціна не може бути від'ємною.";
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }

            if (!ProductFactory.IsSupportedUnit(unit))
            {
                ViewBag.Error = "Непідтримувана одиниця виміру.";
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }

            if (collectedAt == default)
            {
                collectedAt = DateTime.Now;
            }

            var product = db.Products.FirstOrDefault(p => p.Id == productId);
            if (product == null)
            {
                ViewBag.Error = "Товар не знайдено.";
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }

            var normalizedUnit = ProductFactory.NormalizeUnit(unit);
            if (!string.Equals(product.Unit, normalizedUnit, StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = $"Одиниця має збігатися з товаром ({product.Unit}).";
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }

            var availableCompanies = GetAvailableCompaniesForProcurement(user);
            if (availableCompanies.Count == 0)
            {
                ViewBag.Error = "Для користувача не налаштовано жодної компанії для заготівлі.";
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }

            var resolvedCompany = ResolveSelectedCompany(availableCompanies, companyId);
            if (resolvedCompany == null)
            {
                ViewBag.Error = "Оберіть компанію для цієї заготівлі.";
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }

            using var transaction = db.Database.BeginTransaction();

            try
            {
                var expenseAccount = ResolveExpenseAccount(user, resolvedCompany);
                expenseAccount.Debit(Math.Round(parsedQuantity * parsedUnitPrice, 2));

                var procurement = new Procurement(
                    product,
                    user,
                    supplierPersonName,
                    parsedQuantity,
                    normalizedUnit,
                    parsedUnitPrice,
                    paymentMethod,
                    NormalizeToUtc(collectedAt),
                    notes ?? string.Empty);

                procurement.CompanyId = resolvedCompany.Id;
                procurement.ExpenseAccountId = expenseAccount.Id;

                db.Procurements.Add(procurement);
                db.FinanceTransactions.Add(new FinanceTransaction
                {
                    Type = FinanceTransactionType.Adjustment,
                    Amount = procurement.TotalCost,
                    FromAccountId = expenseAccount.Id,
                    Notes = $"Оплата заготівлі: {procurement.SupplierPersonName}, товар: {product.Name}.",
                    CreatedBy = user.Name,
                    CreatedDate = DateTime.UtcNow
                });

                db.SaveChanges();
                transaction.Commit();

                SaveDailyDefaults(user.Id, productId, normalizedUnit, parsedUnitPrice, resolvedCompany.Id);

                TempData["Success"] =
                    $"Заготівлю додано в кошик. Списано {procurement.TotalCost:0.00} грн з рахунку '{expenseAccount.DisplayLabel()}'.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ViewBag.Error = ex.Message;
                LoadCreateData(user, productId, unit, companyId);
                return View();
            }
        }

        [HttpPost]
        public IActionResult TransferBasket(int collectorUserId, int? companyId, string category, int transferZoneId)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanTransferToWarehouse(user))
                return Content("Доступ заборонено");

            var trimmedCategory = category?.Trim() ?? string.Empty;
            if (collectorUserId <= 0 || string.IsNullOrWhiteSpace(trimmedCategory) || !companyId.HasValue)
            {
                TempData["Error"] = "Некоректні параметри кошика.";
                return RedirectToAction(nameof(Index));
            }

            var zone = db.StorageZones
                .Include(z => z.Warehouse)
                .ThenInclude(w => w!.Company)
                .FirstOrDefault(z => z.Id == transferZoneId);

            if (zone == null)
            {
                TempData["Error"] = "Зону складу не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            if (zone.Warehouse?.CompanyId != companyId.Value)
            {
                TempData["Error"] = "Для перевезення потрібно обрати зону тієї ж компанії, до якої належить кошик.";
                return RedirectToAction(nameof(Index));
            }

            var basketProcurements = db.Procurements
                .Include(p => p.Product)
                .Where(p =>
                    !p.IsTransferredToWarehouse &&
                    p.CollectorUserId == collectorUserId &&
                    p.CompanyId == companyId.Value &&
                    (p.ProductCategory == trimmedCategory ||
                     (string.IsNullOrWhiteSpace(p.ProductCategory) && p.Product != null && p.Product.Category == trimmedCategory)))
                .OrderBy(p => p.CollectedAt)
                .ToList();

            if (basketProcurements.Count == 0)
            {
                TempData["Error"] = "Кошик уже порожній або не знайдений.";
                return RedirectToAction(nameof(Index));
            }

            var unitMismatch = basketProcurements.FirstOrDefault(p =>
                p.Product == null ||
                !string.Equals(p.Product.Unit, ProductFactory.NormalizeUnit(p.Unit), StringComparison.OrdinalIgnoreCase));

            if (unitMismatch != null)
            {
                TempData["Error"] = "У кошику є позиції з одиницями, що не збігаються з налаштуванням товару. Виправте записи заготівлі.";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = db.Database.BeginTransaction();

            try
            {
                foreach (var procurement in basketProcurements)
                {
                    var product = procurement.Product!;
                    product.InventoryManager = inventoryManager;

                    var currentQuantity = product.Quantity;
                    var totalQuantityAfter = currentQuantity + procurement.Quantity;
                    var weightedPrice = totalQuantityAfter > 0
                        ? Math.Round(
                            ((currentQuantity * product.Price) + (procurement.Quantity * procurement.UnitPrice)) / totalQuantityAfter,
                            2)
                        : procurement.UnitPrice;

                    var movement = new Movement(
                        MovementType.Add,
                        procurement.Quantity,
                        product,
                        null,
                        zone);

                    inventoryManager.ExecuteMovement(movement, user.Name);
                    db.Movements.Add(movement);
                    product.UpdatePrice(weightedPrice, user.Name);

                    if (product.StorageZoneId != zone.Id)
                    {
                        product.AssignZone(zone, user.Name);
                    }

                    procurement.IsTransferredToWarehouse = true;
                    procurement.TransferredAt = DateTime.UtcNow;
                    procurement.TransferredBy = user.Name;
                    procurement.TransferZoneId = zone.Id;

                    if (string.IsNullOrWhiteSpace(procurement.ProductCategory))
                    {
                        procurement.ProductCategory = product.Category;
                    }
                }

                db.SaveChanges();
                transaction.Commit();

                TempData["Success"] = $"Кошик категорії '{trimmedCategory}' перевезено на склад '{zone.Name}'.";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Error"] = $"Не вдалося перевезти кошик: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            if (!CanAccessProcurementModule(user))
                return Content("Доступ заборонено");

            var procurement = db.Procurements.FirstOrDefault(p => p.Id == id);
            if (procurement == null)
            {
                TempData["Error"] = "Запис заготівлі не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            if (AuthController.IsCollector(user) && procurement.CollectorUserId != user.Id)
            {
                TempData["Error"] = "Ви можете видаляти лише власні записи.";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = db.Database.BeginTransaction();

            try
            {
                if (procurement.ExpenseAccountId.HasValue)
                {
                    var account = db.FinanceAccounts
                        .FirstOrDefault(a => a.Id == procurement.ExpenseAccountId.Value);

                    if (account != null)
                    {
                        account.Credit(procurement.TotalCost);
                        db.FinanceTransactions.Add(new FinanceTransaction
                        {
                            Type = FinanceTransactionType.Adjustment,
                            Amount = procurement.TotalCost,
                            ToAccountId = account.Id,
                            Notes = $"Повернення коштів після видалення заготівлі #{procurement.Id}.",
                            CreatedBy = user.Name,
                            CreatedDate = DateTime.UtcNow
                        });
                    }
                }

                db.Procurements.Remove(procurement);
                db.SaveChanges();
                transaction.Commit();
                TempData["Success"] = "Запис заготівлі видалено.";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Error"] = $"Не вдалося видалити запис: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private void LoadCreateData(
            User user,
            int? selectedProductId = null,
            string? selectedUnit = null,
            int? selectedCompanyId = null)
        {
            ViewBag.Products = db.Products
                .OrderBy(p => p.Name)
                .ToList();

            var availableCompanies = GetAvailableCompaniesForProcurement(user);
            ViewBag.AvailableCompanies = availableCompanies;

            ViewBag.Units = ProductFactory.GetAllowedUnits();
            ViewBag.SelectedProductId = selectedProductId;
            ViewBag.SelectedUnit = selectedUnit;
            ViewBag.SelectedCompanyId = selectedCompanyId;

            var dateKey = BuildSessionKey(user.Id, DefaultsDatePrefix);
            var currentDate = DateTime.Now.ToString("yyyy-MM-dd");
            var savedDate = HttpContext.Session.GetString(dateKey);

            if (!string.Equals(savedDate, currentDate, StringComparison.Ordinal))
            {
                ClearDailyDefaults(user.Id);
                return;
            }

            ViewBag.DefaultProductId = HttpContext.Session.GetInt32(BuildSessionKey(user.Id, DefaultsProductPrefix));
            ViewBag.DefaultUnitPrice = HttpContext.Session.GetString(BuildSessionKey(user.Id, DefaultsPricePrefix));
            ViewBag.DefaultUnit = HttpContext.Session.GetString(BuildSessionKey(user.Id, DefaultsUnitPrefix));
            ViewBag.DefaultCompanyId = HttpContext.Session.GetInt32(BuildSessionKey(user.Id, DefaultsCompanyPrefix));
        }

        private static bool CanAccessProcurementModule(User user)
        {
            return string.Equals(user.Role?.Name, RoleNames.Owner, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(user.Role?.Name, RoleNames.Manager, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(user.Role?.Name, RoleNames.Collector, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanTransferToWarehouse(User user)
        {
            return string.Equals(user.Role?.Name, RoleNames.Owner, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(user.Role?.Name, RoleNames.Manager, StringComparison.OrdinalIgnoreCase);
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

        private void SaveDailyDefaults(int userId, int productId, string unit, decimal unitPrice, int companyId)
        {
            HttpContext.Session.SetString(BuildSessionKey(userId, DefaultsDatePrefix), DateTime.Now.ToString("yyyy-MM-dd"));
            HttpContext.Session.SetInt32(BuildSessionKey(userId, DefaultsProductPrefix), productId);
            HttpContext.Session.SetString(BuildSessionKey(userId, DefaultsUnitPrefix), unit);
            HttpContext.Session.SetString(
                BuildSessionKey(userId, DefaultsPricePrefix),
                unitPrice.ToString("0.###", CultureInfo.InvariantCulture));
            HttpContext.Session.SetInt32(BuildSessionKey(userId, DefaultsCompanyPrefix), companyId);
        }

        private void ClearDailyDefaults(int userId)
        {
            HttpContext.Session.Remove(BuildSessionKey(userId, DefaultsDatePrefix));
            HttpContext.Session.Remove(BuildSessionKey(userId, DefaultsProductPrefix));
            HttpContext.Session.Remove(BuildSessionKey(userId, DefaultsUnitPrefix));
            HttpContext.Session.Remove(BuildSessionKey(userId, DefaultsPricePrefix));
            HttpContext.Session.Remove(BuildSessionKey(userId, DefaultsCompanyPrefix));
        }

        private static string BuildSessionKey(int userId, string prefix)
        {
            return $"{prefix}:{userId}";
        }

        private List<Company> GetAvailableCompaniesForProcurement(User user)
        {
            if (string.Equals(user.Role?.Name, RoleNames.Owner, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.Role?.Name, RoleNames.Manager, StringComparison.OrdinalIgnoreCase))
            {
                return db.Companies
                    .OrderBy(c => c.Name)
                    .ToList();
            }

            var collectorCompanies = db.CompanyUsers
                .Include(cu => cu.Company)
                .Include(cu => cu.Role)
                .Where(cu =>
                    cu.UserId == user.Id &&
                    cu.Company != null &&
                    cu.Role != null &&
                    cu.Role.Name.ToLower() == RoleNames.Collector.ToLower())
                .Select(cu => cu.Company!)
                .Distinct()
                .OrderBy(c => c.Name)
                .ToList();

            if (collectorCompanies.Count > 0)
            {
                return collectorCompanies;
            }

            return db.CompanyUsers
                .Include(cu => cu.Company)
                .Where(cu => cu.UserId == user.Id && cu.Company != null)
                .Select(cu => cu.Company!)
                .Distinct()
                .OrderBy(c => c.Name)
                .ToList();
        }

        private static Company? ResolveSelectedCompany(List<Company> availableCompanies, int? companyId)
        {
            if (companyId.HasValue)
            {
                return availableCompanies.FirstOrDefault(c => c.Id == companyId.Value);
            }

            if (availableCompanies.Count == 1)
            {
                return availableCompanies[0];
            }

            return null;
        }

        private FinanceAccount ResolveExpenseAccount(User user, Company company)
        {
            if (AuthController.IsCollector(user))
            {
                return EnsureCollectorFinanceAccount(company, user);
            }

            return EnsureCompanySafeAccount(company);
        }

        private FinanceAccount EnsureCompanySafeAccount(Company company)
        {
            var account = db.FinanceAccounts.FirstOrDefault(a =>
                a.AccountType == FinanceAccountType.Company &&
                a.CompanyId == company.Id &&
                a.WarehouseId == null &&
                a.UserId == null);

            if (account != null)
            {
                return account;
            }

            account = new FinanceAccount
            {
                AccountType = FinanceAccountType.Company,
                CompanyId = company.Id,
                Name = $"Компанія: {company.Name}",
                Currency = "UAH",
                Balance = 0,
                CreatedDate = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.FinanceAccounts.Add(account);
            db.SaveChanges();

            return account;
        }

        private FinanceAccount EnsureCollectorFinanceAccount(Company company, User collector)
        {
            var account = db.FinanceAccounts.FirstOrDefault(a =>
                a.AccountType == FinanceAccountType.User &&
                a.CompanyId == company.Id &&
                a.UserId == collector.Id &&
                a.WarehouseId == null);

            if (account != null)
            {
                return account;
            }

            account = new FinanceAccount
            {
                AccountType = FinanceAccountType.User,
                CompanyId = company.Id,
                UserId = collector.Id,
                Name = $"Заготівельник: {collector.Name} ({company.Name})",
                Currency = "UAH",
                Balance = 0,
                CreatedDate = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.FinanceAccounts.Add(account);
            db.SaveChanges();

            return account;
        }

        private static string ResolveCategory(Procurement procurement)
        {
            if (!string.IsNullOrWhiteSpace(procurement.ProductCategory))
            {
                return procurement.ProductCategory.Trim();
            }

            return procurement.Product?.Category?.Trim() ?? "Без категорії";
        }

        private static string NormalizeUnitOrUnknown(string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return "unit";
            }

            return ProductFactory.NormalizeUnit(unit);
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
