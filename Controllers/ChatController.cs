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
                return BadRequest("消息不能为空");
            }

            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Forbid();
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
                return BadRequest("消息不能为空");
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

            // 1. 如果 id 是 0，说明是尚未创建的虚拟临时聊天室（从商品页初次进来）
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
            // 2. 如果 id 不是 0，说明已经建立了真实聊天室（发送消息后，或者从 Inbox 进来）
            else
            {
                chatRoom = await _chatService.GetChatRoomByIdAsync(id);

                if (chatRoom == null)
                {
                    return NotFound("真实的聊天室不存在");
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

            // 💡【新增核心逻辑】：如果携带了 productId，Fetch 该商品信息并塞给 ViewBag
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
            // 从你的商品表查出属于该 sellerId 的前10条产品数据
            var products = await _context.Products
                .Where(p => p.SellerId == sellerId)
                .Select(p => new
                {
                    p.ProductId,
                    p.Name,
                    p.Price,
                    p.ImagePath
                })
                .Take(10)
                .ToListAsync();

            return Json(products);
        }

        // 2. 💡 获取该顾客与该商家有关联的历史订单
        [HttpGet]
        public async Task<IActionResult> GetCustomerOrders(int sellerId)
        {
            // 1. 获取当前登录的买家用户
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null)
            {
                return Unauthorized(new { message = "登录状态已过期，请重新登录" });
            }

            var currentUserId = customer.UserId;

            // 2. 💡 核心筛选：从订单明细表开始查，精准过滤出【属于该买家】且【商品属于该商家】的所有订单明细
            // 假设你的 DbContext 里订单明细表叫 OrderItems，订单总表叫 Orders（请根据你项目实际 DbSet 名字微调）
            var matchedOrderData = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi => oi.Order != null && oi.Product != null && oi.Order.CustomerUserId == currentUserId && oi.Product.SellerId == sellerId)
                .OrderByDescending(oi => oi.Order!.OrderTime)
                .Select(oi => new
                {
                    OrderId = oi.OrderId,
                    TotalAmount = oi.Order!.TotalAmount, // 订单总金额
                    OrderDate = oi.Order!.OrderTime.ToString("yyyy-MM-dd HH:mm"),
                    ProductName = oi.Product!.Name
                        .Replace("'", "")
                        .Replace("\"", "")
                        .Replace("\r", "")
                        .Replace("\n", ""),
                    ProductImage = oi.Product!.ImagePath, // 该明细的商品图
                    Quantity = oi.Quantity,
                    Price = oi.Price
                })
                .ToListAsync();

            // 3. 把明细按照 OrderId 进行分组聚合，这样一个订单就算买了当前店铺多个商品也能漂亮地合并显示
            var orders = matchedOrderData
                .GroupBy(x => x.OrderId)
                .Select(g => new
                {
                    OrderId = g.Key,
                    TotalAmount = g.First().TotalAmount,
                    OrderDate = g.First().OrderDate,
                    // 顺便把买的第一个商品的图文提出来当做卡片封面
                    CoverImage = g.First().ProductImage,
                    CoverName = g.First().ProductName,
                    ItemCount = g.Count() // 买了该店多少件商品
                })
                .ToList();

            return Json(orders);
        }

        [HttpGet]
public async Task<IActionResult> SellerGetTheCustomerOrders(int customerId)
{
    // 1. 获取当前登录的商家用户
    var seller = await _sellerContext.GetCurrentSellerAsync(User);
    if (seller == null)
    {
        return Unauthorized(new { message = "登录状态已过期，请重新登录" });
    }
    var currentUserId = seller.UserId; // 商家在主用户表中的 ID

    // 2. 💡【核心筛选】：精准过滤出【买家是当前聊天的客户】且【订单属于当前登录商家】的所有订单
    // 提示：根据您真实的数据库关联，我们直接从 Order 订单总表或明细表出发检索均可。
    // 这里保持您原汁原味的 OrderItems 链路，但彻底纠正条件映射：
    var matchedOrderData = await _context.OrderItems
        .Include(oi => oi.Order)
        .Include(oi => oi.Product)
        .Where(oi => oi.Order != null && oi.Product != null
                  && oi.Order.CustomerUserId == customerId              // A. 必须是这个买家下的单
                  && (oi.Order.SellerUserId == currentUserId || oi.Product.SellerId == currentUserId)) // B. 订单或者商品必须属于当前登录商家
        .Select(oi => new
        {
            OrderId = oi.OrderId,
            TotalAmount = oi.Order!.TotalAmount,                     // 订单总金额                      // 先拿原始 DateTime 对象，后续进行内存化处理或转 String
            Status = oi.Order!.CurrentStatus,                  // 订单状态 (如 Pending/Shipped)
            
            // 💡【安全防错】：优先做 ?.Null 保护，再清除可能破坏前端 JS 拼接的特殊字符
            ProductName = oi.Product != null && oi.Product.Name != null
                ? oi.Product.Name.Replace("'", "").Replace("\"", "").Replace("\r", "").Replace("\n", "")
                : "商品",
                
            ProductImage = oi.Product != null ? oi.Product.ImagePath : "/images/default-product.jpg", // 商品图保底
            Quantity = oi.Quantity,
            Price = oi.Price
        })
        .ToListAsync();

    // 3. 把明细按照 OrderId 进行分组聚合，这样一个订单就算买了当前店铺多个商品也能漂亮地合并显示
    var orders = matchedOrderData
        .GroupBy(x => x.OrderId)
        .Select(g => new
        {
            OrderId = g.Key,
            TotalAmount = g.First().TotalAmount,
            // 后端格式化好标准时间字符串传回给前端直接显
            Status = g.First().Status,
            // 顺便把买的第一个商品的图文提出来当做卡片封面
            CoverImage = g.First().ProductImage,
            CoverName = g.First().ProductName,
            ItemCount = g.Count() // 买了该店多少件商品
        })
        .ToList();

    // 4. 返回干净透彻的 JSON 数组供前端动态 Ajax 捞取渲染
    return Json(orders);
}

        // 1. 卖家从订单页点击 Chat 图标触发的中转 Action
        [HttpPost]
        public async Task<IActionResult> SellerStartConversation(int customerId, int? orderId)
        {
            // 获取当前登录用户的 ID 
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            // 💡 优先通过数据库查找这两名用户之间是否已经开通过聊天室
            var chatRoom = await _context.ChatRoom
                .FirstOrDefaultAsync(r => r.SellerId == currentUserId && r.CustomerId == customerId);

            if (chatRoom == null)
            {
                // 如果没有真实聊天室，带上 id = 0 标记进入临时虚拟房，并把 customerId 和 orderId 作为上下文传下去
                return RedirectToAction("SellerConversation", new { id = 0, customerId = customerId, orderId = orderId });
            }

            // 如果原先就有聊天，则带上真实的房间 ID 进去
            return RedirectToAction("SellerConversation", new { id = chatRoom.ChatRoomId, orderId = orderId });
        }

