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
    public DbSet<Cart> Cart { get; set; }
    public DbSet<DeliveryField> DeliveryField { get; set; }
    public DbSet<Order> Order { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().ToTable("Users").Property(x => x.DateJoin)
        .HasDefaultValueSql("datetime('now')");
        modelBuilder.Entity<Customer>().ToTable("Customers");
        modelBuilder.Entity<Cart>().ToTable("Cart");
        modelBuilder.Entity<DeliveryField>().ToTable("DeliveryField");
        modelBuilder.Entity<Order>().ToTable("Order");
    }
}