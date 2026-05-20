using EcommerceSystem.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcommerceSystem.Controllers
{
   [Authorize]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly ICustomerContext _customerContext;

    public ChatController(IChatService chatService, ICustomerContext customerContext)
    {
        _chatService = chatService;
        _customerContext = customerContext;
    }

    [HttpPost]
    public async Task<IActionResult> StartConversation(int productId, string? variationJson)
    {
        var customer = await _customerContext.GetCurrentCustomerAsync(User);

        var id = await _chatService.StartConversationAsync(
            customer.UserId, productId, variationJson);

        return RedirectToAction("Conversation", new { id });
    }

    public async Task<IActionResult> Conversation(int id)
    {
        var convo = await _chatService.GetConversationAsync(id);
        if (convo == null) return NotFound();

        return View(convo);
    }
}
}