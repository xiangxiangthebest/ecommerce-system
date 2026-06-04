using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Models;
using EcommerceSystem.Data;
using EcommerceSystem.DTOs;
using EcommerceSystem.Factories;
using EcommerceSystem.Interfaces;
 
namespace EcommerceSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IProductReportService _productReportService;
        private readonly IReviewReportService _reviewReportService;
 
        public AdminController(AppDbContext context, IProductReportService productReportService, IReviewReportService reviewReportService)
        {
            _context = context;
            _productReportService = productReportService;
            _reviewReportService = reviewReportService;
        }
 
        // Main Dashboard View
        public IActionResult Index()
        {
            ViewBag.TotalOrders = 150; 
            return View();
        }
 
        // User Management
        public IActionResult ManageUsers()
        {
            return RedirectToAction("ManageSellers");
        }
 
        public async Task<IActionResult> ManageSellers(string searchTerm, string statusFilter)
        {
            var query = _context.Seller.AsQueryable();
 
            // 1. Keyword Search (Shop Name or Email)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(s => s.ShopName.Contains(searchTerm) || s.Email.Contains(searchTerm));
            }
 
            // 2. Status Filter
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = statusFilter switch
                {
                    "Active" => query.Where(s => s.IsActive && s.IsApproved),
                    "Pending" => query.Where(s => s.IsActive && !s.IsApproved),
                    "Banned" => query.Where(s => !s.IsActive),
                    _ => query
                };
            }
 
            var sellers = await query.ToListAsync();
            
            // Pass values back to keep the form state
            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = statusFilter;
            
            return View(sellers);
        }
 
        public IActionResult ManageCustomerService()
        {
            var staffList = _context.CustomerServices.ToList(); 
            return View(staffList);
        }
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveSeller(int id)
        {
            var seller = await _context.Seller.FindAsync(id);
            if (seller == null)
            {
                TempData["AdminError"] = "Seller not found.";
                return RedirectToAction("ManageSellers");
            }
 
            seller.IsApproved = true;
            await _context.SaveChangesAsync();
 
            TempData["AdminSuccess"] = $"{seller.ShopName} has been approved successfully.";
            return RedirectToAction("ManageSellers");
        }
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanSeller(int id)
        {
            var seller = await _context.Seller.FindAsync(id);
            if (seller == null) return NotFound();
 
            // Permanently deactivate the account
            seller.IsActive = false; 
            seller.IsApproved = false; 
 
            await _context.SaveChangesAsync();
 
            TempData["AdminSuccess"] = $"{seller.ShopName} has been permanently banned.";
            return RedirectToAction("ManageSellers");
        }
 
        // Product Control
        [HttpPost]
        public IActionResult ApproveProduct(int productID)
        {
            return RedirectToAction("ManageProducts");
        }
 
        // GET: Display the creation form
        public IActionResult CreateCustomerService()
        {
            return View();
        }
 
        // POST: Process the new account
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomerService(RegisterCustomerServiceDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
 
            var existingUser = await _context.Users.AnyAsync(u => u.Email == model.Email);
            if (existingUser)
            {
                ModelState.AddModelError("Email", "This email address is already in use.");
                return View(model);
            }
 
            UserCreator creator = new CustomerServiceCreator(model); 
            User customerServiceAccount = creator.CreateUser();
 
            try 
            {
                _context.Users.Add(customerServiceAccount);
                await _context.SaveChangesAsync();
 
                TempData["AdminSuccess"] = "Customer Service account created successfully!";
                return RedirectToAction("ManageCustomerService");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving: " + ex.Message);
                return View(model);
            }
        }
 
        [HttpGet]
        public async Task<IActionResult> EditCustomerService(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
 
            var model = new EditCustomerServiceDto
            {
                UserId = user.UserId, 
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };
 
            return View(model);
        }
 
        // POST: Admin/EditCustomerService
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomerService(EditCustomerServiceDto model)
        {
            if (!ModelState.IsValid) return View(model);
 
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null) return NotFound();
 
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email == model.Email && u.UserId != model.UserId);
            
            if (emailExists)
            {
                ModelState.AddModelError("Email", "Email is already in use by another account.");
                return View(model);
            }
 
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;
 
            _context.Update(user);
            await _context.SaveChangesAsync();
 
            TempData["AdminSuccess"] = "Customer Service information updated successfully.";
            return RedirectToAction("ManageCustomerService");
        }
 
        [HttpPost]
        [ActionName("DeleteCustomerService")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomerServiceConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("ManageCustomerService");
            }
 
            try
            {
                user.IsActive = false; 
                
                _context.Update(user);
                await _context.SaveChangesAsync();
 
                TempData["AdminSuccess"] = "Staff member deactivated successfully.";
                return RedirectToAction("ManageCustomerService");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deactivating account: " + ex.Message;
                return RedirectToAction("ManageCustomerService");
            }
        }
 
        // AJAX Action Name aligned to the pattern called by ManageSellers JavaScript:
        [HttpGet]
        public async Task<IActionResult> GetSellerDetails(int id)
        {
            // Kept singular database reference context as required
            var seller = await _context.Seller
                .FirstOrDefaultAsync(s => s.UserId == id);
 
            if (seller == null)
            {
                return NotFound();
            }
 
            return PartialView("SellerDetails", seller);
        }
 
        // Inventory Review - View all products from all sellers
        public async Task<IActionResult> InventoryReview(string searchTerm, string statusFilter, string stockFilter, string deleteFilter)
        {
            var query = _context.Products.Include(p => p.Seller).Include(p => p.Category).AsQueryable();
 
            // 1. Keyword Search (Product Name, SKU, or Shop Name)
            // AFTER — deleteFilter always applies, regardless of whether searchTerm is used
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p =>
                    p.Name.Contains(searchTerm) ||
                    p.SKU.Contains(searchTerm) ||
                        (p.Seller != null && p.Seller.ShopName.Contains(searchTerm)));
            }
 
            // Always apply the delete filter (default is "Active")
            query = deleteFilter switch
            {
                "Deleted" => query.Where(p => p.IsDeleted),
                "All"     => query,
                _         => query.Where(p => !p.IsDeleted)   // "Active" or empty = default
            };
 
            // 3. Status Filter (Draft, Published)
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = statusFilter switch
                {
                    "Draft" => query.Where(p => p.IsDraft),
                    "Published" => query.Where(p => !p.IsDraft),
                    _ => query
                };
            }
 
            // 4. Stock Filter
            if (!string.IsNullOrEmpty(stockFilter))
            {
                query = stockFilter switch
                {
                    "OutOfStock" => query.Where(p => p.StockQuantity == 0),
                    "LowStock" => query.Where(p => p.StockQuantity > 0 && p.StockQuantity < 10),
                    "InStock" => query.Where(p => p.StockQuantity >= 10),
                    _ => query
                };
            }
 
            var products = await query.OrderByDescending(p => p.ProductId).ToListAsync();
 
            // Calculate statistics (all products)
            var allProducts = await _context.Products.Where(p => !p.IsDeleted).ToListAsync();
            ViewBag.TotalProducts = allProducts.Count;
            ViewBag.PublishedCount = allProducts.Count(p => !p.IsDraft);
            ViewBag.DraftCount = allProducts.Count(p => p.IsDraft);
            ViewBag.OutOfStockCount = allProducts.Count(p => p.StockQuantity == 0);
            ViewBag.LowStockCount = allProducts.Count(p => p.StockQuantity > 0 && p.StockQuantity < 10);
            ViewBag.DeletedCount = await _context.Products.CountAsync(p => p.IsDeleted);
 
            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.StockFilter = stockFilter;
            ViewBag.DeleteFilter = deleteFilter;
 
            return View(products);
        }
 
        // View Seller Products and Stock
        public async Task<IActionResult> ViewSellerProducts(int id)
        {
            var seller = await _context.Seller.FirstOrDefaultAsync(s => s.UserId == id);
            if (seller == null)
            {
                TempData["AdminError"] = "Seller not found.";
                return RedirectToAction("ManageSellers");
            }
 
            // Fetch all products uploaded by the seller (including drafts)
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.SellerId == id)
                .OrderByDescending(p => p.ProductId)
                .ToListAsync();
 
            ViewBag.Seller = seller;
            ViewBag.ProductCount = products.Count;
            
            return View(products);
        }
 
        // Delete Product (Soft Delete)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                TempData["AdminError"] = "Product not found.";
                return RedirectToAction("InventoryReview");
            }
 
            // Soft delete
            product.IsDeleted = true;
            product.DeletedAt = DateTime.Now;
 
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
 
            TempData["AdminSuccess"] = $"Product '{product.Name}' has been deleted.";
            return RedirectToAction("InventoryReview");
        }
 
        // Restore Product (Undo Soft Delete)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                TempData["AdminError"] = "Product not found.";
                return RedirectToAction("InventoryReview");
            }
 
            // Restore
            product.IsDeleted = false;
            product.DeletedAt = null;
 
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
 
            TempData["AdminSuccess"] = $"Product '{product.Name}' has been restored.";
            return RedirectToAction("InventoryReview");
        }
 
        // System Settings
        public IActionResult Home()
        {
            return View();
        }
 
        // View Customer Reports
        public async Task<IActionResult> ViewCustomerReports(string? reportType = "all", string? status = "all", string? searchTerm = "")
        {
            IQueryable<object>? productQuery = null;
            IQueryable<object>? reviewQuery = null;
 
            if (reportType == "product" || reportType == "all")
            {
                var productReports = _context.ProductReports
                    .Include(r => r.Product)
                    .Include(r => r.Customer)
                    .AsQueryable();
 
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    productReports = productReports.Where(r =>
                        (r.Product != null && r.Product.Name.Contains(searchTerm)) ||
                        (r.Customer != null && r.Customer.FullName.Contains(searchTerm)) ||
                        r.ReportReason.Contains(searchTerm));
                }
 
                if (status != "all")
                {
                    productReports = productReports.Where(r => r.Status.ToLower() == status!.ToLower());
                }
 
                productQuery = productReports.Cast<object>();
            }
 
            if (reportType == "review" || reportType == "all")
            {
                var reviewReports = _context.ReviewReports
                    .Include(r => r.Review)
                        .ThenInclude(rev => rev.OrderItem)
                            .ThenInclude(oi => oi.Order)
                                .ThenInclude(o => o.Customer)
                    .Include(r => r.Review)
                        .ThenInclude(rev => rev.Product)
                    .Include(r => r.Customer)
                    .AsQueryable();
 
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    reviewReports = reviewReports.Where(r =>
                        (r.Review != null && r.Review.Product != null && r.Review.Product.Name.Contains(searchTerm)) ||
                        (r.Review != null && r.Review.OrderItem != null && r.Review.OrderItem.Order != null && r.Review.OrderItem.Order.Customer != null && r.Review.OrderItem.Order.Customer.FullName.Contains(searchTerm)) ||
                        r.ReportReason.Contains(searchTerm));
                }
 
                if (status != "all")
                {
                    reviewReports = reviewReports.Where(r => r.Status.ToLower() == status!.ToLower());
                }
 
                reviewQuery = reviewReports.Cast<object>();
            }
 
            // Combine both when "all", otherwise use whichever is set
            IEnumerable<object> reports;
            if (productQuery != null && reviewQuery != null)
                reports = (await productQuery.ToListAsync()).Concat(await reviewQuery.ToListAsync());
            else if (productQuery != null)
                reports = await productQuery.ToListAsync();
            else if (reviewQuery != null)
                reports = await reviewQuery.ToListAsync();
            else
                reports = await _context.ProductReports.Cast<object>().ToListAsync();
 
            ViewBag.ReportType = reportType;
            ViewBag.Status = status;
            ViewBag.SearchTerm = searchTerm;
 
            return View(reports.ToList());
        }
 
        // Resolve Report - Update status only (soft delete for reports, hard delete for reviews)
        [HttpPost]
        public async Task<IActionResult> ResolveReport([FromBody] ReportResolutionRequest request)
        {
            try
            {
                if (request.ReportType == "product")
                {
                    var report = await _context.ProductReports
                        .Include(r => r.Product)
                        .FirstOrDefaultAsync(r => r.ReportId == request.ReportId);
 
                    if (report == null)
                        return Json(new { success = false, message = "Product report not found." });
 
                    // Soft delete the product
                    if (report.Product != null)
                    {
                        report.Product.IsDeleted = true;
                        report.Product.DeletedAt = DateTime.Now;
                        _context.Products.Update(report.Product);
                        await _context.SaveChangesAsync();
                    }
 
                    // Then update report status
                    var result = await _productReportService.UpdateReportStatusAsync(request.ReportId, "Approved");
 
                    if (result)
                        return Json(new { success = true, message = "Product report approved and product deleted." });
                    else
                        return Json(new { success = false, message = "Failed to update report status." });
                }
                return Json(new { success = false, message = "Invalid report type." });
            }
            catch (ArgumentException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }
        
        // Update Report Status
        [HttpPost]
        public async Task<IActionResult> UpdateReportStatus([FromBody] UpdateReportStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NewStatus))
                    return Json(new { success = false, message = "Status cannot be empty." });
 
                if (request.ReportType == "product")
                {
                    var result = await _productReportService.UpdateReportStatusAsync(request.ReportId, request.NewStatus);
                    if (result)
                        return Json(new { success = true, message = $"Report status updated to {request.NewStatus}." });
                    else
                        return Json(new { success = false, message = "Product report not found." });
                }
                else if (request.ReportType == "review")
                {
                    var result = await _reviewReportService.UpdateReportStatusAsync(request.ReportId, request.NewStatus);
                    if (result)
                        return Json(new { success = true, message = $"Report status updated to {request.NewStatus}." });
                    else
                        return Json(new { success = false, message = "Review report not found." });
                }
 
                return Json(new { success = false, message = "Invalid report type." });
            }
            catch (ArgumentException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }
 
        // Order Management - Monitor all order flows
        public async Task<IActionResult> OrderManagement(string? statusFilter = "All", string? searchTerm = "")
        {
            var query = _context.Order
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .AsQueryable();
 
            // 1. Status filter (tab)
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                if (Enum.TryParse<OrderStatus>(statusFilter, out var parsedStatus))
                    query = query.Where(o => o.CurrentStatus == parsedStatus);
            }
 
            // 2. Keyword search (Order ID, customer name)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o =>
                    (o.Customer != null && o.Customer.FullName.Contains(searchTerm)) ||
                    o.OrderId.ToString().Contains(searchTerm));
            }
 
            var orders = await query
                .OrderByDescending(o => o.OrderTime)
                .ToListAsync();
 
            // Tab counts
            var allOrders = await _context.Order.ToListAsync();
            ViewBag.CountAll          = allOrders.Count;
            ViewBag.CountPending      = allOrders.Count(o => o.CurrentStatus == OrderStatus.PENDING);
            ViewBag.CountPreparing    = allOrders.Count(o => o.CurrentStatus == OrderStatus.PREPARING);
            ViewBag.CountShipped      = allOrders.Count(o => o.CurrentStatus == OrderStatus.SHIPPED);
            ViewBag.CountDelivered    = allOrders.Count(o => o.CurrentStatus == OrderStatus.DELIVERED);
            ViewBag.CountReceived     = allOrders.Count(o => o.CurrentStatus == OrderStatus.RECEIVED);
            ViewBag.CountCancelled    = allOrders.Count(o => o.CurrentStatus == OrderStatus.CANCELED);
            ViewBag.CountReturnRefund = allOrders.Count(o => o.CurrentStatus == OrderStatus.RETURN_REFUND);
 
            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchTerm   = searchTerm;
 
            return View(orders);
        }
    }
 
    // Request Models for Report Resolution
    public class ReportResolutionRequest
    {
        public int ReportId { get; set; }
        public string? ReportType { get; set; } // "product" or "review"
        public bool DeleteItem { get; set; }
    }
 
    public class UpdateReportStatusRequest
    {
        public int ReportId { get; set; }
        public string? ReportType { get; set; } // "product" or "review"
        public string? NewStatus { get; set; }
    }
}