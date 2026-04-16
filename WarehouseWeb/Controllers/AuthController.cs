using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WarehouseWeb.Data;
using WarehouseWeb.Models;

namespace WarehouseWeb.Controllers
{
    public class AuthController : Controller
    {
        private const string SessionUserIdKey = "CurrentUserId";

        private readonly WarehouseDbContext db;
        private readonly WarehouseManagementSystem warehouseManagementSystem;

        public AuthController(WarehouseDbContext db, WarehouseManagementSystem warehouseManagementSystem)
        {
            this.db = db;
            this.warehouseManagementSystem = warehouseManagementSystem;
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null, string? invite = null)
        {
            var currentUser = GetCurrentUser(HttpContext);
            if (currentUser != null)
            {
                return IsCollector(currentUser)
                    ? RedirectToAction("CollectorDashboard", "Home")
                    : RedirectToAction("Dashboard", "Home");
            }

            var invitation = GetValidInvitation(invite);
            if (!string.IsNullOrWhiteSpace(invite) && invitation == null)
            {
                ViewBag.Error = "Посилання-запрошення недійсне або протерміноване.";
            }

            ViewBag.ReturnUrl = returnUrl;
            ViewBag.InviteToken = invite;
            ViewBag.Invitation = invitation;
            return View();
        }

        [HttpPost]
        public IActionResult Register(
            string username,
            string password,
            string email,
            string role,
            string? returnUrl = null,
            string? invite = null)
        {
            var invitation = GetValidInvitation(invite);
            var inviteProvided = !string.IsNullOrWhiteSpace(invite);

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(email) ||
                (invitation == null && string.IsNullOrWhiteSpace(role)))
            {
                ViewBag.Error = "Будь ласка, заповніть усі поля.";
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.InviteToken = invite;
                ViewBag.Invitation = invitation;
                return View();
            }

            if (inviteProvided && invitation == null)
            {
                ViewBag.Error = "Посилання-запрошення недійсне або протерміноване.";
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.InviteToken = invite;
                return View();
            }

            if (db.Users.Any(u => u.Email == email.Trim()))
            {
                ViewBag.Error = "Користувач із таким email вже існує.";
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.InviteToken = invite;
                ViewBag.Invitation = invitation;
                return View();
            }

            if (invitation != null &&
                !string.IsNullOrWhiteSpace(invitation.Email) &&
                !string.Equals(invitation.Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = $"Це запрошення створено для email: {invitation.Email}.";
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.InviteToken = invite;
                ViewBag.Invitation = invitation;
                return View();
            }

            var roleEntity = invitation?.Role ?? db.Roles.FirstOrDefault(r => r.Name == role.Trim());
            if (roleEntity == null)
            {
                ViewBag.Error = "Обрана роль не знайдена.";
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.InviteToken = invite;
                ViewBag.Invitation = invitation;
                return View();
            }

            var user = new User(username.Trim(), password, email.Trim(), roleEntity);

            db.Users.Add(user);
            db.SaveChanges();

            if (invitation != null && invitation.Company != null)
            {
                var linkExists = db.CompanyUsers.Any(cu => cu.CompanyId == invitation.CompanyId && cu.UserId == user.Id);
                if (!linkExists)
                {
                    var membership = new CompanyUser(user, invitation.Company, roleEntity);
                    db.CompanyUsers.Add(membership);
                }

                invitation.IsUsed = true;
                invitation.UsedAt = DateTime.UtcNow;
                invitation.UsedByUserId = user.Id;
                invitation.UsedByName = user.Name;

                db.SaveChanges();
            }

            warehouseManagementSystem.AddUser(user);
            HttpContext.Session.SetInt32(SessionUserIdKey, user.Id);

            return RedirectAfterAuth(returnUrl);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            var currentUser = GetCurrentUser(HttpContext);
            if (currentUser != null)
            {
                return IsCollector(currentUser)
                    ? RedirectToAction("CollectorDashboard", "Home")
                    : RedirectToAction("Dashboard", "Home");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string password, string? returnUrl = null)
        {
            var loginValue = username?.Trim() ?? string.Empty;

            var user = db.Users
                .Include(u => u.Role)
                .FirstOrDefault(u =>
                    (u.Name == loginValue || u.Email == loginValue) &&
                    u.Password == password);

            if (user == null)
            {
                ViewBag.Error = "Неправильний логін або пароль.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            warehouseManagementSystem.AddUser(user);

            var loggedUser = warehouseManagementSystem.Login(loginValue, password) ?? user;
            loggedUser.Login();
            db.SaveChanges();

            HttpContext.Session.SetInt32(SessionUserIdKey, loggedUser.Id);

            return RedirectAfterAuth(returnUrl);
        }

        public IActionResult Logout()
        {
            var user = GetCurrentUser(HttpContext);
            user?.Logout();
            db.SaveChanges();

            HttpContext.Session.Remove(SessionUserIdKey);
            return RedirectToAction("Index", "Home");
        }

        public static User? GetCurrentUser(HttpContext httpContext)
        {
            var userId = httpContext.Session.GetInt32(SessionUserIdKey);
            if (!userId.HasValue)
            {
                return null;
            }

            var db = httpContext.RequestServices.GetRequiredService<WarehouseDbContext>();

            return db.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.Id == userId.Value);
        }

        public static bool IsWorker(HttpContext httpContext)
        {
            return string.Equals(
                GetCurrentUser(httpContext)?.Role?.Name,
                RoleNames.Worker,
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCollector(HttpContext httpContext)
        {
            return string.Equals(
                GetCurrentUser(httpContext)?.Role?.Name,
                RoleNames.Collector,
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCollector(User? user)
        {
            return string.Equals(
                user?.Role?.Name,
                RoleNames.Collector,
                StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult RedirectAfterAuth(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var currentUser = GetCurrentUser(HttpContext);
            if (IsCollector(currentUser))
            {
                return RedirectToAction("CollectorDashboard", "Home");
            }

            return RedirectToAction("Dashboard", "Home");
        }

        private CompanyInvitation? GetValidInvitation(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var now = DateTime.UtcNow;

            return db.CompanyInvitations
                .Include(i => i.Company)
                .Include(i => i.Role)
                .FirstOrDefault(i =>
                    i.Token == token &&
                    !i.IsUsed &&
                    i.ExpiresAt >= now);
        }
    }
}
