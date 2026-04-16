using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WarehouseWeb.Models;

namespace WarehouseWeb.Data
{
    public static class SeedData
    {
        public static void EnsureSeeded(WarehouseDbContext db)
        {
            db.Database.EnsureCreated();
            EnsureProcurementSchema(db);

            var owner = EnsureRole(
                db,
                RoleNames.Owner,
                "Owner has full access to company, warehouses, roles, and delete operations.");

            var manager = EnsureRole(
                db,
                RoleNames.Manager,
                "Manager controls warehouse structure and day-to-day operations without destructive deletion.");

            var worker = EnsureRole(
                db,
                RoleNames.Worker,
                "Worker performs inventory operations within allowed actions.");

            var collector = EnsureRole(
                db,
                RoleNames.Collector,
                "Collector records procurements from individuals and tracks own procurement journal.");

            EnsurePermissions(owner, new[]
            {
                "all",
                "company.manage_members",
                "company.manage_structure",
                "company.delete",
                "warehouse.delete",
                "inventory.manage",
                "movements.execute",
                "analytics.view",
                "supplier.manage",
                "purchase.manage",
                "finance.manage"
            });

            EnsurePermissions(manager, new[]
            {
                "company.manage_structure",
                "inventory.manage",
                "movements.execute",
                "analytics.view",
                "supplier.manage",
                "purchase.manage",
                "finance.manage"
            });

            EnsurePermissions(worker, new[]
            {
                "inventory.view",
                "movements.execute",
                "purchase.view"
            });

            EnsurePermissions(collector, new[]
            {
                "procurement.create",
                "procurement.view_own",
                "catalog.view"
            });

            db.SaveChanges();
        }

        private static Role EnsureRole(WarehouseDbContext db, string name, string description)
        {
            var role = db.Roles.FirstOrDefault(r => r.Name == name);
            if (role == null)
            {
                role = new Role(name, description);
                db.Roles.Add(role);
            }
            else
            {
                role.Description = description;
            }

            return role;
        }

        private static void EnsurePermissions(Role role, IEnumerable<string> permissions)
        {
            foreach (var permission in permissions)
            {
                role.AddPermission(permission);
            }
        }

        private static void EnsureProcurementSchema(WarehouseDbContext db)
        {
            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS "Suppliers" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Suppliers" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "ContactPerson" TEXT NOT NULL,
                    "Email" TEXT NOT NULL,
                    "Phone" TEXT NOT NULL,
                    "CooperationTerms" TEXT NOT NULL,
                    "PricingNotes" TEXT NOT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    "IsActive" INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS "SupplierContracts" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SupplierContracts" PRIMARY KEY AUTOINCREMENT,
                    "SupplierId" INTEGER NOT NULL,
                    "ContractNumber" TEXT NOT NULL,
                    "StartDate" TEXT NOT NULL,
                    "EndDate" TEXT NULL,
                    "PaymentTerms" TEXT NOT NULL,
                    "DeliveryTerms" TEXT NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    CONSTRAINT "FK_SupplierContracts_Suppliers_SupplierId"
                        FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS "Purchases" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Purchases" PRIMARY KEY AUTOINCREMENT,
                    "SupplierId" INTEGER NOT NULL,
                    "SupplierContractId" INTEGER NULL,
                    "ProductId" INTEGER NOT NULL,
                    "StorageZoneId" INTEGER NULL,
                    "MovementId" INTEGER NULL,
                    "Quantity" TEXT NOT NULL,
                    "UnitPrice" TEXT NOT NULL,
                    "TotalCost" TEXT NOT NULL,
                    "ArrivalDate" TEXT NOT NULL,
                    "PaymentDueDate" TEXT NULL,
                    "IsDirectReceipt" INTEGER NOT NULL DEFAULT 0,
                    "DirectSourceName" TEXT NOT NULL DEFAULT '',
                    "DirectPaymentMethod" INTEGER NULL,
                    "PaymentTerms" TEXT NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "QualityStatus" INTEGER NOT NULL,
                    "QualityNotes" TEXT NOT NULL,
                    "InvoiceNumber" TEXT NOT NULL,
                    "AcceptanceActNumber" TEXT NOT NULL,
                    "CreatedBy" TEXT NOT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    "InventoryPosted" INTEGER NOT NULL,
                    "InventoryPostedAt" TEXT NULL,
                    CONSTRAINT "FK_Purchases_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_Purchases_Suppliers_SupplierId"
                        FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_Purchases_SupplierContracts_SupplierContractId"
                        FOREIGN KEY ("SupplierContractId") REFERENCES "SupplierContracts" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_Purchases_StorageZones_StorageZoneId"
                        FOREIGN KEY ("StorageZoneId") REFERENCES "StorageZones" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_Purchases_Movements_MovementId"
                        FOREIGN KEY ("MovementId") REFERENCES "Movements" ("Id") ON DELETE SET NULL
                );

