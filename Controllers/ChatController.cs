using EcommerceSystem.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EcommerceSystem.Models;
using System.Threading.Tasks;
using EcommerceSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace EcommerceSystem.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly ICustomerContext _customerContext;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ChatController(IChatService chatService, ICustomerContext customerContext, AppDbContext context,IWebHostEnvironment environment)
        {
            _chatService = chatService;
            _customerContext = customerContext;
            _context = context;
            _environment = environment;
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

        [HttpPost]
public async Task<IActionResult> StartConversation(int sellerId, int? productId, string? variationJson)
{
    var customer = await _customerContext.GetCurrentCustomerAsync(User);
    var chatRoom = await _chatService.GetChatRoomAsync(sellerId, customer.UserId, productId);

    // 💡 无论新旧房间，只要是从商品页点进来的，都把 productId 传过去
    if (chatRoom.ChatRoomId == 0)
    {
        return RedirectToAction("Conversation", new { id = 0, sellerId = sellerId, productId = productId });
    }

    return RedirectToAction("Conversation", new { id = chatRoom.ChatRoomId, productId = productId });
}

        [HttpPost]
        public async Task<IActionResult> SendMessage(int chatRoomId, int sellerId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest("消息不能为空");
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
            return RedirectToAction("Conversation", new { id = chatRoomId });
        }


        [HttpGet]
        [Route("Customer/CustomerInbox")]
        public async Task<IActionResult> CustomerInbox()
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            var inboxList = await _chatService.GetCustomerInboxListAsync(customer.UserId);
            
            return View("~/Views/Customer/ChatList.cshtml", inboxList);
        }

        [HttpGet]
        public async Task<IActionResult> Conversation(int id, int? sellerId, int? productId)
        {
            ChatRoom chatRoom;

            // 1. 如果 id 是 0，说明是尚未创建的虚拟临时聊天室（从商品页初次进来）
            if (id == 0)
            {
                var customer = await _customerContext.GetCurrentCustomerAsync(User);

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
        
                // 1. 💡 获取当前商家名下的所有上架商品
        [HttpGet]
        public async Task<IActionResult> GetSellerProducts(int sellerId)
        {
            // 从你的商品表查出属于该 sellerId 的前10条产品数据
            var products = await _context.Products
                .Where(p => p.SellerId == sellerId)
                .Select(p => new {
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
            var currentUserId = customer.UserId;

            // 2. 💡 核心筛选：从订单明细表开始查，精准过滤出【属于该买家】且【商品属于该商家】的所有订单明细
            // 假设你的 DbContext 里订单明细表叫 OrderItems，订单总表叫 Orders（请根据你项目实际 DbSet 名字微调）
            var matchedOrderData = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi => oi.Order.CustomerUserId == currentUserId && oi.Product.SellerId == sellerId)
                .OrderByDescending(oi => oi.Order.OrderTime)
                .Select(oi => new
                {
                    OrderId = oi.OrderId,
                    TotalAmount = oi.Order.TotalAmount, // 订单总金额
                    OrderDate = oi.Order.OrderTime.ToString("yyyy-MM-dd HH:mm"),
                    // 💡【核心防错修复】：强行清除商品名中的单双引号和换行符，防止破坏前端 JS 拼接
                    ProductName = oi.Product.Name
                        .Replace("'", "")
                        .Replace("\"", "")
                        .Replace("\r", "")
                        .Replace("\n", "") ?? "商品",      // 该明细的商品名
                    ProductImage = oi.Product.ImagePath, // 该明细的商品图
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
        
                [HttpPost]
        public async Task<IActionResult> UploadChatImage(IFormFile file, int chatRoomId, int sellerId)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("没有选择任何图片");
            }

            // 1. 确保 wwwroot/uploads/chat/ 文件夹存在
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "chat");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // 2. 生成唯一的文件名（例如：20260525143022_guid.jpg）
            var uniqueFileName = DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString().Substring(0, 8) + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // 3. 把图片保存到服务器硬盘
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // 4. 组装网页可以访问的相对路径 URL
            var imageUrl = "/uploads/chat/" + uniqueFileName;

            // 5. 组装成带 [IMAGE_CARD] 标签的 HTML
            var imageMessageText = $"[IMAGE_CARD]<a href='{imageUrl}' target='_blank'><img src='{imageUrl}' class='img-fluid rounded border shadow-sm' style='max-width: 200px; cursor: zoom-in;' /></a>";

            // 6. 获取当前用户，如果房间号是 0 则先建房间（复用你以前的逻辑）
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            var currentUserId = customer.UserId;

            if (chatRoomId == 0)
            {
                var newRoom = new ChatRoom { CustomerId = currentUserId, SellerId = sellerId };
                _context.ChatRoom.Add(newRoom);
                await _context.SaveChangesAsync();
                chatRoomId = newRoom.ChatRoomId;
            }

            // 7. 将图片消息存入数据库
            await _chatService.SendMessageAsync(chatRoomId, currentUserId, imageMessageText);

            // 返回成功，让前端刷新页面
            return Json(new { success = true, chatRoomId = chatRoomId });
        }
    }
}