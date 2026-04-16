using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Models;

namespace WarehouseWeb.Data
{
    public class WarehouseDbContext : DbContext
    {
        public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Company> Companies => Set<Company>();
        public DbSet<CompanyUser> CompanyUsers => Set<CompanyUser>();
        public DbSet<CompanyInvitation> CompanyInvitations => Set<CompanyInvitation>();

        public DbSet<Warehouse> Warehouses => Set<Warehouse>();
        public DbSet<StorageZone> StorageZones => Set<StorageZone>();

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Movement> Movements => Set<Movement>();

        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<SupplierContract> SupplierContracts => Set<SupplierContract>();
        public DbSet<Purchase> Purchases => Set<Purchase>();
        public DbSet<Sale> Sales => Set<Sale>();
        public DbSet<Procurement> Procurements => Set<Procurement>();
        public DbSet<FinanceAccount> FinanceAccounts => Set<FinanceAccount>();
        public DbSet<FinanceTransaction> FinanceTransactions => Set<FinanceTransaction>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ❗ КРИТИЧНО — щоб EF не ламався
            modelBuilder.Ignore<InventoryManager>();

            // Індекси
            modelBuilder.Entity<Role>()
                .HasIndex(r => r.Name)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<SupplierContract>()
                .HasIndex(c => new { c.SupplierId, c.ContractNumber })
                .IsUnique();

            modelBuilder.Entity<Purchase>()
                .HasIndex(p => p.ArrivalDate);

            // User - Role
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // CompanyUser
            modelBuilder.Entity<CompanyUser>()
                .HasOne(cu => cu.User)
                .WithMany(u => u.Companies)
                .HasForeignKey(cu => cu.UserId);

            modelBuilder.Entity<CompanyUser>()
                .HasOne(cu => cu.Company)
                .WithMany(c => c.Employees)
                .HasForeignKey(cu => cu.CompanyId);

