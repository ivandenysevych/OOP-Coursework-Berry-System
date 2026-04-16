using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WarehouseWeb.Models
{
    public class SupplierContract
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        [Required]
        [MaxLength(80)]
        public string ContractNumber { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [MaxLength(600)]
        public string PaymentTerms { get; set; } = string.Empty;

        [MaxLength(600)]
        public string DeliveryTerms { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public List<Purchase> Purchases { get; set; } = new();

        public SupplierContract() { }

        public SupplierContract(
            Supplier supplier,
            string contractNumber,
            DateTime startDate,
            DateTime? endDate,
            string paymentTerms,
            string deliveryTerms,
            bool isActive = true)
        {
            Supplier = supplier ?? throw new ArgumentNullException(nameof(supplier));
            SupplierId = supplier.Id;

            ContractNumber = contractNumber.Trim();
            StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            EndDate = endDate.HasValue
                ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc)
                : null;

            PaymentTerms = paymentTerms?.Trim() ?? string.Empty;
            DeliveryTerms = deliveryTerms?.Trim() ?? string.Empty;

            IsActive = isActive;
            CreatedDate = DateTime.UtcNow;

            if (!supplier.Contracts.Contains(this))
                supplier.Contracts.Add(this);
        }

        public bool IsValidOn(DateTime dateUtc)
        {
            if (!IsActive)
                return false;

            if (dateUtc < StartDate)
                return false;

            return !EndDate.HasValue || dateUtc <= EndDate.Value;
        }

        public void AddPurchase(Purchase purchase)
        {
            if (purchase == null)
                throw new ArgumentNullException(nameof(purchase));

            if (!Purchases.Contains(purchase))
            {
                Purchases.Add(purchase);
                purchase.Contract = this;
                purchase.SupplierContractId = Id;
            }
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}