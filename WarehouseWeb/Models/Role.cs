using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace WarehouseWeb.Models
{
    public static class RoleNames
    {
        public const string Owner = "Owner";
        public const string Manager = "Manager";
        public const string Worker = "Worker";
        public const string Collector = "Collector";
    }

    public class Role
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(40)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string PermissionsRaw { get; set; } = string.Empty;

        public List<User> Users { get; set; } = new();
        public List<CompanyUser> CompanyUsers { get; set; } = new();

        public Role() { }

        public Role(string name, string description)
        {
            Name = name.Trim();
            Description = description.Trim();
        }

        public void AddPermission(string permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                return;

            var values = GetPermissions().ToList();

            if (!values.Any(p => p.Equals(permission, StringComparison.OrdinalIgnoreCase)))
                values.Add(permission.Trim());

            PermissionsRaw = string.Join(';', values);
        }

        public IReadOnlyList<string> GetPermissions()
        {
            if (string.IsNullOrWhiteSpace(PermissionsRaw))
                return new List<string>();

            return PermissionsRaw
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        public bool HasPermission(string permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                return false;

            return GetPermissions()
                .Any(p => p.Equals(permission, StringComparison.OrdinalIgnoreCase));
        }

        public void AssignUser(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (!Users.Contains(user))
            {
                Users.Add(user);
                user.Role = this;
                user.RoleId = Id;
            }
        }
    }
}
