using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Data;
using WarehouseWeb.Models;
using WarehouseWeb.ViewModels;

namespace WarehouseWeb.Controllers
{
    public class FinanceController : Controller
    {
        private readonly WarehouseDbContext db;

        public FinanceController(WarehouseDbContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!CanManageFinance(user))
            {
                return Content("Доступ заборонено");
            }

            EnsureBaseAccounts();

            var model = BuildDashboardModel();
            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(model);
        }

        [HttpPost]
        public IActionResult TopUpCompany(int companyId, string amount, string? notes)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!CanManageFinance(user))
            {
                return Content("Доступ заборонено");
            }

            if (!TryParseDecimalFlexible(amount, out var parsedAmount) || parsedAmount <= 0)
            {
                TempData["Error"] = "Вкажіть коректну суму поповнення.";
                return RedirectToAction(nameof(Index));
            }

            EnsureBaseAccounts();

            var company = db.Companies.FirstOrDefault(c => c.Id == companyId);
            if (company == null)
            {
                TempData["Error"] = "Компанію не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            var account = db.FinanceAccounts.FirstOrDefault(a =>
                a.AccountType == FinanceAccountType.Company &&
                a.CompanyId == companyId &&
                a.WarehouseId == null &&
                a.UserId == null);

            if (account == null)
            {
                TempData["Error"] = "Рахунок компанії не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                account.Credit(parsedAmount);

                var transaction = new FinanceTransaction
                {
                    Type = FinanceTransactionType.TopUp,
                    Amount = parsedAmount,
                    ToAccountId = account.Id,
                    Notes = notes?.Trim() ?? string.Empty,
                    CreatedBy = user.Name,
                    CreatedDate = DateTime.UtcNow
                };

                db.FinanceTransactions.Add(transaction);
                db.SaveChanges();

                TempData["Success"] = $"Баланс компанії '{company.Name}' поповнено на {parsedAmount:0.00} грн.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Не вдалося виконати поповнення: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Transfer(int fromAccountId, int toAccountId, string amount, string? notes)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!CanManageFinance(user))
            {
                return Content("Доступ заборонено");
            }

            if (fromAccountId == toAccountId)
            {
                TempData["Error"] = "Рахунки відправника та отримувача мають бути різними.";
                return RedirectToAction(nameof(Index));
            }

            if (!TryParseDecimalFlexible(amount, out var parsedAmount) || parsedAmount <= 0)
            {
                TempData["Error"] = "Вкажіть коректну суму переказу.";
                return RedirectToAction(nameof(Index));
            }

            EnsureBaseAccounts();

            var fromAccount = db.FinanceAccounts.FirstOrDefault(a => a.Id == fromAccountId);
            var toAccount = db.FinanceAccounts.FirstOrDefault(a => a.Id == toAccountId);

            if (fromAccount == null || toAccount == null)
            {
                TempData["Error"] = "Один із рахунків не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = db.Database.BeginTransaction();

            try
            {
                fromAccount.Debit(parsedAmount);
                toAccount.Credit(parsedAmount);

                var financeTransaction = new FinanceTransaction
                {
                    Type = FinanceTransactionType.Transfer,
                    Amount = parsedAmount,
                    FromAccountId = fromAccount.Id,
                    ToAccountId = toAccount.Id,
                    Notes = notes?.Trim() ?? string.Empty,
                    CreatedBy = user.Name,
                    CreatedDate = DateTime.UtcNow
                };

                db.FinanceTransactions.Add(financeTransaction);
                db.SaveChanges();
                transaction.Commit();

                TempData["Success"] = $"Переказ виконано: {parsedAmount:0.00} грн.";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Error"] = $"Не вдалося виконати переказ: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult TransferCompanyToCollector(int companyId, int collectorAccountId, string amount, string? notes)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!CanManageFinance(user))
            {
                return Content("Доступ заборонено");
            }

            if (!TryParseDecimalFlexible(amount, out var parsedAmount) || parsedAmount <= 0)
            {
                TempData["Error"] = "Вкажіть коректну суму переказу.";
                return RedirectToAction(nameof(Index));
            }

            EnsureBaseAccounts();

            var company = db.Companies.FirstOrDefault(c => c.Id == companyId);
            if (company == null)
            {
                TempData["Error"] = "Компанію не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            var fromAccount = db.FinanceAccounts.FirstOrDefault(a =>
                a.AccountType == FinanceAccountType.Company &&
                a.CompanyId == companyId &&
                a.WarehouseId == null &&
                a.UserId == null);

            var collectorAccount = db.FinanceAccounts
                .Include(a => a.User)
                .FirstOrDefault(a =>
                    a.Id == collectorAccountId &&
                    a.AccountType == FinanceAccountType.User &&
                    a.CompanyId == companyId &&
                    a.WarehouseId == null &&
                    a.UserId != null);

            if (fromAccount == null)
            {
                TempData["Error"] = "Рахунок компанії не знайдено.";
                return RedirectToAction(nameof(Index));
            }

            if (collectorAccount == null)
            {
                TempData["Error"] = "Рахунок заготівельника не знайдено для цієї компанії.";
                return RedirectToAction(nameof(Index));
            }

            var isCollector = db.CompanyUsers
                .Include(cu => cu.Role)
                .Any(cu =>
                    cu.CompanyId == companyId &&
                    cu.UserId == collectorAccount.UserId &&
                    string.Equals(cu.Role != null ? cu.Role.Name : string.Empty, RoleNames.Collector, StringComparison.OrdinalIgnoreCase));

            if (!isCollector)
            {
                TempData["Error"] = "Обраний працівник не є заготівельником у цій компанії.";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = db.Database.BeginTransaction();

            try
            {
                fromAccount.Debit(parsedAmount);
                collectorAccount.Credit(parsedAmount);

                var transferNotes = notes?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(transferNotes))
                {
                    transferNotes = $"Видача заготівельнику. {transferNotes}";
                }
                else
                {
                    transferNotes = "Видача заготівельнику.";
                }

                db.FinanceTransactions.Add(new FinanceTransaction
                {
                    Type = FinanceTransactionType.Transfer,
                    Amount = parsedAmount,
                    FromAccountId = fromAccount.Id,
                    ToAccountId = collectorAccount.Id,
                    Notes = transferNotes,
                    CreatedBy = user.Name,
                    CreatedDate = DateTime.UtcNow
                });

                db.SaveChanges();
                transaction.Commit();

                TempData["Success"] =
                    $"Переказ виконано: {parsedAmount:0.00} грн з рахунку компанії '{company.Name}' на баланс '{collectorAccount.DisplayLabel()}'.";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Error"] = $"Не вдалося виконати переказ: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private FinanceDashboardViewModel BuildDashboardModel()
        {
            var companies = db.Companies
                .Include(c => c.Warehouses)
                .Include(c => c.Employees)
                .ThenInclude(cu => cu.User)
                .Include(c => c.Employees)
                .ThenInclude(cu => cu.Role)
                .OrderBy(c => c.Name)
                .ToList();

            var accounts = db.FinanceAccounts
                .OrderBy(a => a.AccountType)
                .ThenBy(a => a.Name)
                .ToList();

            var sections = new List<FinanceCompanySectionViewModel>();

            foreach (var company in companies)
            {
                var companyAccount = FindAccount(accounts, FinanceAccountType.Company, company.Id, null, null);
                if (companyAccount == null)
                {
                    continue;
                }

                var warehouseAccounts = company.Warehouses
                    .Select(w => FindAccount(accounts, FinanceAccountType.Warehouse, company.Id, w.Id, null))
                    .Where(a => a != null)
                    .Cast<FinanceAccount>()
                    .OrderBy(a => a.Name)
                    .ToList();

                var collectorAccounts = company.Employees
                    .Where(IsCollectorMembership)
                    .Select(cu => FindAccount(accounts, FinanceAccountType.User, company.Id, null, cu.UserId))
                    .Where(a => a != null)
                    .Cast<FinanceAccount>()
                    .OrderBy(a => a.Name)
                    .ToList();

                sections.Add(new FinanceCompanySectionViewModel
                {
                    Company = company,
                    CompanyAccount = companyAccount,
                    WarehouseAccounts = warehouseAccounts,
                    CollectorAccounts = collectorAccounts
                });
            }

            var options = accounts
                .OrderBy(a => a.AccountType)
                .ThenBy(a => a.Name)
                .Select(a => new FinanceAccountOptionViewModel
                {
                    Id = a.Id,
                    Label = BuildAccountLabel(a)
                })
                .ToList();

            var recentTransactions = db.FinanceTransactions
                .Include(t => t.FromAccount)
                .Include(t => t.ToAccount)
                .OrderByDescending(t => t.CreatedDate)
                .Take(30)
                .ToList();

            return new FinanceDashboardViewModel
            {
                Companies = sections,
                AccountOptions = options,
                RecentTransactions = recentTransactions
            };
        }

        private void EnsureBaseAccounts()
        {
            var companies = db.Companies
                .Include(c => c.Warehouses)
                .Include(c => c.Employees)
                .ThenInclude(cu => cu.User)
                .Include(c => c.Employees)
                .ThenInclude(cu => cu.Role)
                .ToList();

            var accounts = db.FinanceAccounts.ToList();
            var hasChanges = false;

            foreach (var company in companies)
            {
                hasChanges |= EnsureCompanyAccount(company, accounts);

                foreach (var warehouse in company.Warehouses)
                {
                    hasChanges |= EnsureWarehouseAccount(company, warehouse, accounts);
                }

                foreach (var companyUser in company.Employees.Where(IsCollectorMembership))
                {
                    if (companyUser.User == null)
                    {
                        continue;
                    }

                    hasChanges |= EnsureCollectorAccount(company, companyUser.User, accounts);
                }
            }

            if (hasChanges)
            {
                db.SaveChanges();
            }
        }

        private bool EnsureCompanyAccount(Company company, List<FinanceAccount> accounts)
        {
            var expectedName = $"Компанія: {company.Name}";
            var account = FindAccount(accounts, FinanceAccountType.Company, company.Id, null, null);

            if (account == null)
            {
                var newAccount = new FinanceAccount
                {
                    AccountType = FinanceAccountType.Company,
                    CompanyId = company.Id,
                    Name = expectedName,
                    Currency = "UAH",
                    Balance = 0,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.FinanceAccounts.Add(newAccount);
                accounts.Add(newAccount);
                return true;
            }

            return SyncAccountTitle(account, expectedName);
        }

        private bool EnsureWarehouseAccount(Company company, Warehouse warehouse, List<FinanceAccount> accounts)
        {
            var expectedName = $"Склад: {warehouse.Name} ({company.Name})";
            var account = FindAccount(accounts, FinanceAccountType.Warehouse, company.Id, warehouse.Id, null);

            if (account == null)
            {
                var newAccount = new FinanceAccount
                {
                    AccountType = FinanceAccountType.Warehouse,
                    CompanyId = company.Id,
                    WarehouseId = warehouse.Id,
                    Name = expectedName,
                    Currency = "UAH",
                    Balance = 0,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.FinanceAccounts.Add(newAccount);
                accounts.Add(newAccount);
                return true;
            }

            return SyncAccountTitle(account, expectedName);
        }

        private bool EnsureCollectorAccount(Company company, User collector, List<FinanceAccount> accounts)
        {
            var expectedName = $"Заготівельник: {collector.Name} ({company.Name})";
            var account = FindAccount(accounts, FinanceAccountType.User, company.Id, null, collector.Id);

            if (account == null)
            {
                var newAccount = new FinanceAccount
                {
                    AccountType = FinanceAccountType.User,
                    CompanyId = company.Id,
                    UserId = collector.Id,
                    Name = expectedName,
                    Currency = "UAH",
                    Balance = 0,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.FinanceAccounts.Add(newAccount);
                accounts.Add(newAccount);
                return true;
            }

            return SyncAccountTitle(account, expectedName);
        }

        private static FinanceAccount? FindAccount(
            List<FinanceAccount> accounts,
            FinanceAccountType type,
            int? companyId,
            int? warehouseId,
            int? userId)
        {
            return accounts.FirstOrDefault(a =>
                a.AccountType == type &&
                a.CompanyId == companyId &&
                a.WarehouseId == warehouseId &&
                a.UserId == userId);
        }

        private static bool SyncAccountTitle(FinanceAccount account, string expectedName)
        {
            var changed = false;

            if (!string.Equals(account.Name, expectedName, StringComparison.Ordinal))
            {
                account.Name = expectedName;
                changed = true;
            }

            if (!string.Equals(account.Currency, "UAH", StringComparison.OrdinalIgnoreCase))
            {
                account.Currency = "UAH";
                changed = true;
            }

            if (changed)
            {
                account.UpdatedAt = DateTime.UtcNow;
            }

            return changed;
        }

        private static bool IsCollectorMembership(CompanyUser companyUser)
        {
            return string.Equals(
                       companyUser.Role?.Name,
                       RoleNames.Collector,
                       StringComparison.OrdinalIgnoreCase) &&
                   companyUser.UserId > 0;
        }

        private static bool CanManageFinance(User user)
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

        private static string BuildAccountLabel(FinanceAccount account)
        {
            var typeLabel = account.AccountType switch
            {
                FinanceAccountType.Company => "Компанія",
                FinanceAccountType.Warehouse => "Склад",
                FinanceAccountType.User => "Працівник",
                _ => "Рахунок"
            };

            return $"{typeLabel} | {account.DisplayLabel()} | Баланс: {account.Balance:0.00} {account.Currency}";
        }
    }
}
