using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WarehouseWeb.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Password { get; set; } = string.Empty;

        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public DateTime? LastLoginAt { get; set; }
        public bool IsLoggedIn { get; set; }

        public List<CompanyUser> Companies { get; set; } = new();
        public List<Procurement> Procurements { get; set; } = new();

        public User() { }

        public User(string name, string password, string email, Role role)
        {
            Name = name.Trim();
            Password = password;
            Email = email.Trim();
            AssignRole(role);
        }

        public void AssignRole(Role role)
        {
            Role = role ?? throw new ArgumentNullException(nameof(role));
            RoleId = role.Id;
        }

        public void Login()
        {
            IsLoggedIn = true;
            LastLoginAt = DateTime.UtcNow;
        }

        public void Logout()
        {
            IsLoggedIn = false;
        }

        public void JoinCompany(Company company)
        {
            if (company == null)
                throw new ArgumentNullException(nameof(company));

            var link = new CompanyUser
            {
                User = this,
                UserId = Id,
                Company = company,
                CompanyId = company.Id
            };

            if (!Companies.Exists(c => c.CompanyId == company.Id))
            {
                Companies.Add(link);
                company.Employees.Add(link);
            }
        }
    }
}
