using System;

namespace EcommerceSystem.Models
{
    public class CartProduct : Product
    {
        // Cart product is a product with some extra attributes so it will inherit product

        public int Quantity { get; set; }
        public DateTime TimeAdded { get; set; }
        public OrderStatus ItemStatus { get; set; }

        // 默认构造函数
        public CartProduct()
        {
            TimeAdded = DateTime.Now;
        }

        public CartProduct(int quantity, DateTime timeAdded)
        {
            Quantity = quantity;
            TimeAdded = timeAdded;
        }

        public string GetFormattedDateAdded()
        {
            return TimeAdded.ToString("dd-MM-yyyy");
        }

        public string GetFormattedTimeAdded()
        {
            return TimeAdded.ToString("hh:mm");
        }

        // 检查库存是否足够
        public bool CanAddToCart()
        {
            return StockQuantity >= Quantity;
        }
    }
}