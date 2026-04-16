using System.Collections.Generic;

namespace WarehouseWeb.Models
{
    public class InventoryManager
    {
        private readonly List<IInventoryObserver> observers = new();

        public void Attach(IInventoryObserver observer)
        {
            if (observer == null || observers.Contains(observer))
                return;

            observers.Add(observer);
        }

        public void Detach(IInventoryObserver observer)
        {
            if (observer == null)
                return;

            observers.Remove(observer);
        }

        public void Notify(Product product, string action)
        {
            if (product == null)
                return;

            foreach (var observer in observers)
            {
                observer.Update(product, action);
            }
        }

        public void ExecuteMovement(Movement movement, string performedBy)
        {
            if (movement == null)
                return;

            movement.Execute(performedBy);

            if (movement.Product != null)
            {
                movement.Product.InventoryManager = this;
                Notify(movement.Product, "Рух товару виконано");
            }
        }
    }
}