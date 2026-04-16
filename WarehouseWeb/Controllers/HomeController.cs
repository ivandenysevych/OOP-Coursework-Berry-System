using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WarehouseWeb.Models;

namespace WarehouseWeb.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Dashboard()
    {
        var currentUser = AuthController.GetCurrentUser(HttpContext);
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = "/Home/Dashboard" });
        }

        if (AuthController.IsCollector(currentUser))
        {
            return RedirectToAction(nameof(CollectorDashboard));
        }

        return View();
    }

    public IActionResult CollectorDashboard()
    {
        var currentUser = AuthController.GetCurrentUser(HttpContext);
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = "/Home/CollectorDashboard" });
        }

        if (!AuthController.IsCollector(currentUser))
        {
            return RedirectToAction(nameof(Dashboard));
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
