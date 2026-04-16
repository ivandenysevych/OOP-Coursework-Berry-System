using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Data;
using WarehouseWeb.Models;
using WarehouseWeb.ViewModels;

namespace WarehouseWeb.Controllers
{
    public class SupplierController : Controller
    {
        private readonly WarehouseDbContext db;

        public SupplierController(WarehouseDbContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            var suppliers = db.Suppliers
                .Include(s => s.Contracts)
                .Include(s => s.Purchases)
                .OrderBy(s => s.Name)
                .ToList();

            return View(suppliers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!CanManageSuppliers(user))
            {
                return Content("Доступ заборонено");
            }

            return View();
        }

        [HttpPost]
        public IActionResult Create(
            string name,
            string? contactPerson,
            string? email,
            string? phone,
            string? cooperationTerms,
            string? pricingNotes,
            bool isActive = true)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!CanManageSuppliers(user))
            {
                return Content("Доступ заборонено");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                ViewBag.Error = "Вкажіть назву постачальника.";
                return View();
            }

            var normalizedName = name.Trim();
            var exists = db.Suppliers.Any(s => s.Name == normalizedName);
            if (exists)
            {
                ViewBag.Error = "Постачальник з такою назвою вже існує.";
                return View();
            }

            var supplier = new Supplier(
                normalizedName,
                contactPerson ?? string.Empty,
                email ?? string.Empty,
                phone ?? string.Empty,
                cooperationTerms ?? string.Empty,
                pricingNotes ?? string.Empty,
                isActive);

            db.Suppliers.Add(supplier);
            db.SaveChanges();

            return RedirectToAction(nameof(Details), new { id = supplier.Id });
        }

        public IActionResult Details(int id)
        {
            var supplier = db.Suppliers
                .Include(s => s.Contracts)
                .Include(s => s.Purchases)
                .ThenInclude(p => p.Product)
                .Include(s => s.Purchases)
                .ThenInclude(p => p.Contract)
                .FirstOrDefault(s => s.Id == id);

            if (supplier == null)
            {
                return NotFound();
            }

            var model = new SupplierDetailsViewModel
            {
                Supplier = supplier,
                Contracts = supplier.Contracts
                    .OrderByDescending(c => c.StartDate)
                    .ToList(),
                RecentPurchases = supplier.Purchases
                    .OrderByDescending(p => p.ArrivalDate)
                    .Take(50)
                    .ToList()
            };

            var currentUser = AuthController.GetCurrentUser(HttpContext);
            ViewBag.CanManage = currentUser != null && CanManageSuppliers(currentUser);
            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(model);
        }

        [HttpPost]
        public IActionResult AddContract(
            int supplierId,
            string contractNumber,
            DateTime startDate,
            DateTime? endDate,
            string? paymentTerms,
            string? deliveryTerms,
            bool isActive = true)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!CanManageSuppliers(user))
            {
                return Content("Доступ заборонено");
            }

            var supplier = db.Suppliers.FirstOrDefault(s => s.Id == supplierId);
            if (supplier == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(contractNumber))
            {
                TempData["Error"] = "Вкажіть номер договору.";
                return RedirectToAction(nameof(Details), new { id = supplierId });
            }

            if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            {
                TempData["Error"] = "Дата завершення не може бути раніше дати початку.";
                return RedirectToAction(nameof(Details), new { id = supplierId });
            }

            var normalizedNumber = contractNumber.Trim();
            var exists = db.SupplierContracts.Any(c =>
                c.SupplierId == supplierId &&
                c.ContractNumber == normalizedNumber);

            if (exists)
            {
                TempData["Error"] = "Договір із таким номером уже існує для цього постачальника.";
                return RedirectToAction(nameof(Details), new { id = supplierId });
            }

            var contract = new SupplierContract(
                supplier,
                normalizedNumber,
                startDate,
                endDate,
                paymentTerms ?? string.Empty,
                deliveryTerms ?? string.Empty,
                isActive);

            db.SupplierContracts.Add(contract);
            db.SaveChanges();

            TempData["Success"] = "Договір постачання додано.";
            return RedirectToAction(nameof(Details), new { id = supplierId });
        }

        private static bool CanManageSuppliers(User user)
        {
            return string.Equals(user.Role?.Name, RoleNames.Owner, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(user.Role?.Name, RoleNames.Manager, StringComparison.OrdinalIgnoreCase);
        }
    }
}
