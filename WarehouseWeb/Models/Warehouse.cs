using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace WarehouseWeb.Models
{
    public class Warehouse
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public string Location { get; set; } = string.Empty;

        [MaxLength(400)]
        public string Description { get; set; } = string.Empty;

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public List<StorageZone> Zones { get; set; } = new();

        public Warehouse() { }

        public Warehouse(string name, string location, string description, Company company)
        {
            Name = name.Trim();
            Location = location.Trim();
            Description = description.Trim();
            Company = company ?? throw new ArgumentNullException(nameof(company));
            CompanyId = company.Id;
        }

        public void AddZone(StorageZone zone)
        {
            if (zone == null)
                throw new ArgumentNullException(nameof(zone));

            if (!Zones.Contains(zone))
                Zones.Add(zone);
        }

        public void RemoveZone(StorageZone zone)
        {
            if (zone == null)
                throw new ArgumentNullException(nameof(zone));

            Zones.Remove(zone);
        }

        public StorageZone? GetZoneById(int id)
        {
            return Zones.FirstOrDefault(z => z.Id == id);
        }

        public void AddProduct(Product product, StorageZone zone)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            if (zone == null || !Zones.Contains(zone))
                throw new InvalidOperationException("Зона не належить цьому складу.");

            zone.AddProduct(product);
        }

        public void MoveProduct(Product product, StorageZone fromZone, StorageZone toZone, string user)
        {
            if (product == null || fromZone == null || toZone == null)
                throw new ArgumentNullException("Некоректні дані для переміщення.");

            if (!Zones.Contains(fromZone) || !Zones.Contains(toZone))
                throw new InvalidOperationException("Одна із зон не належить складу.");

            fromZone.RemoveProduct(product);
            toZone.AddProduct(product);
            product.AssignZone(toZone, user);
        }
    }
}