using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using EcommerceSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services
{
    public class CartService : ICartService
    {
        private readonly AppDbContext _context;

        public CartService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Cart> GetCartAsync(int customerId)
        {
            var cart = await _context.Cart
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p!.Seller)
                .FirstOrDefaultAsync(c => c.UserId == customerId);

            return cart ?? new Cart { CartItems = new List<CartItem>() };
        }

        public async Task<OperationResult> AddToCartAsync(int customerId, int productId, int quantity, string selectedVariations)
        {
            if (quantity <= 0) quantity = 1;

            var product = await _context.Products.FindAsync(productId);
            if (product == null) return OperationResult.Fail("Product not found.");
            if (product.IsDeleted) return OperationResult.Fail("This product has been deleted.");
            if (product.IsDraft) return OperationResult.Fail("This product is not yet available.");

            var cart = await _context.Cart
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == customerId);

            if (cart == null)
            {
                cart = new Cart { UserId = customerId };
                _context.Cart.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existing = cart.CartItems
                .FirstOrDefault(x => x.ProductId == productId && x.SelectedVariations == (selectedVariations ?? "{}"));

            if (existing != null)
            {
                existing.Quantity += quantity;
            }
            else
            {
                cart.CartItems.Add(new CartItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    Price = product.Price,
                    SelectedVariations = selectedVariations ?? "{}"
                });
            }

            await _context.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> UpdateQuantityAsync(int customerId, int cartItemId, int quantity)
        {
            if (quantity <= 0) return OperationResult.Fail("Quantity must be at least 1.");

            var item = await _context.CartItem
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.Cart!.UserId == customerId);

            if (item == null) return OperationResult.Fail("Cart item not found.");

            item.Quantity = quantity;
            await _context.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> RemoveItemAsync(int customerId, int cartItemId)
        {
            var item = await _context.CartItem
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.Cart!.UserId == customerId);

            if (item == null) return OperationResult.Fail("Cart item not found.");

            _context.CartItem.Remove(item);
            await _context.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult<Checkout>> BuildCartCheckoutAsync(int customerId, List<int> cartItemIds)
        {
            if (cartItemIds == null || cartItemIds.Count == 0)
                return OperationResult<Checkout>.Fail("No items selected.");

            var customer = await _context.Users.OfType<Customer>()
                .FirstOrDefaultAsync(x => x.UserId == customerId);

            if (customer == null) return OperationResult<Checkout>.Fail("Customer not found.");

            var addresses = await _context.DeliveryField
                .Where(a => a.UserId == customerId)
                .ToListAsync();

            var items = await _context.CartItem
                .Include(ci => ci.Product)!.ThenInclude(p => p!.Seller)
                .Include(ci => ci.Cart)
                .Where(ci => cartItemIds.Contains(ci.CartItemId) && ci.Cart!.UserId == customerId)
                .ToListAsync();

            if (!items.Any()) return OperationResult<Checkout>.Fail("Your cart is empty.");

            var checkout = new Checkout
            {
                Customer = customer,
                CartItems = items,
                Addresses = addresses
            };

            return OperationResult<Checkout>.Ok(checkout);
        }

        public async Task<OperationResult<Checkout>> BuildBuyNowCheckoutAsync(int customerId, int productId, int quantity, string selectedVariations)
        {
            if (quantity <= 0) quantity = 1;

            var customer = await _context.Users.OfType<Customer>()
                .FirstOrDefaultAsync(x => x.UserId == customerId);

            if (customer == null) return OperationResult<Checkout>.Fail("Customer not found.");

            var product = await _context.Products
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null) return OperationResult<Checkout>.Fail("Product not found.");
            if (product.IsDeleted) return OperationResult<Checkout>.Fail("This product has been deleted.");
            if (product.IsDraft) return OperationResult<Checkout>.Fail("This product is not yet available.");

            var buyNowItem = new CartItem
            {
                CartItemId = 0,
                ProductId = product.ProductId,
                Product = product,
                Quantity = quantity,
                Price = product.Price,
                SelectedVariations = selectedVariations ?? "{}"
            };

            var addresses = await _context.DeliveryField
                .Where(a => a.UserId == customerId)
                .ToListAsync();

            var checkout = new Checkout
            {
                Customer = customer,
                CartItems = new List<CartItem> { buyNowItem },
                Addresses = addresses
            };

            return OperationResult<Checkout>.Ok(checkout);
        }

        public async Task<int> GetCartItemCountAsync(int customerId)
        {
            return await _context.CartItem
                .Where(ci => ci.Cart.UserId == customerId)
                .SumAsync(ci => ci.Quantity);
        }
    }
}