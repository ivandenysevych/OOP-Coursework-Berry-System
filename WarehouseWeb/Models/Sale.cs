using System;
using System.ComponentModel.DataAnnotations;

namespace WarehouseWeb.Models
{
    public enum SaleStatus
    {
        Draft,
        Completed,
        Cancelled
    }

    public class Sale
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int? MovementId { get; set; }
        public Movement? Movement { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }

        public DateTime SaleDate { get; set; } = DateTime.UtcNow;
        public SaleStatus Status { get; set; } = SaleStatus.Completed;

        [MaxLength(180)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(600)]
        public string PaymentTerms { get; set; } = string.Empty;

        [MaxLength(80)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [MaxLength(600)]
        public string Notes { get; set; } = string.Empty;

        [MaxLength(120)]
        public string CreatedBy { get; set; } = "system";

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public Sale() { }

        public Sale(
            Product product,
            decimal quantity,
            decimal unitPrice,
            DateTime saleDate,
            string customerName,
            string paymentTerms,
            string invoiceNumber,
            string notes,
            string createdBy)
        {
            if (quantity <= 0)
                throw new ArgumentException("Кількість має бути > 0");

            if (unitPrice < 0)
                throw new ArgumentException("Ціна не може бути < 0");

            Product = product ?? throw new ArgumentNullException(nameof(product));
            ProductId = product.Id;

            Quantity = quantity;
            UnitPrice = unitPrice;
            TotalAmount = Math.Round(quantity * unitPrice, 2);

            SaleDate = saleDate == default ? DateTime.UtcNow : saleDate;
            Status = SaleStatus.Completed;

            CustomerName = customerName?.Trim() ?? string.Empty;
            PaymentTerms = paymentTerms?.Trim() ?? string.Empty;
            InvoiceNumber = invoiceNumber?.Trim() ?? string.Empty;
            Notes = notes?.Trim() ?? string.Empty;
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy.Trim();
            CreatedDate = DateTime.UtcNow;
        }
    }
}