            modelBuilder.Entity<CompanyUser>()
                .HasOne(cu => cu.Role)
                .WithMany(r => r.CompanyUsers)
                .HasForeignKey(cu => cu.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Warehouse
            modelBuilder.Entity<Warehouse>()
                .HasOne(w => w.Company)
                .WithMany(c => c.Warehouses)
                .HasForeignKey(w => w.CompanyId);

            // StorageZone
            modelBuilder.Entity<StorageZone>()
                .HasOne(z => z.Warehouse)
                .WithMany(w => w.Zones)
                .HasForeignKey(z => z.WarehouseId);

            // Product
            modelBuilder.Entity<Product>()
                .Property(p => p.Quantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Zone)
                .WithMany(z => z.Products)
                .HasForeignKey(p => p.StorageZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            // Movement
            modelBuilder.Entity<Movement>()
                .Property(m => m.Quantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Movement>()
                .HasOne(m => m.Product)
                .WithMany(p => p.Movements)
                .HasForeignKey(m => m.ProductId);

            modelBuilder.Entity<Movement>()
                .HasOne(m => m.FromZone)
                .WithMany()
                .HasForeignKey(m => m.FromZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Movement>()
                .HasOne(m => m.ToZone)
                .WithMany()
                .HasForeignKey(m => m.ToZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            // Purchase
            modelBuilder.Entity<Purchase>()
                .Property(p => p.Quantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Purchase>()
                .Property(p => p.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Purchase>()
                .Property(p => p.TotalCost)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Purchase>()
                .Property(p => p.DirectSourceName)
                .HasMaxLength(180);

            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Purchases)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.Contract)
                .WithMany(c => c.Purchases)
                .HasForeignKey(p => p.SupplierContractId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.Product)
                .WithMany(p => p.Purchases)
                .HasForeignKey(p => p.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.StorageZone)
                .WithMany(z => z.Purchases)
                .HasForeignKey(p => p.StorageZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.Movement)
                .WithOne(m => m.Purchase)
                .HasForeignKey<Purchase>(p => p.MovementId)
                .OnDelete(DeleteBehavior.SetNull);

            // Company invitation
            modelBuilder.Entity<CompanyInvitation>()
                .HasIndex(i => i.Token)
                .IsUnique();

            modelBuilder.Entity<CompanyInvitation>()
                .HasIndex(i => new { i.CompanyId, i.IsUsed, i.ExpiresAt });

            modelBuilder.Entity<CompanyInvitation>()
                .HasOne(i => i.Company)
                .WithMany()
                .HasForeignKey(i => i.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CompanyInvitation>()
                .HasOne(i => i.Role)
                .WithMany()
                .HasForeignKey(i => i.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CompanyInvitation>()
                .HasOne(i => i.CreatedByUser)
                .WithMany()
                .HasForeignKey(i => i.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CompanyInvitation>()
                .HasOne(i => i.UsedByUser)
                .WithMany()
                .HasForeignKey(i => i.UsedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Sale
            modelBuilder.Entity<Sale>()
                .ToTable("SalesRecords");

            modelBuilder.Entity<Sale>()
                .Property(s => s.Quantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Sale>()
                .Property(s => s.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Sale>()
                .Property(s => s.TotalAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Sale>()
                .HasIndex(s => s.SaleDate);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Product)
                .WithMany(p => p.Sales)
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Movement)
                .WithOne(m => m.Sale)
                .HasForeignKey<Sale>(s => s.MovementId)
                .OnDelete(DeleteBehavior.SetNull);

            // Procurement
            modelBuilder.Entity<Procurement>()
                .Property(p => p.Quantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Procurement>()
                .Property(p => p.Unit)
                .HasMaxLength(20);

            modelBuilder.Entity<Procurement>()
                .Property(p => p.ProductCategory)
                .HasMaxLength(120);

            modelBuilder.Entity<Procurement>()
                .Property(p => p.TransferredBy)
                .HasMaxLength(120);

            modelBuilder.Entity<Procurement>()
                .Property(p => p.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Procurement>()
                .Property(p => p.TotalCost)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Procurement>()
                .HasIndex(p => p.CollectedAt);

            modelBuilder.Entity<Procurement>()
                .HasIndex(p => p.IsTransferredToWarehouse);

            modelBuilder.Entity<Procurement>()
                .HasIndex(p => p.CompanyId);

            modelBuilder.Entity<Procurement>()
                .HasIndex(p => p.ExpenseAccountId);

            modelBuilder.Entity<Procurement>()
                .HasOne(p => p.Product)
                .WithMany(p => p.Procurements)
                .HasForeignKey(p => p.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Procurement>()
                .HasOne(p => p.CollectorUser)
                .WithMany(u => u.Procurements)
                .HasForeignKey(p => p.CollectorUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Procurement>()
                .HasOne(p => p.Company)
                .WithMany()
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Procurement>()
                .HasOne(p => p.TransferZone)
                .WithMany()
                .HasForeignKey(p => p.TransferZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Procurement>()
                .HasOne(p => p.ExpenseAccount)
                .WithMany()
                .HasForeignKey(p => p.ExpenseAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            // Finance account
            modelBuilder.Entity<FinanceAccount>()
                .Property(a => a.Balance)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinanceAccount>()
                .HasIndex(a => new { a.AccountType, a.CompanyId, a.WarehouseId, a.UserId });

            modelBuilder.Entity<FinanceAccount>()
                .HasOne(a => a.Company)
                .WithMany()
                .HasForeignKey(a => a.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<FinanceAccount>()
                .HasOne(a => a.Warehouse)
                .WithMany()
                .HasForeignKey(a => a.WarehouseId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<FinanceAccount>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Finance transaction
            modelBuilder.Entity<FinanceTransaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinanceTransaction>()
                .HasIndex(t => t.CreatedDate);

            modelBuilder.Entity<FinanceTransaction>()
                .HasOne(t => t.FromAccount)
                .WithMany()
                .HasForeignKey(t => t.FromAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<FinanceTransaction>()
                .HasOne(t => t.ToAccount)
                .WithMany()
                .HasForeignKey(t => t.ToAccountId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
