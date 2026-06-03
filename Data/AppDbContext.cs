using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<CustomerService> CustomerServices { get; set; }
    public DbSet<Seller> Seller { get; set; } 
    public DbSet<Admin> Admin { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Category { get; set; }
    public DbSet<Cart> Cart { get; set; }
    public DbSet<CartItem> CartItem { get; set; }
    public DbSet<DeliveryField> DeliveryField { get; set; }
    public DbSet<Order> Order { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<ChatRoom> ChatRoom { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ProductReport> ProductReports { get; set; }
    public DbSet<ReviewReport> ReviewReports { get; set; }
    public DbSet<CustomerVoucher> CustomerVouchers { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().ToTable("Users").Property(x => x.DateJoin).HasDefaultValueSql("datetime('now')");
        modelBuilder.Entity<Customer>().ToTable("Customers");
        modelBuilder.Entity<CustomerService>().ToTable("CustomerService");
        modelBuilder.Entity<Seller>().ToTable("Seller");
        modelBuilder.Entity<Admin>().ToTable("Admin");
        modelBuilder.Entity<Cart>().ToTable("Cart");
        modelBuilder.Entity<DeliveryField>().ToTable("DeliveryField");
        modelBuilder.Entity<Order>().ToTable("Order");
        modelBuilder.Entity<Order>().Property(o => o.ReturnStatus).HasConversion<string>();
        modelBuilder.Entity<Order>().Property(o => o.ReturnInitiatedBy).HasConversion<string>();
        modelBuilder.Entity<Order>().Property(o => o.CurrentStatus).HasConversion<string>();
        modelBuilder.Entity<Order>().HasOne(o => o.Address).WithMany().HasForeignKey(o => o.AddressId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Product>().ToTable("Product");
        modelBuilder.Entity<Category>().ToTable("Category");
        modelBuilder.Entity<Review>().ToTable("Review");
        modelBuilder.Entity<ProductReport>().ToTable("ProductReport").HasKey(p => p.ReportId);
        modelBuilder.Entity<ProductReport>().HasOne(p => p.Product).WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProductReport>().HasOne(p => p.Customer).WithMany().HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReviewReport>().ToTable("ReviewReport").HasKey(r => r.ReviewReportId);
        modelBuilder.Entity<ReviewReport>().HasOne(r => r.Review).WithMany().HasForeignKey(r => r.ReviewId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReviewReport>().HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}