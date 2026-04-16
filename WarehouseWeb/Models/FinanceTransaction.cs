using System;
using System.ComponentModel.DataAnnotations;

namespace WarehouseWeb.Models
{
    public enum FinanceTransactionType
    {
        TopUp = 1,
        Transfer = 2,
        Adjustment = 3
    }

    public class FinanceTransaction
    {
        public int Id { get; set; }

        public int? FromAccountId { get; set; }
        public FinanceAccount? FromAccount { get; set; }

        public int? ToAccountId { get; set; }
        public FinanceAccount? ToAccount { get; set; }

        public FinanceTransactionType Type { get; set; } = FinanceTransactionType.Transfer;

        public decimal Amount { get; set; }

        [MaxLength(600)]
        public string Notes { get; set; } = string.Empty;

        [MaxLength(120)]
        public string CreatedBy { get; set; } = "system";

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
