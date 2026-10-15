namespace Order_Management.Domain
{
    public class OrderItem
    {
        public Guid Id { get; private set; }
        public Guid OrderId { get; private set; }
        public Guid ProductId { get; private set; }
        public Product Product { get; private set; } = null!;
        public int Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }
        public DateTime CreatedAt { get; private set; }
        
        private OrderItem() { }
        
        internal OrderItem(Guid orderId, Guid productId, int quantity, decimal unitPrice)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity harus > 0.");
            
            if (unitPrice <= 0)
                throw new ArgumentOutOfRangeException(nameof(unitPrice), "UnitPrice harus > 0.");

            Id = Guid.NewGuid();
            OrderId = orderId;
            ProductId = productId;
            Quantity = quantity;
            UnitPrice = unitPrice;
            CreatedAt = DateTime.UtcNow;
        }
    }
}