                CREATE TABLE IF NOT EXISTS "SalesRecords" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SalesRecords" PRIMARY KEY AUTOINCREMENT,
                    "ProductId" INTEGER NOT NULL,
                    "MovementId" INTEGER NULL,
                    "Quantity" TEXT NOT NULL,
                    "UnitPrice" TEXT NOT NULL,
                    "TotalAmount" TEXT NOT NULL,
                    "SaleDate" TEXT NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "CustomerName" TEXT NOT NULL,
                    "PaymentTerms" TEXT NOT NULL,
                    "InvoiceNumber" TEXT NOT NULL,
                    "Notes" TEXT NOT NULL,
                    "CreatedBy" TEXT NOT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    CONSTRAINT "FK_SalesRecords_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_SalesRecords_Movements_MovementId"
                        FOREIGN KEY ("MovementId") REFERENCES "Movements" ("Id") ON DELETE SET NULL
                );

                CREATE TABLE IF NOT EXISTS "Procurements" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Procurements" PRIMARY KEY AUTOINCREMENT,
                    "ProductId" INTEGER NOT NULL,
                    "CollectorUserId" INTEGER NULL,
                    "CompanyId" INTEGER NULL,
                    "CollectorName" TEXT NOT NULL,
                    "SupplierPersonName" TEXT NOT NULL,
                    "ProductCategory" TEXT NOT NULL DEFAULT '',
                    "Quantity" TEXT NOT NULL,
                    "Unit" TEXT NOT NULL DEFAULT 'kg',
                    "UnitPrice" TEXT NOT NULL,
                    "TotalCost" TEXT NOT NULL,
                    "PaymentMethod" INTEGER NOT NULL,
                    "CollectedAt" TEXT NOT NULL,
                    "Notes" TEXT NOT NULL,
                    "IsTransferredToWarehouse" INTEGER NOT NULL DEFAULT 0,
                    "TransferredAt" TEXT NULL,
                    "TransferredBy" TEXT NOT NULL DEFAULT '',
                    "TransferZoneId" INTEGER NULL,
                    "ExpenseAccountId" INTEGER NULL,
                    "CreatedDate" TEXT NOT NULL,
                    CONSTRAINT "FK_Procurements_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_Procurements_Users_CollectorUserId"
                        FOREIGN KEY ("CollectorUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_Procurements_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_Procurements_StorageZones_TransferZoneId"
                        FOREIGN KEY ("TransferZoneId") REFERENCES "StorageZones" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_Procurements_FinanceAccounts_ExpenseAccountId"
                        FOREIGN KEY ("ExpenseAccountId") REFERENCES "FinanceAccounts" ("Id") ON DELETE SET NULL
                );

                CREATE TABLE IF NOT EXISTS "CompanyInvitations" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_CompanyInvitations" PRIMARY KEY AUTOINCREMENT,
                    "CompanyId" INTEGER NOT NULL,
                    "RoleId" INTEGER NOT NULL,
                    "Email" TEXT NOT NULL DEFAULT '',
                    "Token" TEXT NOT NULL,
                    "CreatedByName" TEXT NOT NULL DEFAULT '',
                    "CreatedByUserId" INTEGER NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "ExpiresAt" TEXT NOT NULL,
                    "IsUsed" INTEGER NOT NULL DEFAULT 0,
                    "UsedByUserId" INTEGER NULL,
                    "UsedAt" TEXT NULL,
                    "UsedByName" TEXT NOT NULL DEFAULT '',
                    CONSTRAINT "FK_CompanyInvitations_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_CompanyInvitations_Roles_RoleId"
                        FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_CompanyInvitations_Users_CreatedByUserId"
                        FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_CompanyInvitations_Users_UsedByUserId"
                        FOREIGN KEY ("UsedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
                );

