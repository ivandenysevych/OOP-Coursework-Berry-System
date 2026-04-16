using System;
using System.Collections.Generic;
using System.Linq;

namespace WarehouseWeb.Models
{
    public class ProductFactory
    {
        private static readonly HashSet<string> AllowedUnits = new(StringComparer.OrdinalIgnoreCase)
        {
            "kg",
            "g",
            "pcs",
            "box"
        };

        private ProductFactory() { }

        public static Product CreateProduct(
            string name,
            string category,
            decimal quantity,
            decimal price,
            string unit,
            StorageZone? zone = null,
            string createdBy = "system")
        {
            ValidateUnit(unit);

            var normalizedUnit = NormalizeUnit(unit);

            var product = new Product(name, category, quantity, price, normalizedUnit)
            {
                LastModifiedBy = createdBy,
                LastModifiedAt = DateTime.UtcNow
            };

            if (zone != null)
            {
                zone.AddProduct(product);
                product.Zone = zone;
            }

            return product;
        }

        public static bool IsSupportedUnit(string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return false;
            }

            return AllowedUnits.Contains(unit.Trim());
        }

        public static string NormalizeUnit(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return string.Empty;
            }

            return unit.Trim().ToLowerInvariant();
        }

        public static IReadOnlyList<string> GetAllowedUnits()
        {
            return AllowedUnits.OrderBy(x => x).ToList();
        }

        private static void ValidateUnit(string unit)
        {
            if (!IsSupportedUnit(unit))
            {
                throw new ArgumentException("Непідтримувана одиниця виміру. Доступно: kg, g, pcs, box.");
            }
        }
    }
}
