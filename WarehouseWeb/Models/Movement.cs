using System;

namespace WarehouseWeb.Models
{
    public enum MovementType
    {
        Add,
        Move,
        Remove
    }

    public class Movement
    {
        public int Id { get; set; }
        public MovementType Type { get; set; }
        public decimal Quantity { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int? FromZoneId { get; set; }
        public StorageZone? FromZone { get; set; }

        public int? ToZoneId { get; set; }
        public StorageZone? ToZone { get; set; }

        public bool IsExecuted { get; set; }
        public Purchase? Purchase { get; set; }
        public Sale? Sale { get; set; }

        public Movement() { }

        public Movement(MovementType type, decimal quantity, Product product, StorageZone? fromZone = null, StorageZone? toZone = null)
        {
            if (quantity <= 0)
                throw new ArgumentException("Кількість має бути більшою за нуль.");

            Type = type;
            Quantity = quantity;
            Product = product ?? throw new ArgumentNullException(nameof(product));
            ProductId = product.Id;

            FromZone = fromZone;
            FromZoneId = fromZone?.Id;

            ToZone = toZone;
            ToZoneId = toZone?.Id;

            Date = DateTime.UtcNow;
            IsExecuted = false;
        }

        public void Execute(string performedBy)
        {
            if (IsExecuted)
                return;

            switch (Type)
            {
                case MovementType.Add:
                    Product.IncreaseQuantity(Quantity, performedBy);
                    Product.InventoryManager?.Notify(Product, "Додано товар");
                    break;

                case MovementType.Remove:
                    Product.DecreaseQuantity(Quantity, performedBy);
                    Product.InventoryManager?.Notify(Product, "Видалено товар");
                    break;

                case MovementType.Move:
                    if (ToZone == null)
                        throw new InvalidOperationException("Для переміщення потрібно вказати цільову зону.");

                    if (FromZone != null)
                        FromZone.RemoveProduct(Product);

                    ToZone.AddProduct(Product);
                    Product.AssignZone(ToZone, performedBy);
                    Product.InventoryManager?.Notify(Product, "Переміщено товар");
                    break;
            }

            IsExecuted = true;
            Date = DateTime.UtcNow;
        }

        public void Cancel(string performedBy)
        {
            if (!IsExecuted)
                return;

            switch (Type)
            {
                case MovementType.Add:
                    Product.DecreaseQuantity(Quantity, performedBy);
                    break;

                case MovementType.Remove:
                    Product.IncreaseQuantity(Quantity, performedBy);
                    break;

                case MovementType.Move:
                    if (FromZone == null)
                        throw new InvalidOperationException("Для скасування переміщення потрібна вихідна зона.");

                    ToZone?.RemoveProduct(Product);
                    FromZone.AddProduct(Product);
                    Product.AssignZone(FromZone, performedBy);
                    break;
            }

            Product.InventoryManager?.Notify(Product, "Скасовано рух товару");

            IsExecuted = false;
            Date = DateTime.UtcNow;
        }
    }
}
