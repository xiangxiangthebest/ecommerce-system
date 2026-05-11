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
    
    // CRITICAL: Added these for your Seller logic to work
    public DbSet<Seller> Seller { get; set; } 
    public DbSet<Admin> Admin { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Category { get; set; }

    public DbSet<Cart> Cart { get; set; }
    public DbSet<CartItem> CartItem { get; set; }
    public DbSet<Review> Review { get; set; }
    public DbSet<Complaint> Complaint { get; set; }
    public DbSet<Notification> Notification { get; set; }
    public DbSet<ChatMessage> ChatMessage { get; set; }
    public DbSet<DeliveryField> DeliveryField { get; set; }
    public DbSet<Order> Order { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().ToTable("Users").Property(x => x.DateJoin)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<Customer>().ToTable("Customers");
        modelBuilder.Entity<CustomerService>().ToTable("CustomerService");
        
        // Ensures Seller is its own table linked to Users
        modelBuilder.Entity<Seller>().ToTable("Seller");
        modelBuilder.Entity<Admin>().ToTable("Admin");
        
        modelBuilder.Entity<Cart>().ToTable("Cart");
        modelBuilder.Entity<DeliveryField>().ToTable("DeliveryField");
        modelBuilder.Entity<Order>().ToTable("Order");
        modelBuilder.Entity<Product>().ToTable("Product");
        modelBuilder.Entity<Category>().ToTable("Category");
    }
}