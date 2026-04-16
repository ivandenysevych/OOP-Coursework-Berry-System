using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace WarehouseWeb.Models
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(180)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(120)]
        public string ContactPerson { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(40)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(700)]
        public string CooperationTerms { get; set; } = string.Empty;

        [MaxLength(500)]
        public string PricingNotes { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public List<SupplierContract> Contracts { get; set; } = new();
        public List<Purchase> Purchases { get; set; } = new();

        public Supplier() { }

        public Supplier(
            string name,
            string contactPerson,
            string email,
            string phone,
            string cooperationTerms,
            string pricingNotes,
            bool isActive = true)
        {
            Name = name.Trim();
            ContactPerson = contactPerson?.Trim() ?? string.Empty;
            Email = email?.Trim() ?? string.Empty;
            Phone = phone?.Trim() ?? string.Empty;
            CooperationTerms = cooperationTerms?.Trim() ?? string.Empty;
            PricingNotes = pricingNotes?.Trim() ?? string.Empty;
            CreatedDate = DateTime.UtcNow;
            IsActive = isActive;
        }

        public void AddContract(SupplierContract contract)
        {
            if (contract == null)
                throw new ArgumentNullException(nameof(contract));

            if (!Contracts.Contains(contract))
            {
                Contracts.Add(contract);
                contract.Supplier = this;
                contract.SupplierId = Id;
            }
        }

        public void AddPurchase(Purchase purchase)
        {
            if (purchase == null)
                throw new ArgumentNullException(nameof(purchase));

            if (!Purchases.Contains(purchase))
            {
                Purchases.Add(purchase);
                purchase.Supplier = this;
                purchase.SupplierId = Id;
            }
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}