using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Data;
using WarehouseWeb.Models;

namespace WarehouseWeb.Controllers
{
    public class CompanyController : Controller
    {
        private readonly WarehouseDbContext db;
        private readonly WarehouseManagementSystem warehouseManagementSystem;

        public CompanyController(WarehouseDbContext db, WarehouseManagementSystem warehouseManagementSystem)
        {
            this.db = db;
            this.warehouseManagementSystem = warehouseManagementSystem;
        }

        public IActionResult Index()
        {
            var companies = db.Companies
                .Include(c => c.Warehouses)
                .ThenInclude(w => w.Zones)
                .ThenInclude(z => z.Products)
                .Include(c => c.Employees)
                .ThenInclude(cu => cu.User)
                .OrderBy(c => c.Name)
                .ToList();

            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(companies);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (!CanCreateCompany())
            {
                return Content("Доступ заборонено");
            }

            return View();
        }

        [HttpPost]
        public IActionResult Create(string name, string description)
        {
            if (!CanCreateCompany())
            {
                return Content("Доступ заборонено");
            }

            var currentUser = AuthController.GetCurrentUser(HttpContext);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                ViewBag.Error = "Вкажіть назву компанії.";
                return View();
            }

            var ownerRole = GetRoleByName(RoleNames.Owner);
            if (ownerRole == null)
            {
                ViewBag.Error = "Роль власника не знайдена.";
                return View();
            }

            var company = warehouseManagementSystem.RegisterCompany(name.Trim(), description?.Trim() ?? string.Empty);
            db.Companies.Add(company);
            db.SaveChanges();

            var ownerLinkExists = db.CompanyUsers.Any(cu => cu.CompanyId == company.Id && cu.UserId == currentUser.Id);
            if (!ownerLinkExists)
            {
                var ownerLink = new CompanyUser(currentUser, company, ownerRole);
                company.AddEmployee(ownerLink);
                db.CompanyUsers.Add(ownerLink);
                db.SaveChanges();
            }

            TempData["Success"] = "Компанію створено. Ви призначені її власником.";
            return RedirectToAction(nameof(Details), new { id = company.Id });
        }

        public IActionResult Details(int id)
        {
            var company = db.Companies
                .Include(c => c.Warehouses)
                .ThenInclude(w => w.Zones)
                .ThenInclude(z => z.Products)
                .Include(c => c.Employees)
                .ThenInclude(cu => cu.User)
                .Include(c => c.Employees)
                .ThenInclude(cu => cu.Role)
                .FirstOrDefault(c => c.Id == id);

            if (company == null)
            {
                return NotFound();
            }

            var currentUser = AuthController.GetCurrentUser(HttpContext);
            var isCompanyOwner = IsCompanyOwner(id);
            var canManageStructure = CanManageCompanyStructure(id);
            var invitations = db.CompanyInvitations
                .Include(i => i.Role)
                .Include(i => i.CreatedByUser)
                .Include(i => i.UsedByUser)
                .Where(i => i.CompanyId == id)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();

            ViewBag.Users = db.Users.Include(u => u.Role).OrderBy(u => u.Name).ToList();
            ViewBag.Roles = db.Roles.OrderBy(r => r.Name).ToList();
            ViewBag.IsCompanyOwner = isCompanyOwner;
            ViewBag.CanManageStructure = canManageStructure;
            ViewBag.CurrentUserId = currentUser?.Id;
            ViewBag.Invitations = invitations;
            ViewBag.GeneratedInviteLink = TempData["GeneratedInviteLink"];
            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(company);
        }

