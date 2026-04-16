using System.Collections.Generic;
using WarehouseWeb.Models;

namespace WarehouseWeb.ViewModels
{
    public class FinanceAccountOptionViewModel
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class FinanceCompanySectionViewModel
    {
        public Company Company { get; set; } = null!;
        public FinanceAccount CompanyAccount { get; set; } = null!;
        public IReadOnlyList<FinanceAccount> WarehouseAccounts { get; set; } = new List<FinanceAccount>();
        public IReadOnlyList<FinanceAccount> CollectorAccounts { get; set; } = new List<FinanceAccount>();
    }

    public class FinanceDashboardViewModel
    {
        public IReadOnlyList<FinanceCompanySectionViewModel> Companies { get; set; } = new List<FinanceCompanySectionViewModel>();
        public IReadOnlyList<FinanceAccountOptionViewModel> AccountOptions { get; set; } = new List<FinanceAccountOptionViewModel>();
        public IReadOnlyList<FinanceTransaction> RecentTransactions { get; set; } = new List<FinanceTransaction>();
    }
}
