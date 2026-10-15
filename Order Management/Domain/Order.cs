namespace Order_Management.Domain
{
    public class Order
    {
        public Guid Id { get; private set; }
        public Guid CustomerId { get; private set; }
        public OrderStatus Status { get; private set; } = OrderStatus.Pending;
        public string ShippingAddress { get; private set; } = null!;
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        public byte[] RowVersion { get; private set; } = null!;

        private readonly List<OrderItem> _items = new();
        public IReadOnlyCollection<OrderItem> Items => _items;

        private Order(){ }

        public static Order Create(
            Guid customerId,
            string shippingAddress,
            IEnumerable<(Guid ProductId, int Quantity, decimal UnitPrice)> items)
        {
            if (customerId == Guid.Empty)
                throw new ArgumentException("CustomerId tidak boleh kosong", nameof(customerId));
            
            if (string.IsNullOrWhiteSpace(shippingAddress))
                throw new ArgumentException("Shipping Address wajib diisi.", nameof(shippingAddress));
            
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                ShippingAddress =  shippingAddress.Trim(),
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };
            
            foreach (var item in items)
                order._items.Add(new OrderItem(order.Id, item.ProductId, item.Quantity, item.UnitPrice));
            
            if (order._items.Count == 0)
                throw new InvalidOperationException("Order harus memiliki minimal 1 item.");
            
            return order;
        }
        
        public bool TryChangeStatus(OrderStatus status)
        {
            if(!IsValidTransition(Status, status))
                return false;
            
            Status = status;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }

        private static bool IsValidTransition(OrderStatus from, OrderStatus to) => (from, to) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Confirmed, OrderStatus.Shipped) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            _ => false
        };
        
        public bool IsCancellable => Status is OrderStatus.Pending or OrderStatus.Confirmed;
    }
}