                CREATE TABLE IF NOT EXISTS "FinanceAccounts" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_FinanceAccounts" PRIMARY KEY AUTOINCREMENT,
                    "AccountType" INTEGER NOT NULL,
                    "CompanyId" INTEGER NULL,
                    "WarehouseId" INTEGER NULL,
                    "UserId" INTEGER NULL,
                    "Name" TEXT NOT NULL DEFAULT '',
                    "Currency" TEXT NOT NULL DEFAULT 'UAH',
                    "Balance" TEXT NOT NULL DEFAULT 0,
                    "CreatedDate" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_FinanceAccounts_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_FinanceAccounts_Warehouses_WarehouseId"
                        FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_FinanceAccounts_Users_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
                );

                CREATE TABLE IF NOT EXISTS "FinanceTransactions" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_FinanceTransactions" PRIMARY KEY AUTOINCREMENT,
                    "FromAccountId" INTEGER NULL,
                    "ToAccountId" INTEGER NULL,
                    "Type" INTEGER NOT NULL,
                    "Amount" TEXT NOT NULL,
                    "Notes" TEXT NOT NULL DEFAULT '',
                    "CreatedBy" TEXT NOT NULL DEFAULT 'system',
                    "CreatedDate" TEXT NOT NULL,
                    CONSTRAINT "FK_FinanceTransactions_FinanceAccounts_FromAccountId"
                        FOREIGN KEY ("FromAccountId") REFERENCES "FinanceAccounts" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_FinanceTransactions_FinanceAccounts_ToAccountId"
                        FOREIGN KEY ("ToAccountId") REFERENCES "FinanceAccounts" ("Id") ON DELETE SET NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SupplierContracts_SupplierId_ContractNumber"
                    ON "SupplierContracts" ("SupplierId", "ContractNumber");

                CREATE INDEX IF NOT EXISTS "IX_Purchases_ArrivalDate"
                    ON "Purchases" ("ArrivalDate");

                CREATE INDEX IF NOT EXISTS "IX_Purchases_ProductId"
                    ON "Purchases" ("ProductId");

                CREATE INDEX IF NOT EXISTS "IX_Purchases_SupplierId"
                    ON "Purchases" ("SupplierId");

                CREATE INDEX IF NOT EXISTS "IX_Purchases_SupplierContractId"
                    ON "Purchases" ("SupplierContractId");

                CREATE INDEX IF NOT EXISTS "IX_Purchases_StorageZoneId"
                    ON "Purchases" ("StorageZoneId");

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Purchases_MovementId"
                    ON "Purchases" ("MovementId");

                CREATE INDEX IF NOT EXISTS "IX_Purchases_IsDirectReceipt"
                    ON "Purchases" ("IsDirectReceipt");

                CREATE INDEX IF NOT EXISTS "IX_SalesRecords_SaleDate"
                    ON "SalesRecords" ("SaleDate");

                CREATE INDEX IF NOT EXISTS "IX_SalesRecords_ProductId"
                    ON "SalesRecords" ("ProductId");

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SalesRecords_MovementId"
                    ON "SalesRecords" ("MovementId");

                CREATE INDEX IF NOT EXISTS "IX_Procurements_CollectedAt"
                    ON "Procurements" ("CollectedAt");

                CREATE INDEX IF NOT EXISTS "IX_Procurements_ProductId"
                    ON "Procurements" ("ProductId");

                CREATE INDEX IF NOT EXISTS "IX_Procurements_CollectorUserId"
                    ON "Procurements" ("CollectorUserId");

                CREATE INDEX IF NOT EXISTS "IX_Procurements_CompanyId"
                    ON "Procurements" ("CompanyId");

                CREATE INDEX IF NOT EXISTS "IX_Procurements_IsTransferredToWarehouse"
                    ON "Procurements" ("IsTransferredToWarehouse");

                CREATE INDEX IF NOT EXISTS "IX_Procurements_TransferZoneId"
                    ON "Procurements" ("TransferZoneId");

                CREATE INDEX IF NOT EXISTS "IX_Procurements_ExpenseAccountId"
                    ON "Procurements" ("ExpenseAccountId");

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_CompanyInvitations_Token"
                    ON "CompanyInvitations" ("Token");

                CREATE INDEX IF NOT EXISTS "IX_CompanyInvitations_CompanyId_IsUsed_ExpiresAt"
                    ON "CompanyInvitations" ("CompanyId", "IsUsed", "ExpiresAt");

                CREATE INDEX IF NOT EXISTS "IX_FinanceAccounts_AccountType_CompanyId_WarehouseId_UserId"
                    ON "FinanceAccounts" ("AccountType", "CompanyId", "WarehouseId", "UserId");

                CREATE INDEX IF NOT EXISTS "IX_FinanceAccounts_CompanyId"
                    ON "FinanceAccounts" ("CompanyId");

                CREATE INDEX IF NOT EXISTS "IX_FinanceAccounts_WarehouseId"
                    ON "FinanceAccounts" ("WarehouseId");

                CREATE INDEX IF NOT EXISTS "IX_FinanceAccounts_UserId"
                    ON "FinanceAccounts" ("UserId");

                CREATE INDEX IF NOT EXISTS "IX_FinanceTransactions_CreatedDate"
                    ON "FinanceTransactions" ("CreatedDate");

                CREATE INDEX IF NOT EXISTS "IX_FinanceTransactions_FromAccountId"
                    ON "FinanceTransactions" ("FromAccountId");

                CREATE INDEX IF NOT EXISTS "IX_FinanceTransactions_ToAccountId"
                    ON "FinanceTransactions" ("ToAccountId");
                """);

            EnsurePurchasesColumns(db);
            EnsureProcurementsColumns(db);
            EnsureCompanyInvitationColumns(db);
        }

        private static void EnsurePurchasesColumns(WarehouseDbContext db)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(\"Purchases\");";

                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var columnName = reader["name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(columnName))
                        {
                            existingColumns.Add(columnName);
                        }
                    }
                }

                if (!existingColumns.Contains("IsDirectReceipt"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Purchases\" ADD COLUMN \"IsDirectReceipt\" INTEGER NOT NULL DEFAULT 0;");
                }

                if (!existingColumns.Contains("DirectSourceName"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Purchases\" ADD COLUMN \"DirectSourceName\" TEXT NOT NULL DEFAULT '';");
                }

                if (!existingColumns.Contains("DirectPaymentMethod"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Purchases\" ADD COLUMN \"DirectPaymentMethod\" INTEGER NULL;");
                }

                db.Database.ExecuteSqlRaw(
                    """
                    CREATE INDEX IF NOT EXISTS "IX_Purchases_IsDirectReceipt"
                        ON "Purchases" ("IsDirectReceipt");
                    """);
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        private static void EnsureProcurementsColumns(WarehouseDbContext db)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(\"Procurements\");";

                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var hasUnitColumn = false;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var columnName = reader["name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(columnName))
                        {
                            existingColumns.Add(columnName);
                        }

                        if (string.Equals(columnName, "Unit", StringComparison.OrdinalIgnoreCase))
                        {
                            hasUnitColumn = true;
                        }
                    }
                }

                if (!hasUnitColumn)
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Procurements\" ADD COLUMN \"Unit\" TEXT NOT NULL DEFAULT 'kg';");
                }

                if (!existingColumns.Contains("ProductCategory"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Procurements\" ADD COLUMN \"ProductCategory\" TEXT NOT NULL DEFAULT '';");
                }

                if (!existingColumns.Contains("IsTransferredToWarehouse"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Procurements\" ADD COLUMN \"IsTransferredToWarehouse\" INTEGER NOT NULL DEFAULT 0;");
                }

                if (!existingColumns.Contains("TransferredAt"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Procurements\" ADD COLUMN \"TransferredAt\" TEXT NULL;");
                }

                if (!existingColumns.Contains("TransferredBy"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Procurements\" ADD COLUMN \"TransferredBy\" TEXT NOT NULL DEFAULT '';");
                }

                if (!existingColumns.Contains("TransferZoneId"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Procurements\" ADD COLUMN \"TransferZoneId\" INTEGER NULL;");
                }

                if (!existingColumns.Contains("CompanyId"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Procurements\" ADD COLUMN \"CompanyId\" INTEGER NULL;");
                }

                if (!existingColumns.Contains("ExpenseAccountId"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"Procurements\" ADD COLUMN \"ExpenseAccountId\" INTEGER NULL;");
                }

                db.Database.ExecuteSqlRaw(
                    """
                    CREATE INDEX IF NOT EXISTS "IX_Procurements_IsTransferredToWarehouse"
                        ON "Procurements" ("IsTransferredToWarehouse");

                    CREATE INDEX IF NOT EXISTS "IX_Procurements_TransferZoneId"
                        ON "Procurements" ("TransferZoneId");

                    CREATE INDEX IF NOT EXISTS "IX_Procurements_CompanyId"
                        ON "Procurements" ("CompanyId");

                    CREATE INDEX IF NOT EXISTS "IX_Procurements_ExpenseAccountId"
                        ON "Procurements" ("ExpenseAccountId");
                    """);
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        private static void EnsureCompanyInvitationColumns(WarehouseDbContext db)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(\"CompanyInvitations\");";

                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var columnName = reader["name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(columnName))
                        {
                            existingColumns.Add(columnName);
                        }
                    }
                }

                if (!existingColumns.Contains("Email"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"CompanyInvitations\" ADD COLUMN \"Email\" TEXT NOT NULL DEFAULT '';");
                }

                if (!existingColumns.Contains("Token"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"CompanyInvitations\" ADD COLUMN \"Token\" TEXT NOT NULL DEFAULT '';");
                }

                if (!existingColumns.Contains("CreatedByName"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"CompanyInvitations\" ADD COLUMN \"CreatedByName\" TEXT NOT NULL DEFAULT '';");
                }

                if (!existingColumns.Contains("CreatedByUserId"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"CompanyInvitations\" ADD COLUMN \"CreatedByUserId\" INTEGER NULL;");
                }

                if (!existingColumns.Contains("CreatedAt"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"CompanyInvitations\" ADD COLUMN \"CreatedAt\" TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z';");
                }

                if (!existingColumns.Contains("ExpiresAt"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"CompanyInvitations\" ADD COLUMN \"ExpiresAt\" TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z';");
                }

                if (!existingColumns.Contains("IsUsed"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"CompanyInvitations\" ADD COLUMN \"IsUsed\" INTEGER NOT NULL DEFAULT 0;");
                }

                if (!existingColumns.Contains("UsedByUserId"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"CompanyInvitations\" ADD COLUMN \"UsedByUserId\" INTEGER NULL;");
                }

                if (!existingColumns.Contains("UsedAt"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"CompanyInvitations\" ADD COLUMN \"UsedAt\" TEXT NULL;");
                }

                if (!existingColumns.Contains("UsedByName"))
                {
                    db.Database.ExecuteSqlRaw(
                        "ALTER TABLE \"CompanyInvitations\" ADD COLUMN \"UsedByName\" TEXT NOT NULL DEFAULT '';");
                }

                db.Database.ExecuteSqlRaw(
                    """
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_CompanyInvitations_Token"
                        ON "CompanyInvitations" ("Token");

                    CREATE INDEX IF NOT EXISTS "IX_CompanyInvitations_CompanyId_IsUsed_ExpiresAt"
                        ON "CompanyInvitations" ("CompanyId", "IsUsed", "ExpiresAt");
                    """);
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }
    }
}
