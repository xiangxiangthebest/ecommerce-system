using EcommerceSystem.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EcommerceSystem.Models;
using EcommerceSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly ICustomerContext _customerContext;
        private readonly ISellerContext _sellerContext;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ICartService _cartService;
        private readonly INotificationService _notificationService;

        public ChatController(
            IChatService chatService, 
            ICustomerContext customerContext, 
            AppDbContext context, 
            IWebHostEnvironment environment,
            ICartService cartService,
            INotificationService notificationService,
            ISellerContext sellerContext)
        {
            _chatService = chatService;
            _customerContext = customerContext;
            _context = context;
            _environment = environment;
            _cartService = cartService;
            _notificationService = notificationService;
            _sellerContext = sellerContext;
        }

        private async Task LoadNavbarAsync()
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return;

            ViewBag.CartCount = await _cartService.GetCartItemCountAsync(customer.UserId);

            var notifications = await _notificationService.GetForUserAsync(customer.UserId);
            ViewBag.UnreadNotificationCount = notifications?.Count(n => !n.IsRead) ?? 0;
        }

        [HttpGet]
        public async Task<IActionResult> SellerInbox()
        {
            //Through Claim get the SellerId
            var sellerIdClaim = User.FindFirst("SellerId")?.Value;
            if (string.IsNullOrEmpty(sellerIdClaim) || !int.TryParse(sellerIdClaim, out int sellerId))
            {
                return Forbid();
            }

            var inboxList = await _chatService.GetSellerInboxListAsync(sellerId);
            return View(inboxList);
        }

        // This chat room is opened from the product detail page, so the productId should be passed as a parameter.
        [HttpPost]
        public async Task<IActionResult> CustomerStartConversation(int sellerId, int? productId, string? variationJson)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Forbid();

            var chatRoom = await _chatService.GetChatRoomAsync(sellerId, customer.UserId, productId);
            if (chatRoom.ChatRoomId == 0)
            {
                return RedirectToAction("CustomerConversation", new { id = 0, sellerId = sellerId, productId = productId });
            }

            return RedirectToAction("CustomerConversation", new { id = chatRoom.ChatRoomId, productId = productId });
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(int chatRoomId, int sellerId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest("Message cannot be empty");
            }

            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            var currentUserId = customer.UserId;

            if (chatRoomId == 0)
            {
                var newRoom = new ChatRoom
                {
                    CustomerId = currentUserId,
                    SellerId = sellerId
                };
                _context.ChatRoom.Add(newRoom);
                await _context.SaveChangesAsync();

                chatRoomId = newRoom.ChatRoomId;
            }
            await _chatService.SendMessageAsync(chatRoomId, currentUserId, message);
            return RedirectToAction("CustomerConversation", new { id = chatRoomId });
        }
        
        [HttpPost]
        public async Task<IActionResult> SellerSendMessage(int chatRoomId, int sellerId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest("Message cannot be empty");
            }

            var seller = await _sellerContext.GetCurrentSellerAsync(User);
            if (seller == null) return Forbid();
            var currentUserId = seller.UserId;

            if (chatRoomId == 0)
            {
                var newRoom = new ChatRoom
                {
                    CustomerId = currentUserId,
                    SellerId = sellerId
                };
                _context.ChatRoom.Add(newRoom);
                await _context.SaveChangesAsync();

                chatRoomId = newRoom.ChatRoomId;
            }
            await _chatService.SendMessageAsync(chatRoomId, currentUserId, message);
            return RedirectToAction("SellerConversation", new { id = chatRoomId });
        }

        [HttpGet]
        [Route("Customer/CustomerInbox")]
        public async Task<IActionResult> CustomerInbox()
        {
            await LoadNavbarAsync();

            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Forbid();

            var inboxList = await _chatService.GetCustomerInboxListAsync(customer.UserId);
            return View("~/Views/Customer/ChatList.cshtml", inboxList);
        }

        [HttpGet]
        public async Task<IActionResult> CustomerConversation(int id, int? sellerId, int? productId)
        {
            await LoadNavbarAsync();
            
            ChatRoom? chatRoom;
            if (id == 0)
            {
                var customer = await _customerContext.GetCurrentCustomerAsync(User);
                if (customer == null) return Forbid();

                Seller? dbSeller = null;
                if (sellerId.HasValue)
                {
                    var productWithSeller = await _context.Products
                        .Include(p => p.Seller)
                        .FirstOrDefaultAsync(p => p.SellerId == sellerId.Value);

                    if (productWithSeller != null)
                    {
                        dbSeller = productWithSeller.Seller;
                    }
                }

                chatRoom = new ChatRoom
                {
                    ChatRoomId = 0,
                    CustomerId = customer.UserId,
                    SellerId = sellerId ?? 0,
                    Messages = new List<ChatMessage>(),
                    Seller = dbSeller
                };
            }
            else
            {
                chatRoom = await _chatService.GetChatRoomByIdAsync(id);

                if (chatRoom == null)
                {
                    return NotFound("真实的聊天室不存在");
                }

                var customer = await _customerContext.GetCurrentCustomerAsync(User);
                if (customer != null)
                {
                    await _chatService.MarkMessagesAsReadAsync(id, customer.UserId);
                }

                if (chatRoom.Seller == null && chatRoom.SellerId > 0)
                {
                    var productWithSeller = await _context.Products
                        .Include(p => p.Seller)
                        .FirstOrDefaultAsync(p => p.SellerId == chatRoom.SellerId);

                    if (productWithSeller != null)
                    {
                        chatRoom.Seller = productWithSeller.Seller;
                    }
                }
            }

            if (productId.HasValue)
            {
                var chatProduct = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == productId.Value);
                ViewBag.ChatProduct = chatProduct;
            }

            return View("~/Views/Customer/ChatRoom.cshtml", chatRoom);
        }


        [HttpGet]
        public async Task<IActionResult> GetSellerProducts(int sellerId)
        {
            var products = await _context.Products
                .Where(p => p.SellerId == sellerId && !p.IsDeleted && !p.IsDraft)
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    productId = p.ProductId,
                    name      = p.Name,
                    price     = p.Price,
                    imagePath = p.ImagePath ?? "/images/default-product.jpg"
                })
                .ToListAsync();

            return Json(products);
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerOrders(int sellerId)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null)
            {
                return Unauthorized(new { message = "登录状态已过期，请重新登录" });
            }

            var currentUserId = customer.UserId;

            var matchedOrderData = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi => oi.Order != null && oi.Product != null && oi.Order.CustomerUserId == currentUserId && oi.Product.SellerId == sellerId)
                .OrderByDescending(oi => oi.Order!.OrderTime)
                .Select(oi => new
                {
                    OrderId = oi.OrderId,
                    TotalAmount = oi.Order!.TotalAmount, 
                    OrderDate = oi.Order!.OrderTime.ToString("yyyy-MM-dd HH:mm"),
                    ProductName = oi.Product!.Name
                        .Replace("'", "")
                        .Replace("\"", "")
                        .Replace("\r", "")
                        .Replace("\n", ""),
                    ProductImage = oi.Product!.ImagePath, 
                    Quantity = oi.Quantity,
                    Price = oi.Price
                })
                .ToListAsync();


            var orders = matchedOrderData
                .GroupBy(x => x.OrderId)
                .Select(g => new
                {
                    OrderId = g.Key,
                    TotalAmount = g.First().TotalAmount,
                    OrderDate = g.First().OrderDate,

                    CoverImage = g.First().ProductImage,
                    CoverName = g.First().ProductName,
                    ItemCount = g.Count() 
                })
                .ToList();

            return Json(orders);
        }

        [HttpGet]
        public async Task<IActionResult> SellerGetTheCustomerOrders(int customerId)
        {
            var seller = await _sellerContext.GetCurrentSellerAsync(User);
            if (seller == null)
            {
                return Unauthorized(new { message = "登录状态已过期，请重新登录" });
            }
            var currentUserId = seller.UserId; 

            var matchedOrderData = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi => oi.Order != null && oi.Product != null
                        && oi.Order.CustomerUserId == customerId            
                        && (oi.Order.SellerUserId == currentUserId || oi.Product.SellerId == currentUserId)) 
                .Select(oi => new
                {
                    OrderId = oi.OrderId,
                    TotalAmount = oi.Order!.TotalAmount,            
                    Status = oi.Order!.CurrentStatus,                
                    
                    ProductName = oi.Product != null && oi.Product.Name != null
                        ? oi.Product.Name.Replace("'", "").Replace("\"", "").Replace("\r", "").Replace("\n", "")
                        : "商品",
                        
                    ProductImage = oi.Product != null ? oi.Product.ImagePath : "/images/default-product.jpg",
                    Quantity = oi.Quantity,
                    Price = oi.Price
                })
                .ToListAsync();

            var orders = matchedOrderData
                .GroupBy(x => x.OrderId)
                .Select(g => new
                {
                    OrderId = g.Key,
                    TotalAmount = g.First().TotalAmount,
                    Status = g.First().Status,
                    CoverImage = g.First().ProductImage,
                    CoverName = g.First().ProductName,
                    ItemCount = g.Count() 
                })
                .ToList();

            return Json(orders);
        }


        [HttpPost]
        public async Task<IActionResult> SellerStartConversation(int customerId, int? orderId)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            var chatRoom = await _context.ChatRoom
                .FirstOrDefaultAsync(r => r.SellerId == currentUserId && r.CustomerId == customerId);

            if (chatRoom == null)
            {
                return RedirectToAction("SellerConversation", new { id = 0, customerId = customerId, orderId = orderId });
            }
            return RedirectToAction("SellerConversation", new { id = chatRoom.ChatRoomId, orderId = orderId });
        }

        [HttpGet]
        public async Task<IActionResult> SellerConversation(int id, int? customerId, int? orderId)
        {
            ChatRoom? chatRoom;
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            if (id == 0)
            {
                if (!customerId.HasValue) return BadRequest("缺少顾客参数");

                var customerInfo = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == customerId.Value);
                var sellerInfo = await _context.Seller.FirstOrDefaultAsync(s => s.UserId == currentUserId);

                chatRoom = new ChatRoom
                {
                    ChatRoomId = 0, 
                    CustomerId = customerId.Value,
                    SellerId = currentUserId,
                    Messages = new List<ChatMessage>(),
                    Customer = customerInfo,
                    Seller = sellerInfo
                };
            }
            else
            {
                chatRoom = await _context.ChatRoom
                    .Include(r => r.Messages)
                    .Include(r => r.Customer)
                    .Include(r => r.Seller)
                    .FirstOrDefaultAsync(r => r.ChatRoomId == id);

                if (chatRoom == null) return NotFound("该聊天室不存在");
                
                await _chatService.MarkMessagesAsReadAsync(id, currentUserId);
            }

            if (orderId.HasValue)
            {
                var orderContext = await _context.Order
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId.Value);

                if (orderContext != null)
                {
                    ViewBag.InitiatedOrder = orderContext;
                    ViewBag.IsOrderInitiated = true; 
                }
            }

            ViewBag.ActiveTab = "ChatRoom"; 
            ViewBag.IsApproved = true;
            ViewBag.ShopName = chatRoom.Seller?.ShopName ?? "Seller Dashboard";

            if (!string.IsNullOrEmpty(HttpContext.Request.Query["search"]))
            {
                ViewBag.AutoSearchKeyword = HttpContext.Request.Query["search"].ToString();
            }
            return View("~/Views/Seller/Home.cshtml", chatRoom);
        }

        [HttpPost]
        public async Task<IActionResult> CreateChatRoom(int customerId)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var existing = await _context.ChatRoom
                .FirstOrDefaultAsync(r => r.SellerId == currentUserId && r.CustomerId == customerId);

            if (existing != null)
            {
                return Json(new { chatRoomId = existing.ChatRoomId });
            }

            var newRoom = new ChatRoom
            {
                SellerId = currentUserId,
                CustomerId = customerId
            };

            _context.ChatRoom.Add(newRoom);
            await _context.SaveChangesAsync();

            return Json(new { chatRoomId = newRoom.ChatRoomId });
        }

        [HttpGet]
        public async Task<IActionResult> GetProductNameById(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (product != null)
            {
                var cleanName = product.Name?
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Trim();

                return Json(new { name = cleanName });
            }
            return Json(new { name = "" });
        }

        [Authorize(Roles = "Seller")]
        public async Task<IActionResult> StartConversationFromOrder(int customerId)
        {
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var seller = await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);
            if (seller == null) return RedirectToAction("Login", "Account");

            var chatRoom = await _context.ChatRoom
                .FirstOrDefaultAsync(cr => cr.SellerId == seller.UserId && cr.CustomerId == customerId);

            if (chatRoom == null)
            {
                chatRoom = new ChatRoom
                {
                    SellerId = seller.UserId,
                    CustomerId = customerId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ChatRoom.Add(chatRoom);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("SellerConversation", "Chat", new { id = chatRoom.ChatRoomId });
        }

    }

}