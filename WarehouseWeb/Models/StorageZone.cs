using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace WarehouseWeb.Models
{
    public class StorageZone
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Type { get; set; } = string.Empty;

        public double Capacity { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }

        public List<Product> Products { get; set; } = new();
        public List<Purchase> Purchases { get; set; } = new();

        public StorageZone() { }

        public StorageZone(string name, string type, double capacity, Warehouse warehouse)
        {
            Name = name.Trim();
            Type = type.Trim();
            Capacity = capacity;
            Warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
            WarehouseId = warehouse.Id;
        }

        public double CurrentLoad()
        {
            return Products.Sum(p => (double)p.Quantity);
        }

        public void AddProduct(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            var projectedLoad = CurrentLoad() + (double)product.Quantity;

            if (Capacity > 0 && projectedLoad > Capacity)
                throw new InvalidOperationException("Перевищено місткість зони.");

            if (!Products.Contains(product))
                Products.Add(product);

            if (product.StorageZoneId != Id)
                product.AssignZone(this, "system");
        }

        public void RemoveProduct(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            Products.Remove(product);

            if (product.StorageZoneId == Id)
                product.RemoveZone("system");
        }
    }
}