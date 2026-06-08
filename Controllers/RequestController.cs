using Microsoft.AspNetCore.Mvc;
using EcommerceSystem.Enums;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Data;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;         


namespace EcommerceSystem.Controllers
{
    public class RequestController : Controller
    {
        private readonly ICustomerContext _customerContext;
        private readonly AppDbContext _context;
        public RequestController(ICustomerContext customerContext, AppDbContext context)
        {
            _customerContext = customerContext;
            _context = context;
        }

        private async Task<Seller?> GetCurrentSellerAsync()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            return await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAfterSalesRequest(
            int orderId,
            string requestServiceType,
            string requestIssueType,
            string description,
            List<IFormFile>? images)
        {
            var user = await _customerContext.GetCurrentCustomerAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found" });
            var order = await _context.Order.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return Json(new { success = false, message = "Order not found" });
            order.CurrentStatus = OrderStatus.AFTER_SALES_REQUESTED;

            if (!Enum.TryParse<RequestServiceType>(requestServiceType, out var serviceType))
                return Json(new { success = false, message = "Invalid service type" });

            if (!Enum.TryParse<RequestIssueType>(requestIssueType, true, out var issueType)
                || !Enum.IsDefined(typeof(RequestIssueType), issueType))
                return Json(new { success = false, message = "Invalid issue type" });

            var request = new Request
            {
                RequestUserId = user.UserId,
                OrderId = orderId,
                RequestServiceType = serviceType,  
                RequestIssueType = issueType,
                Description = description,
            };

            _context.Request.Add(request);
            await _context.SaveChangesAsync();

            if (images != null && images.Count > 0)
            {
                foreach (var file in images)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    var path = Path.Combine("wwwroot/uploads", fileName);

                    using var stream = new FileStream(path, FileMode.Create);
                    await file.CopyToAsync(stream);

                    _context.RequestImage.Add(new RequestImage
                    {
                        RequestId = request.RequestId,
                        ImagePath = fileName
                    });
                }

                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetRequest(int orderId)
        {
            var request = await _context.Request
                .Include(r => r.Images)
                .FirstOrDefaultAsync(r => r.OrderId == orderId);

            if (request == null)
                return Json(new { success = false, message = "Request not found" });

            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return Json(new { success = false, message = "Order not found" });

            return Json(new
            {
                success = true,
                customerName = order.Customer?.FullName ?? "Customer",
                orderItems = order.OrderItems.Select(oi => new
                {
                    productName = oi.Product?.Name,
                    quantity = oi.Quantity,
                    price = oi.Price,
                    imageUrl = oi.Product?.ImagePath != null ? "/images/" + Path.GetFileName(oi.Product.ImagePath) : null
                }).ToList(),
                serviceType = request.RequestServiceType switch
                {
                    RequestServiceType.RETURN_REFUND => "Return & Refund",
                    RequestServiceType.REFUND => "Refund Only",
                    RequestServiceType.SUSPEND_ACCOUNT => "Suspend Account",
                    _ => "Other"
                },
                issueType = request.RequestIssueType switch
                {
                    RequestIssueType.WrongItemReceived => "Wrong item received",
                    RequestIssueType.ChangeOfMind => "Change of mind",
                    RequestIssueType.ItemNotAsDescribed => "Item not as described",
                    RequestIssueType.ItemNotDelivered => "Item not delivered",
                    RequestIssueType.DamagedDefective => "Damaged / defective",
                    RequestIssueType.MissingPartsAccessories => "Missing parts / accessories",
                    _ => "Other"
                },
                description = request.Description,
                createdAt = request.CreatedAt.ToString("dd MMM yyyy, hh:mm tt"),
                images = request.Images.Select(img => "/uploads/" + img.ImagePath).ToList(),
                requestId = request.RequestId
            });
        }

        [HttpPost]
        public IActionResult ApproveRequest(int requestId)
        {
            var request = _context.Request
                .Include(r => r.Order)
                .FirstOrDefault(r => r.RequestId == requestId);

            if (request == null) return NotFound();

            IRequestStrategy strategy;

            if (request.RequestServiceType == RequestServiceType.RETURN_REFUND)
            {
                strategy = new ReturnRefundStrategy();
            }
            else if (request.RequestServiceType == RequestServiceType.REFUND)
            {
                strategy = new RefundStrategy();
            }
            else if (request.RequestServiceType == RequestServiceType.SUSPEND_ACCOUNT)
            {
                strategy = new SuspendAccountStrategy();
            }
            else if (request.RequestServiceType == RequestServiceType.ACTIVE_ACCOUNT)
            {
                strategy = new ActivateAccountStrategy();
            }
            else
            {
                return BadRequest("Invalid request type for approval.");
            }

            strategy.Solve(request);
            request.ApprovedAt = DateTime.UtcNow;
            request.ReviewedBy = User.Identity?.Name;
            _context.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RejectAfterSale(int requestId, int orderId)
        {
            var request = await _context.Request
                .Include(r => r.Order)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request == null) return NotFound();

            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return NotFound();

            order.CurrentStatus = OrderStatus.DELIVERED;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}

