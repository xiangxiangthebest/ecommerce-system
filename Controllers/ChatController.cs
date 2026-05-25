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
        
    }
}