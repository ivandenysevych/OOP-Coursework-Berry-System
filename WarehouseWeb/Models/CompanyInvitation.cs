using System;
using System.ComponentModel.DataAnnotations;

namespace WarehouseWeb.Models
{
    public class CompanyInvitation
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public int RoleId { get; set; }
        public Role? Role { get; set; }

        [MaxLength(320)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Token { get; set; } = string.Empty;

        [MaxLength(120)]
        public string CreatedByName { get; set; } = string.Empty;

        public int? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
        public bool IsUsed { get; set; }

        public int? UsedByUserId { get; set; }
        public User? UsedByUser { get; set; }

        public DateTime? UsedAt { get; set; }

        [MaxLength(120)]
        public string UsedByName { get; set; } = string.Empty;
    }
}
