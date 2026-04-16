using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace WarehouseWeb.Models
{
    public class Company
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(400)]
        public string Description { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public List<Warehouse> Warehouses { get; set; } = new();
        public List<CompanyUser> Employees { get; set; } = new();

        public Company() { }

        public Company(string name, string description)
        {
            Name = name.Trim();
            Description = description.Trim();
            CreatedDate = DateTime.UtcNow;
        }

        public void AddWarehouse(Warehouse warehouse)
        {
            if (warehouse == null)
                throw new ArgumentNullException(nameof(warehouse));

            if (!Warehouses.Contains(warehouse))
            {
                Warehouses.Add(warehouse);
                warehouse.Company = this;
                warehouse.CompanyId = Id;
            }
        }

        public void RemoveWarehouse(Warehouse warehouse)
        {
            if (warehouse == null)
                throw new ArgumentNullException(nameof(warehouse));

            Warehouses.Remove(warehouse);
        }

        public void AddEmployee(CompanyUser companyUser)
        {
            if (companyUser == null)
                throw new ArgumentNullException(nameof(companyUser));

            if (!Employees.Any(e => e.UserId == companyUser.UserId))
            {
                Employees.Add(companyUser);
                companyUser.Company = this;
                companyUser.CompanyId = Id;
            }
        }

        public void RemoveEmployee(CompanyUser companyUser)
        {
            if (companyUser == null)
                throw new ArgumentNullException(nameof(companyUser));

            Employees.Remove(companyUser);
        }
    }
}