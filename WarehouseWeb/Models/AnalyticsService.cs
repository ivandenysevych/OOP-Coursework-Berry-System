using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WarehouseWeb.Models
{
    public class AnalyticsService : IInventoryObserver
    {
        private readonly List<string> notifications = new();

        public decimal CalculateStockValue(IEnumerable<Product> products)
        {
            return products.Sum(p => p.Price * p.Quantity);
        }

        public decimal CalculateAveragePrice(IEnumerable<Product> products)
        {
            var list = products.ToList();
            if (list.Count == 0)
                return 0;

            return list.Average(p => p.Price);
        }

        public string GenerateReport(IEnumerable<Product> products)
        {
            var list = products.ToList();
            var sb = new StringBuilder();

            sb.AppendLine($"Кількість товарів: {list.Count}");
            sb.AppendLine($"Середня ціна: {CalculateAveragePrice(list):0.00}");
            sb.AppendLine($"Загальна вартість запасів: {CalculateStockValue(list):0.00}");

            foreach (var product in list.OrderBy(p => p.Name))
            {
                sb.AppendLine(
                    $"{product.Name} | к-сть: {product.Quantity} {product.Unit} | ціна: {product.Price:0.00} | сума: {(product.Quantity * product.Price):0.00}");
            }

            return sb.ToString();
        }

        public IReadOnlyList<string> GetNotifications()
        {
            return notifications;
        }

        public void Update(Product product, string action)
        {
            notifications.Add(
                $"[{action}] {product.Name} | {product.Quantity} {product.Unit} | {product.LastModifiedAt:u} | {product.LastModifiedBy}");
        }
    }
}