using System;
using System.Collections.Generic;
using System.Linq;

namespace WarehouseWeb.Models
{
    public class WarehouseManagementSystem
    {
        private static readonly Lazy<WarehouseManagementSystem> LazyInstance =
            new(() => new WarehouseManagementSystem());

        private readonly List<Company> companies = new();
        private readonly List<User> users = new();

        private WarehouseManagementSystem() { }

        public static WarehouseManagementSystem Instance => LazyInstance.Value;

        public IReadOnlyList<Company> Companies => companies;
        public IReadOnlyList<User> Users => users;

        public Company RegisterCompany(string name, string description)
        {
            var company = new Company(name, description);
            AddCompany(company);
            return company;
        }

        public void AddCompany(Company company)
        {
            if (company == null)
                throw new ArgumentNullException(nameof(company));

            if (!companies.Any(c => c.Id == company.Id))
                companies.Add(company);
        }

        public void AddUser(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (!users.Any(u => u.Id == user.Id))
                users.Add(user);
        }

        public User? Login(string usernameOrEmail, string password)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
                return null;

            var user = users.FirstOrDefault(u =>
                (u.Name.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase) ||
                 u.Email.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase)) &&
                u.Password == password);

            if (user != null)
                user.Login();

            return user;
        }

        public void Logout(User user)
        {
            if (user == null)
                return;

            user.Logout();
        }

        public Company? GetCompanyById(int id)
        {
            return companies.FirstOrDefault(c => c.Id == id);
        }

        public User? GetUserById(int id)
        {
            return users.FirstOrDefault(u => u.Id == id);
        }
    }
}