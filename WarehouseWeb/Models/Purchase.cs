using System;
using System.ComponentModel.DataAnnotations;

namespace WarehouseWeb.Models
{
    public enum PurchaseStatus
    {
        Draft,
        Received,
        Rejected,
        Cancelled
    }

    public enum PurchaseQualityStatus
    {
        Pending,
        Accepted,
        AcceptedWithRemarks,
        Rejected
    }

    public class Purchase
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        public int? SupplierContractId { get; set; }
        public SupplierContract? Contract { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int? StorageZoneId { get; set; }
        public StorageZone? StorageZone { get; set; }

        public int? MovementId { get; set; }
        public Movement? Movement { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalCost { get; set; }

        public DateTime ArrivalDate { get; set; } = DateTime.UtcNow;
        public DateTime? PaymentDueDate { get; set; }

        public bool IsDirectReceipt { get; set; }

        [MaxLength(180)]
        public string DirectSourceName { get; set; } = string.Empty;

        public ProcurementPaymentMethod? DirectPaymentMethod { get; set; }

        [MaxLength(600)]
        public string PaymentTerms { get; set; } = string.Empty;

        public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;
        public PurchaseQualityStatus QualityStatus { get; set; } = PurchaseQualityStatus.Pending;

        [MaxLength(600)]
        public string QualityNotes { get; set; } = string.Empty;

        [MaxLength(80)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [MaxLength(80)]
        public string AcceptanceActNumber { get; set; } = string.Empty;

        [MaxLength(120)]
        public string CreatedBy { get; set; } = "system";

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public bool InventoryPosted { get; set; }
        public DateTime? InventoryPostedAt { get; set; }

        public Purchase() { }

        public Purchase(
            Supplier supplier,
            Product product,
            SupplierContract? contract,
            StorageZone? storageZone,
            decimal quantity,
            decimal unitPrice,
            DateTime arrivalDate,
            DateTime? paymentDueDate,
            string paymentTerms,
            PurchaseQualityStatus qualityStatus,
            string qualityNotes,
            string invoiceNumber,
            string acceptanceActNumber,
            string createdBy,
            bool isDirectReceipt = false,
            string? directSourceName = null,
            ProcurementPaymentMethod? directPaymentMethod = null)
        {
            if (quantity <= 0)
                throw new ArgumentException("Кількість має бути > 0");

            if (unitPrice < 0)
                throw new ArgumentException("Ціна не може бути < 0");

            Supplier = supplier ?? throw new ArgumentNullException(nameof(supplier));
            SupplierId = supplier.Id;

            Product = product ?? throw new ArgumentNullException(nameof(product));
            ProductId = product.Id;

            Contract = contract;
            SupplierContractId = contract?.Id;

            StorageZone = storageZone;
            StorageZoneId = storageZone?.Id;

            Quantity = quantity;
            UnitPrice = unitPrice;
            TotalCost = Math.Round(quantity * unitPrice, 2);

            ArrivalDate = arrivalDate == default ? DateTime.UtcNow : arrivalDate;
            PaymentDueDate = paymentDueDate;

            PaymentTerms = paymentTerms ?? string.Empty;
            QualityStatus = qualityStatus;
            IsDirectReceipt = isDirectReceipt;
            DirectSourceName = directSourceName?.Trim() ?? string.Empty;
            DirectPaymentMethod = directPaymentMethod;

            Status = qualityStatus == PurchaseQualityStatus.Rejected
                ? PurchaseStatus.Rejected
                : PurchaseStatus.Received;

            QualityNotes = qualityNotes ?? string.Empty;

            InvoiceNumber = invoiceNumber ?? string.Empty;
            AcceptanceActNumber = acceptanceActNumber ?? string.Empty;

            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy;
            CreatedDate = DateTime.UtcNow;
        }

        public Movement CreateMovement(InventoryManager inventoryManager)
        {
            if (Product == null)
                throw new InvalidOperationException("Продукт не заданий");

            Product.InventoryManager = inventoryManager;

            var movement = new Movement(
                MovementType.Add,
                Quantity,
                Product,
                null,
                StorageZone
            );

            Movement = movement;

            return movement;
        }

        public void MarkInventoryPosted()
        {
            InventoryPosted = true;
            InventoryPostedAt = DateTime.UtcNow;

            Product?.InventoryManager?.Notify(Product, "Закупівля додана");
        }
    }
}
