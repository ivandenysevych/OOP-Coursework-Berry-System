using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseWeb.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(180)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(120)]
        public string Category { get; set; } = string.Empty;

        public decimal Quantity { get; set; }
        public decimal Price { get; set; }

        [Required]
        [MaxLength(20)]
        public string Unit { get; set; } = string.Empty;

        public int? StorageZoneId { get; set; }
        public StorageZone? Zone { get; set; }

        [MaxLength(120)]
        public string LastModifiedBy { get; set; } = "system";

        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

        public List<Movement> Movements { get; set; } = new();
        public List<Purchase> Purchases { get; set; } = new();
        public List<Sale> Sales { get; set; } = new();
        public List<Procurement> Procurements { get; set; } = new();

        // ❗ ВАЖЛИВО: НЕ ЙДЕ В БД
        [NotMapped]
        public InventoryManager? InventoryManager { get; set; }

        public Product() { }

        public Product(string name, string category, decimal quantity, decimal price, string unit)
        {
            Name = name.Trim();
            Category = category.Trim();
            Quantity = quantity;
            Price = price;
            Unit = unit.Trim().ToLowerInvariant();
            LastModifiedBy = "system";
            LastModifiedAt = DateTime.UtcNow;
        }

        public void Update(string name, string category, decimal quantity, decimal price, string unit, string user)
        {
            Name = name.Trim();
            Category = category.Trim();
            Quantity = quantity;
            Price = price;
            Unit = unit.Trim().ToLowerInvariant();
            Touch(user);

            InventoryManager?.Notify(this, "Оновлено продукт");
        }

        public void UpdateQuantity(decimal quantity, string user)
        {
            if (quantity < 0)
                throw new ArgumentException("Кількість не може бути від'ємною.");

            Quantity = quantity;
            Touch(user);

            InventoryManager?.Notify(this, "Оновлено кількість");
        }

        public void UpdatePrice(decimal price, string user)
        {
            if (price < 0)
                throw new ArgumentException("Ціна не може бути від'ємною.");

            Price = price;
            Touch(user);

            InventoryManager?.Notify(this, "Оновлено ціну");
        }

        public void IncreaseQuantity(decimal amount, string user)
        {
            if (amount <= 0)
                throw new ArgumentException("Кількість має бути > 0");

            Quantity += amount;
            Touch(user);

            InventoryManager?.Notify(this, "Збільшено кількість");
        }

        public void DecreaseQuantity(decimal amount, string user)
        {
            if (amount <= 0 || amount > Quantity)
                throw new ArgumentException("Некоректне значення кількості.");

            Quantity -= amount;
            Touch(user);

            InventoryManager?.Notify(this, "Зменшено кількість");
        }

        public void AssignZone(StorageZone zone, string user)
        {
            Zone = zone;
            StorageZoneId = zone.Id;
            Touch(user);

            InventoryManager?.Notify(this, "Змінено зону");
        }

        public void RemoveZone(string user)
        {
            Zone = null;
            StorageZoneId = null;
            Touch(user);

            InventoryManager?.Notify(this, "Видалено зону");
        }

        private void Touch(string user)
        {
            LastModifiedBy = string.IsNullOrWhiteSpace(user) ? "system" : user;
            LastModifiedAt = DateTime.UtcNow;
        }
    }
}
