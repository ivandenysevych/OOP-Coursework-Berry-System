using System;

namespace WarehouseWeb.Models
{
    public class CompanyUser
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public int UserId { get; set; }
        public User? User { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public CompanyUser() { }

        public CompanyUser(User user, Company company, Role role)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
            Company = company ?? throw new ArgumentNullException(nameof(company));
            Role = role ?? throw new ArgumentNullException(nameof(role));

            UserId = user.Id;
            CompanyId = company.Id;
            RoleId = role.Id;
            CreatedDate = DateTime.UtcNow;

            Link();
        }

        public void Link()
        {
            if (User != null && !User.Companies.Contains(this))
                User.Companies.Add(this);

            if (Company != null && !Company.Employees.Contains(this))
                Company.Employees.Add(this);

            if (Role != null && !Role.CompanyUsers.Contains(this))
                Role.CompanyUsers.Add(this);
        }
    }
}