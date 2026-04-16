using System;
using System.ComponentModel.DataAnnotations;

namespace WarehouseWeb.Models
{
    public enum ProcurementPaymentMethod
    {
        Cash,
        Card
    }

    public class Procurement
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int? CollectorUserId { get; set; }
        public User? CollectorUser { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        [MaxLength(120)]
        public string CollectorName { get; set; } = string.Empty;

        [MaxLength(180)]
        public string SupplierPersonName { get; set; } = string.Empty;

        [MaxLength(120)]
        public string ProductCategory { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        [MaxLength(20)]
        public string Unit { get; set; } = "kg";

        public decimal UnitPrice { get; set; }
        public decimal TotalCost { get; set; }

        public ProcurementPaymentMethod PaymentMethod { get; set; } = ProcurementPaymentMethod.Cash;
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;

        public bool IsTransferredToWarehouse { get; set; }
        public DateTime? TransferredAt { get; set; }

        [MaxLength(120)]
        public string TransferredBy { get; set; } = string.Empty;

        public int? TransferZoneId { get; set; }
        public StorageZone? TransferZone { get; set; }

        public int? ExpenseAccountId { get; set; }
        public FinanceAccount? ExpenseAccount { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public Procurement() { }

        public Procurement(
            Product product,
            User collector,
            string supplierPersonName,
            decimal quantity,
            string unit,
            decimal unitPrice,
            ProcurementPaymentMethod paymentMethod,
            DateTime collectedAt,
            string notes)
        {
            if (quantity <= 0)
                throw new ArgumentException("Кількість має бути > 0");

            if (!ProductFactory.IsSupportedUnit(unit))
                throw new ArgumentException("Непідтримувана одиниця виміру.");

            if (unitPrice < 0)
                throw new ArgumentException("Ціна не може бути < 0");

            Product = product ?? throw new ArgumentNullException(nameof(product));
            ProductId = product.Id;

            CollectorUser = collector ?? throw new ArgumentNullException(nameof(collector));
            CollectorUserId = collector.Id;
            CollectorName = collector.Name;

            SupplierPersonName = supplierPersonName?.Trim() ?? string.Empty;
            ProductCategory = product.Category;
            Quantity = quantity;
            Unit = ProductFactory.NormalizeUnit(unit);
            UnitPrice = unitPrice;
            TotalCost = Math.Round(quantity * unitPrice, 2);
            PaymentMethod = paymentMethod;
            CollectedAt = collectedAt == default ? DateTime.UtcNow : collectedAt;
            Notes = notes?.Trim() ?? string.Empty;
            IsTransferredToWarehouse = false;
            TransferredAt = null;
            TransferredBy = string.Empty;
            TransferZoneId = null;
            CreatedDate = DateTime.UtcNow;
        }
    }
}
