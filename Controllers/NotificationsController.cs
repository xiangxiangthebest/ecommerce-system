using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EcommerceSystem.Interfaces;
using System.Security.Claims;
using EcommerceSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;
    private readonly AppDbContext _context;

    public NotificationsController(INotificationService notificationService, AppDbContext context)
    {
        _notificationService = notificationService;
        _context = context;
    }

    private async Task<int> GetCurrentUserIdAsync()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        return user?.UserId ?? 0;
    }

    // Used by the navbar dropdown (Seller/Admin) — returns JSON
    [HttpGet]
    public async Task<IActionResult> GetDropdown()
    {
        var userId = await GetCurrentUserIdAsync();
        var notifications = await _notificationService.GetForUserAsync(userId);
        var unread = notifications.Count(n => !n.IsRead);

        return Json(new {
            unreadCount = unread,
            items = notifications.Take(10).Select(n => new {
                n.NotificationId,
                n.Title,
                n.Message,
                n.IsRead,
                n.OrderId,
                createdAt = n.CreatedAt.ToString("dd MMM, h:mm tt")
            })
        });
    }

    // Used by BOTH dropdown (fetch POST) and the customer page (form POST)
    // Returns Ok() for fetch calls; redirects for form submits
    [HttpPost]
    public async Task<IActionResult> MarkRead([FromQuery] int id)
    {
        await _notificationService.MarkAsReadAsync(id);

        // If request came from a browser form (not fetch), redirect back
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest"
            && !Request.Headers.ContainsKey("RequestVerificationToken"))
        {
            return RedirectToAction("Notifications", "Customer");
        }

        return Ok();
    }

    // Dropdown version — returns JSON (called via fetch by seller/admin JS)
    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = await GetCurrentUserIdAsync();
        await _notificationService.MarkAllAsReadAsync(userId);

        // If it's a regular form POST (customer page), redirect back
        if (!Request.Headers.ContainsKey("X-Requested-With"))
        {
            return RedirectToAction("Notifications", "Customer");
        }

        return Ok();
    }
}