        [HttpPost]
        public IActionResult UpdateCompany(int companyId, string name, string description)
        {
            if (!CanManageCompanyStructure(companyId))
            {
                TempData["Error"] = "Недостатньо прав для редагування компанії.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var company = db.Companies.FirstOrDefault(c => c.Id == companyId);
            if (company == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Вкажіть назву компанії.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            company.Name = name.Trim();
            company.Description = description?.Trim() ?? string.Empty;
            db.SaveChanges();

            TempData["Success"] = "Дані компанії оновлено.";
            return RedirectToAction(nameof(Details), new { id = companyId });
        }

        [HttpPost]
        public IActionResult AddWarehouse(int companyId, string name, string location, string description)
        {
            if (!CanManageCompanyStructure(companyId))
            {
                TempData["Error"] = "Недостатньо прав для додавання складу.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Вкажіть назву складу.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var company = db.Companies
                .Include(c => c.Warehouses)
                .FirstOrDefault(c => c.Id == companyId);

            if (company == null)
            {
                return NotFound();
            }

            var warehouse = new Warehouse(
                name.Trim(),
                location?.Trim() ?? string.Empty,
                description?.Trim() ?? string.Empty,
                company);

            company.AddWarehouse(warehouse);
            db.Warehouses.Add(warehouse);
            db.SaveChanges();

            TempData["Success"] = "Склад додано.";
            return RedirectToAction(nameof(Details), new { id = companyId });
        }

        [HttpPost]
        public IActionResult UpdateWarehouse(int companyId, int warehouseId, string name, string location, string description)
        {
            if (!CanManageCompanyStructure(companyId))
            {
                TempData["Error"] = "Недостатньо прав для редагування складу.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var warehouse = db.Warehouses
                .FirstOrDefault(w => w.Id == warehouseId && w.CompanyId == companyId);

            if (warehouse == null)
            {
                TempData["Error"] = "Склад не знайдено.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Назва складу не може бути порожньою.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            warehouse.Name = name.Trim();
            warehouse.Location = location?.Trim() ?? string.Empty;
            warehouse.Description = description?.Trim() ?? string.Empty;
            db.SaveChanges();

            TempData["Success"] = "Дані складу оновлено.";
            return RedirectToAction(nameof(Details), new { id = companyId });
        }

        [HttpPost]
        public IActionResult AddZone(int warehouseId, string name, string type, double capacity)
        {
            var warehouse = db.Warehouses
                .Include(w => w.Zones)
                .FirstOrDefault(w => w.Id == warehouseId);

            if (warehouse == null)
            {
                return NotFound();
            }

            var companyId = warehouse.CompanyId;

            if (!CanManageCompanyStructure(companyId))
            {
                TempData["Error"] = "Недостатньо прав для додавання зони.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
            {
                TempData["Error"] = "Вкажіть назву і тип зони.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            if (capacity < 0)
            {
                TempData["Error"] = "Місткість не може бути від'ємною.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var zone = new StorageZone(name.Trim(), type.Trim(), capacity, warehouse);
            warehouse.AddZone(zone);

            db.StorageZones.Add(zone);
            db.SaveChanges();

            TempData["Success"] = "Зону додано.";
            return RedirectToAction(nameof(Details), new { id = companyId });
        }

        [HttpPost]
        public IActionResult UpdateZone(int companyId, int zoneId, string name, string type, double capacity)
        {
            if (!CanManageCompanyStructure(companyId))
            {
                TempData["Error"] = "Недостатньо прав для редагування зони.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var zone = db.StorageZones
                .Include(z => z.Warehouse)
                .Include(z => z.Products)
                .FirstOrDefault(z => z.Id == zoneId &&
                                     z.Warehouse != null &&
                                     z.Warehouse.CompanyId == companyId);

            if (zone == null)
            {
                TempData["Error"] = "Зону не знайдено.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
            {
                TempData["Error"] = "Назва і тип зони обов'язкові.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            if (capacity < 0)
            {
                TempData["Error"] = "Місткість не може бути від'ємною.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var currentLoad = zone.Products.Sum(p => (double)p.Quantity);
            if (capacity > 0 && currentLoad > capacity)
            {
                TempData["Error"] = $"Неможливо зменшити місткість до {capacity}, поточне завантаження: {currentLoad}.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            zone.Name = name.Trim();
            zone.Type = type.Trim();
            zone.Capacity = capacity;

            db.SaveChanges();

            TempData["Success"] = "Дані зони оновлено.";
            return RedirectToAction(nameof(Details), new { id = companyId });
        }

        [HttpPost]
        public IActionResult DeleteWarehouse(int companyId, int warehouseId, string password)
        {
            if (!IsCompanyOwner(companyId))
            {
                TempData["Error"] = "Лише власник компанії може видаляти склади.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var currentUser = AuthController.GetCurrentUser(HttpContext);
            if (currentUser == null || !PasswordMatches(currentUser, password))
            {
                TempData["Error"] = "Неправильний пароль.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var warehouse = db.Warehouses
                .Include(w => w.Zones)
                .FirstOrDefault(w => w.Id == warehouseId && w.CompanyId == companyId);

            if (warehouse == null)
            {
                TempData["Error"] = "Склад не знайдено.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            try
            {
                db.Warehouses.Remove(warehouse);
                db.SaveChanges();
                TempData["Success"] = "Склад видалено.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Не вдалося видалити склад через пов'язані записи.";
            }

            return RedirectToAction(nameof(Details), new { id = companyId });
        }

        [HttpPost]
        public IActionResult DeleteCompany(int companyId, string password)
        {
            if (!IsCompanyOwner(companyId))
            {
                TempData["Error"] = "Лише власник компанії може видалити компанію.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var currentUser = AuthController.GetCurrentUser(HttpContext);
            if (currentUser == null || !PasswordMatches(currentUser, password))
            {
                TempData["Error"] = "Неправильний пароль.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var company = db.Companies
                .Include(c => c.Warehouses)
                .ThenInclude(w => w.Zones)
                .Include(c => c.Employees)
                .FirstOrDefault(c => c.Id == companyId);

            if (company == null)
            {
                return NotFound();
            }

            try
            {
                db.Companies.Remove(company);
                db.SaveChanges();
                TempData["Success"] = "Компанію видалено.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Не вдалося видалити компанію через пов'язані записи.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }
        }

        [HttpPost]
        public IActionResult AddEmployee(int companyId, int userId, int roleId)
        {
            if (!IsCompanyOwner(companyId))
            {
                TempData["Error"] = "Лише власник компанії може призначати ролі працівникам.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var company = db.Companies
                .Include(c => c.Employees)
                .FirstOrDefault(c => c.Id == companyId);
            var user = db.Users.FirstOrDefault(u => u.Id == userId);
            var role = db.Roles.FirstOrDefault(r => r.Id == roleId);

            if (company == null || user == null || role == null)
            {
                return NotFound();
            }

            var exists = db.CompanyUsers.Any(cu => cu.CompanyId == companyId && cu.UserId == userId);
            if (exists)
            {
                TempData["Error"] = "Цей користувач вже доданий до компанії.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var employee = new CompanyUser(user, company, role);
            company.AddEmployee(employee);
            db.CompanyUsers.Add(employee);
            db.SaveChanges();

            TempData["Success"] = "Працівника додано до компанії.";
            return RedirectToAction(nameof(Details), new { id = companyId });
        }

        [HttpPost]
        public IActionResult CreateInvitation(int companyId, int roleId, string? email, int validDays = 7)
        {
            if (!IsCompanyOwner(companyId))
            {
                TempData["Error"] = "Лише власник компанії може створювати запрошення.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var company = db.Companies.FirstOrDefault(c => c.Id == companyId);
            var role = db.Roles.FirstOrDefault(r => r.Id == roleId);
            var currentUser = AuthController.GetCurrentUser(HttpContext);

            if (company == null || role == null || currentUser == null)
            {
                TempData["Error"] = "Не вдалося створити запрошення.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            if (validDays < 1 || validDays > 60)
            {
                validDays = 7;
            }

            var invitation = new CompanyInvitation
            {
                CompanyId = company.Id,
                RoleId = role.Id,
                Email = email?.Trim() ?? string.Empty,
                Token = Guid.NewGuid().ToString("N"),
                CreatedByName = currentUser.Name,
                CreatedByUserId = currentUser.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(validDays),
                IsUsed = false
            };

            db.CompanyInvitations.Add(invitation);
            db.SaveChanges();

            var inviteLink = Url.Action("Register", "Auth", new { invite = invitation.Token }, Request.Scheme) ?? string.Empty;
            TempData["GeneratedInviteLink"] = inviteLink;
            TempData["Success"] = "Посилання-запрошення створено.";

            return RedirectToAction(nameof(Details), new { id = companyId });
        }

        [HttpPost]
        public IActionResult RevokeInvitation(int companyId, int invitationId)
        {
            if (!IsCompanyOwner(companyId))
            {
                TempData["Error"] = "Лише власник компанії може відкликати запрошення.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var invitation = db.CompanyInvitations
                .FirstOrDefault(i => i.Id == invitationId && i.CompanyId == companyId);

            if (invitation == null)
            {
                TempData["Error"] = "Запрошення не знайдено.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            if (invitation.IsUsed)
            {
                TempData["Error"] = "Запрошення вже використано, його не можна відкликати.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            db.CompanyInvitations.Remove(invitation);
            db.SaveChanges();

            TempData["Success"] = "Запрошення відкликано.";
            return RedirectToAction(nameof(Details), new { id = companyId });
        }

        [HttpPost]
        public IActionResult UpdateEmployeeRole(int companyId, int companyUserId, int roleId)
        {
            if (!IsCompanyOwner(companyId))
            {
                TempData["Error"] = "Лише власник компанії може змінювати ролі.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var employee = db.CompanyUsers
                .Include(cu => cu.User)
                .Include(cu => cu.Role)
                .FirstOrDefault(cu => cu.Id == companyUserId && cu.CompanyId == companyId);

            var newRole = db.Roles.FirstOrDefault(r => r.Id == roleId);

            if (employee == null || newRole == null)
            {
                TempData["Error"] = "Не вдалося оновити роль.";
                return RedirectToAction(nameof(Details), new { id = companyId });
            }

            var ownerRoleId = db.Roles
                .Where(r => r.Name.ToLower() == RoleNames.Owner.ToLower())
                .Select(r => r.Id)
                .FirstOrDefault();

            if (ownerRoleId > 0 && employee.RoleId == ownerRoleId && newRole.Id != ownerRoleId)
            {
                var ownerCount = db.CompanyUsers.Count(cu => cu.CompanyId == companyId && cu.RoleId == ownerRoleId);
                if (ownerCount <= 1)
                {
                    TempData["Error"] = "Має бути хоча б один власник.";
                    return RedirectToAction(nameof(Details), new { id = companyId });
                }
            }

            employee.RoleId = newRole.Id;
            db.SaveChanges();

            TempData["Success"] = "Роль оновлено.";
            return RedirectToAction(nameof(Details), new { id = companyId });
        }

        private bool CanCreateCompany()
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            return user?.Role?.Name?.ToLower() == RoleNames.Owner.ToLower() ||
                   user?.Role?.Name?.ToLower() == RoleNames.Manager.ToLower();
        }

        private bool CanManageCompanyStructure(int companyId)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null) return false;

            var companyRoleName = db.CompanyUsers
                .Where(cu => cu.CompanyId == companyId && cu.UserId == user.Id)
                .Select(cu => cu.Role != null ? cu.Role.Name : "")
                .FirstOrDefault();

            return companyRoleName?.ToLower() == RoleNames.Owner.ToLower() ||
                   companyRoleName?.ToLower() == RoleNames.Manager.ToLower();
        }

        private bool IsCompanyOwner(int companyId)
        {
            var user = AuthController.GetCurrentUser(HttpContext);
            if (user == null) return false;

            var companyRoleName = db.CompanyUsers
                .Where(cu => cu.CompanyId == companyId && cu.UserId == user.Id)
                .Select(cu => cu.Role != null ? cu.Role.Name : "")
                .FirstOrDefault();

            return companyRoleName?.ToLower() == RoleNames.Owner.ToLower();
        }

        private bool PasswordMatches(User user, string password)
        {
            return !string.IsNullOrWhiteSpace(password) && user.Password == password;
        }

        private Role? GetRoleByName(string roleName)
        {
            return db.Roles.FirstOrDefault(r => r.Name.ToLower() == roleName.ToLower());
        }
    }
}
