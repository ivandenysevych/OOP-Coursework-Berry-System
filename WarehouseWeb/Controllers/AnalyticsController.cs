using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Data;
using WarehouseWeb.Models;
using WarehouseWeb.ViewModels;

namespace WarehouseWeb.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly WarehouseDbContext db;
        private readonly AnalyticsService analyticsService;

        public AnalyticsController(WarehouseDbContext db, AnalyticsService analyticsService)
        {
            this.db = db;
            this.analyticsService = analyticsService;
        }

        public IActionResult Index()
        {
            var products = db.Products
                .Include(p => p.Zone)
                .ThenInclude(z => z!.Warehouse)
                .ThenInclude(w => w!.Company)
                .OrderBy(p => p.Name)
                .ToList();

            var startDate = DateTime.UtcNow.Date.AddDays(-13);

            var purchases = db.Purchases
                .Where(p => p.ArrivalDate >= startDate)
                .ToList();

            var sales = db.Sales
                .Where(s => s.SaleDate >= startDate)
                .ToList();

            var movements = db.Movements.ToList();

            var topProductsByValue = products
                .Select(p => new AnalyticsBarPointViewModel
                {
                    Label = p.Name,
                    Value = Math.Round(p.Quantity * p.Price, 2)
                })
                .OrderByDescending(x => x.Value)
                .Take(8)
                .ToList();

            var stockByCompany = products
                .GroupBy(p =>
                    p.Zone?.Warehouse?.Company?.Name ?? "Без компанії")
                .Select(g => new AnalyticsBarPointViewModel
                {
                    Label = g.Key,
                    Value = Math.Round(g.Sum(p => p.Quantity * p.Price), 2)
                })
                .OrderByDescending(x => x.Value)
                .ToList();

            var movementBreakdown = movements
                .GroupBy(m => m.Type)
                .Select(g => new AnalyticsBarPointViewModel
                {
                    Label = g.Key switch
                    {
                        MovementType.Add => "Надходження",
                        MovementType.Remove => "Списання",
                        MovementType.Move => "Переміщення",
                        _ => g.Key.ToString()
                    },
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .ToList();

            var dailyTurnover = BuildDailyTurnover(startDate, purchases, sales);

            var model = new AnalyticsDashboardViewModel
            {
                AveragePrice = analyticsService.CalculateAveragePrice(products),
                StockValue = analyticsService.CalculateStockValue(products),
                Report = analyticsService.GenerateReport(products),
                Notifications = analyticsService.GetNotifications()
                    .OrderByDescending(x => x)
                    .Take(15)
                    .ToList(),
                TopProductsByValue = topProductsByValue,
                StockByCompany = stockByCompany,
                MovementBreakdown = movementBreakdown,
                DailyTurnover = dailyTurnover
            };

            return View(model);
        }

        private static IReadOnlyList<AnalyticsDailyTurnoverViewModel> BuildDailyTurnover(
            DateTime startDate,
            List<Purchase> purchases,
            List<Sale> sales)
        {
            var result = new List<AnalyticsDailyTurnoverViewModel>();

            for (var date = startDate.Date; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
            {
                var purchaseTotal = purchases
                    .Where(p => p.ArrivalDate.Date == date)
                    .Sum(p => p.TotalCost);

                var saleTotal = sales
                    .Where(s => s.SaleDate.Date == date)
                    .Sum(s => s.TotalAmount);

                result.Add(new AnalyticsDailyTurnoverViewModel
                {
                    Label = date.ToString("dd.MM"),
                    PurchaseAmount = Math.Round(purchaseTotal, 2),
                    SaleAmount = Math.Round(saleTotal, 2)
                });
            }

            return result;
        }
    }
}
