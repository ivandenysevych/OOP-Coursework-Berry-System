namespace WarehouseWeb.Models
{
    public interface IInventoryObserver
    {
        void Update(Product product, string action);
    }
}