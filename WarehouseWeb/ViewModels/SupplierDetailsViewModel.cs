using System.Collections.Generic;
using WarehouseWeb.Models;

namespace WarehouseWeb.ViewModels
{
    public class SupplierDetailsViewModel
    {
        public Supplier Supplier { get; set; } = new();
        public IReadOnlyList<SupplierContract> Contracts { get; set; } = new List<SupplierContract>();
        public IReadOnlyList<Purchase> RecentPurchases { get; set; } = new List<Purchase>();
    }
}
