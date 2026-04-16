using System.Collections.Generic;

namespace WarehouseWeb.ViewModels
{
    public class AnalyticsBarPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class AnalyticsDailyTurnoverViewModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal PurchaseAmount { get; set; }
        public decimal SaleAmount { get; set; }
    }

    public class AnalyticsDashboardViewModel
    {
        public decimal AveragePrice { get; set; }
        public decimal StockValue { get; set; }
        public string Report { get; set; } = string.Empty;
        public IReadOnlyList<string> Notifications { get; set; } = new List<string>();
        public IReadOnlyList<AnalyticsBarPointViewModel> TopProductsByValue { get; set; } = new List<AnalyticsBarPointViewModel>();
        public IReadOnlyList<AnalyticsBarPointViewModel> StockByCompany { get; set; } = new List<AnalyticsBarPointViewModel>();
        public IReadOnlyList<AnalyticsBarPointViewModel> MovementBreakdown { get; set; } = new List<AnalyticsBarPointViewModel>();
        public IReadOnlyList<AnalyticsDailyTurnoverViewModel> DailyTurnover { get; set; } = new List<AnalyticsDailyTurnoverViewModel>();
    }
}
