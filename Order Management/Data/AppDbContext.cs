using Microsoft.EntityFrameworkCore;
using Order_Management.Domain;
using Order_Management.Features.Idempotency;

namespace Order_Management.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---------------- Product ----------------
        b.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);

            // Soft delete: semua query otomatis abaikan yang terhapus
            e.HasQueryFilter(x => !x.IsDeleted);

            // Unik hanya untuk yang belum terhapus
            e.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        // ---------------- Order ----------------
        b.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.HasKey(x => x.Id);
            e.Property(x => x.ShippingAddress).HasMaxLength(500).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            // Optimistic concurrency (Skenario B)
            e.Property(x => x.RowVersion).IsRowVersion();

            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);

            e.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Pakai backing field _items
            e.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // ---------------- OrderItem ----------------
        b.Entity<OrderItem>(e =>
        {
            e.ToTable("OrderItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasIndex(x => x.ProductId);

            e.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- Idempotency ----------------
        b.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("IdempotencyKeys");
            e.HasKey(x => x.Key);
            e.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.CustomerId, x.Key }).IsUnique(); // inti penjaga race
            e.HasIndex(x => x.ExpiresAt);
        });
    }
}