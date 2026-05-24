using EcommerceSystem.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EcommerceSystem.Models;
using System.Threading.Tasks;
using EcommerceSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly ICustomerContext _customerContext;
        private readonly AppDbContext _context;

        public ChatController(IChatService chatService, ICustomerContext customerContext, AppDbContext context)
        {
            _chatService = chatService;
            _customerContext = customerContext;
            _context = context;
        }



        /// <summary>
        /// 2. 卖家端：获取商家的收件箱列表
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SellerInbox()
        {
            // 假设你通过 Claim 拿到了 SellerId，或者根据实际业务获取
            var sellerIdClaim = User.FindFirst("SellerId")?.Value;
            if (string.IsNullOrEmpty(sellerIdClaim) || !int.TryParse(sellerIdClaim, out int sellerId))
            {
                return Forbid(); // 不是商家则禁止访问
            }

            var inboxList = await _chatService.GetSellerInboxListAsync(sellerId);
            return View(inboxList); // 对应 Views/Chat/SellerInbox.cshtml
        }

        /// <summary>
        /// 3. 发起或进入聊天室
        /// 根据你在商品详情页点击“联系商家”，传入商家的 sellerId
        /// </summary>
[HttpPost]
public async Task<IActionResult> StartConversation(int sellerId, int? productId, string? variationJson)
{
    var customer = await _customerContext.GetCurrentCustomerAsync(User);

    // 1. 去数据库查一下，买家和这个卖家以前有没有聊过天
    // 💡 注意：这里调用你原先的查房 Service，但要确保它【不自动 Add 进数据库】
    var chatRoom = await _chatService.GetChatRoomAsync(sellerId, customer.UserId, productId);

    // 2. 如果 chatRoom.ChatRoomId 已经是 0（说明是新房间，Service 没存盘）
    if (chatRoom.ChatRoomId == 0)
    {
        // 携带 sellerId 隐式参数去往 0 号虚拟房间
        return RedirectToAction("Conversation", new { id = 0, sellerId = sellerId });
    }

    // 3. 如果是老房间，正常带上真实 ID 去往老房间
    return RedirectToAction("Conversation", new { id = chatRoom.ChatRoomId });
}


        /// <summary>
        /// 5. 发送消息
        /// </summary>
       [HttpPost]
public async Task<IActionResult> SendMessage(int chatRoomId, int sellerId, string message)
{
    if (string.IsNullOrWhiteSpace(message))
    {
        return BadRequest("消息不能为空");
    }

    // 获取当前登录用户 ID（作为买家）
    var customer = await _customerContext.GetCurrentCustomerAsync(User);
    var currentUserId = customer.UserId;

    // 💡 核心逻辑：如果是 0，说明数据库还没有这个房间，这是第一条消息！
    if (chatRoomId == 0)
    {
        // 1. 动态在数据库创建新房间
        var newRoom = new ChatRoom
        {
            CustomerId = currentUserId,
            SellerId = sellerId
        };
        _context.ChatRoom.Add(newRoom);
        await _context.SaveChangesAsync(); // 此时拿到了真正的自增主键 newRoom.ChatRoomId

        chatRoomId = newRoom.ChatRoomId; // 2. 把 0 替换成真正的房间 ID
    }

    // 3. 走原有的逻辑：调用 Service 发送并保存这条消息
    await _chatService.SendMessageAsync(chatRoomId, currentUserId, message);

    // 4. 发送完后，刷新并留在当前已经真实创建的聊天室里
    return RedirectToAction("Conversation", new { id = chatRoomId });
}


        // 1. 导航栏点击后，去加载收件箱列表 (指向 ChatList.cshtml)
[HttpGet]
[Route("Customer/CustomerInbox")]
public async Task<IActionResult> CustomerInbox()
{
    var customer = await _customerContext.GetCurrentCustomerAsync(User);
    var inboxList = await _chatService.GetCustomerInboxListAsync(customer.UserId);
    
    // 💡 明确指向绝对路径的 ChatList.cshtml
    return View("~/Views/Customer/ChatList.cshtml", inboxList); 
}

[HttpGet]
public async Task<IActionResult> Conversation(int id, int? sellerId)
{
    ChatRoom chatRoom;

    // 1. 💡 如果 id 是 0，说明是尚未创建的虚拟临时聊天室（首次沟通）
    if (id == 0)
    {
        var customer = await _customerContext.GetCurrentCustomerAsync(User);
        
        Seller? dbSeller = null;
        if (sellerId.HasValue)
        {
            // 通过商品表连表查出 Seller
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
    // 2. 💡 如果 id 不是 0，说明已经建立了真实聊天室（发送消息后）
    else
    {
        // 先从原来的 Service 获取基础聊天室和消息数据
        chatRoom = await _chatService.GetChatRoomByIdAsync(id);
        
        if (chatRoom == null) 
        {
            return NotFound("真实的聊天室不存在");
        }

        // 🔥【核心修复逻辑】：如果老房间的 Seller 是 null，我们主动去帮它 Fetch 出来！
        if (chatRoom.Seller == null && chatRoom.SellerId > 0)
        {
            var productWithSeller = await _context.Products
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.SellerId == chatRoom.SellerId);

            if (productWithSeller != null)
            {
                chatRoom.Seller = productWithSeller.Seller; // 完美补全 Seller 信息！
            }
        }
    }

    // 强行渲染前端视图
    return View("~/Views/Customer/ChatRoom.cshtml", chatRoom); 
}
        
    }
}