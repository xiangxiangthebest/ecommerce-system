using EcommerceSystem.Data;
using EcommerceSystem.Models;
using EcommerceSystem.Services;
using EcommerceSystem.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=database.db"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICustomerContext, CustomerContext>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IProfileImageStorage, LocalProfileImageStorage>();
builder.Services.AddScoped<IReturnImageStorage, LocalReturnImageStorage>();
builder.Services.AddScoped<IReviewImageStorage, LocalReviewImageStorage>();
builder.Services.AddScoped<INotificationService, NotificationService>();
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    context.Database.Migrate();

    if (!context.Users.Any(x => x.Email == "admin@gmail.com"))
    {
        var admin = new Admin
        {
            FullName = "Administrator",
            Email = "admin@gmail.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "Admin"
        };

        context.Users.Add(admin);
        context.SaveChanges();
    }
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();