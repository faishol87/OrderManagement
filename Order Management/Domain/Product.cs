namespace Order_Management.Domain
{
    public class Product
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = null!;
        public int StockQuantity { get; private set; }
        public decimal Price { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public DateTime? DeletedAt { get; private set; }
        public bool IsDeleted { get; private set; } = false;
        
        private Product() { }

        public static Product Create(string name, int stockQuantity, decimal price)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Nama tidak boleh kosong", nameof(name));
            
            if (stockQuantity < 0)
                throw new ArgumentException("Stock tidak model minus", nameof(stockQuantity));
            
            if (price <= 0)
                throw new ArgumentException("Harga harus lebih besar dari 0", nameof(price));

            var product = new Product()
            {
                Id = Guid.NewGuid(),
                Name = name,
                StockQuantity = stockQuantity,
                Price = price,
                CreatedAt = DateTime.UtcNow,
            };
                
            return product;
        }

        public bool Delete()
        {
            if (IsDeleted)
                return false;

            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            
            return true;
        }
        
        public bool Restore()
        {
            if (!IsDeleted)
                return false;

            IsDeleted = false;
            DeletedAt = null;
            UpdatedAt = DateTime.UtcNow;
            
            return true;
        }
    }
}