// 2. 🌟 卖家端专属聊天会话 Action (独立出来，避免和买家端页面揉在一起报错)
        [HttpGet]
        public async Task<IActionResult> SellerConversation(int id, int? customerId, int? orderId)
        {
            ChatRoom? chatRoom;
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // ======= 情况 A: 虚拟聊天室（初次点进来，还未正式创建记录） =======
            if (id == 0)
            {
                if (!customerId.HasValue) return BadRequest("缺少顾客参数");

                // 在内存中构建虚拟的 ChatRoom 供前端渲染框架使用，不 Save 进数据库
                var customerInfo = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == customerId.Value);
                var sellerInfo = await _context.Seller.FirstOrDefaultAsync(s => s.UserId == currentUserId);

                chatRoom = new ChatRoom
                {
                    ChatRoomId = 0, // 0 代表临时虚拟房
                    CustomerId = customerId.Value,
                    SellerId = currentUserId,
                    Messages = new List<ChatMessage>(),
                    Customer = customerInfo,
                    Seller = sellerInfo
                };
            }
            // ======= 情况 B: 真实聊天室（已有历史记录，或直接从 Inbox 点击加载） =======
            else
            {
                chatRoom = await _context.ChatRoom
                    .Include(r => r.Messages)
                    .Include(r => r.Customer)
                    .Include(r => r.Seller)
                    .FirstOrDefaultAsync(r => r.ChatRoomId == id);

                if (chatRoom == null) return NotFound("该聊天室不存在");
            }

            // ======= 提取订单信息（如果存在）传给前端展示预载小框 =======
            if (orderId.HasValue)
            {
                var orderContext = await _context.Order
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId.Value);

                if (orderContext != null)
                {
                    ViewBag.InitiatedOrder = orderContext;
                    ViewBag.IsOrderInitiated = true; // 开启订单推荐开关
                }
            }

            // ======= 设置 Dashboard 主页面框架侧边栏高亮环境变量 =======
            ViewBag.ActiveTab = "ChatRoom"; 
            ViewBag.IsApproved = true;
            ViewBag.ShopName = chatRoom.Seller?.ShopName ?? "Seller Dashboard";

            if (!string.IsNullOrEmpty(HttpContext.Request.Query["search"]))
            {
                ViewBag.AutoSearchKeyword = HttpContext.Request.Query["search"].ToString();
            }

            // 💡 重点：把当前会话投递给卖家主框架 Home.cshtml，由它在右侧内嵌渲染 ChatRoom
            return View("~/Views/Seller/Home.cshtml", chatRoom);
        }

        // 3. 💡 纯异步轻量接口：供前端 JS 延迟创建聊天室使用
        [HttpPost]
        public async Task<IActionResult> CreateChatRoom(int customerId)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // 再次安全拦截，防止并发时重复创建
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
                // 顺便洗掉商品名字里可能存在的特殊换行符
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
            // 1. 获取当前登录的卖家 ID
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var seller = await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);
            if (seller == null) return RedirectToAction("Login", "Account");

            // 2. 检查数据库里，该卖家和此客户是否已经存在聊天室
            var chatRoom = await _context.ChatRoom
                .FirstOrDefaultAsync(cr => cr.SellerId == seller.UserId && cr.CustomerId == customerId);

            // 3. 如果是第一次聊天（聊天室不存在），原地自动帮他们建立一个
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

            // 4. 🚀 关键：拿到了 ChatRoomId (比如 1)，顺畅重定向到你原本工作完美的聊天页面！
            return RedirectToAction("SellerConversation", "Chat", new { id = chatRoom.ChatRoomId });
        }

    }

}