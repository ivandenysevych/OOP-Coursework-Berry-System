using System;
using System.ComponentModel.DataAnnotations;

namespace WarehouseWeb.Models
{
    public enum FinanceAccountType
    {
        Company = 1,
        Warehouse = 2,
        User = 3
    }

    public class FinanceAccount
    {
        public int Id { get; set; }

        public FinanceAccountType AccountType { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int? WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }

        [MaxLength(180)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(10)]
        public string Currency { get; set; } = "UAH";

        public decimal Balance { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public void Credit(decimal amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Сума поповнення має бути більшою за нуль.");
            }

            Balance += amount;
            Touch();
        }

        public void Debit(decimal amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Сума списання має бути більшою за нуль.");
            }

            if (Balance < amount)
            {
                throw new InvalidOperationException("Недостатньо коштів на рахунку.");
            }

            Balance -= amount;
            Touch();
        }

        public string DisplayLabel()
        {
            return string.IsNullOrWhiteSpace(Name)
                ? $"Рахунок #{Id}"
                : Name;
        }

        private void Touch()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
