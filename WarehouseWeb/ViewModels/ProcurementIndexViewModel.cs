using System;
using System.Collections.Generic;
using WarehouseWeb.Models;

namespace WarehouseWeb.ViewModels
{
    public class ProcurementBasketSummaryViewModel
    {
        public int CollectorUserId { get; set; }
        public int? CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string CollectorName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string QuantitySummary { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public int RecordsCount { get; set; }
        public DateTime FirstCollectedAt { get; set; }
        public DateTime LastCollectedAt { get; set; }
    }

    public class ProcurementIndexViewModel
    {
        public List<Procurement> Procurements { get; set; } = new();
        public List<ProcurementBasketSummaryViewModel> ActiveBaskets { get; set; } = new();
        public List<StorageZone> TransferZones { get; set; } = new();
        public bool IsCollector { get; set; }
        public bool CanTransferToWarehouse { get; set; }
    }
}
