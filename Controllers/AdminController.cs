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

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // Main Dashboard View
        public IActionResult Index()
        {
            // In a real app, you'd fetch stats here (e.g., total users, pending sellers)
            ViewBag.TotalOrders = 150; 
            return View();
        }

        // User Management
        public IActionResult ManageUsers()
        {
            // Logic to fetch all users from the database
            return RedirectToAction("ManageSellers");
        }

        public async Task<IActionResult> ManageSellers()
        {
            // Fetch all sellers in one list
            var sellers = await _context.Seller.ToListAsync();
            return View(sellers);
        }

        public IActionResult ManageCustomerService()
        {
            var staffList = _context.CustomerServices.ToList(); // Or your logic
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
            // Logic to set product status to 'Approved'
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

            // 1. Check if the email already exists
            var existingUser = await _context.Users.AnyAsync(u => u.Email == model.Email);
            if (existingUser)
            {
                ModelState.AddModelError("Email", "This email address is already in use.");
                return View(model);
            }

            // 2. Use your new CustomerServiceCreator
            // This utilizes the logic in your uploaded CustomerServiceCreator.cs
            UserCreator creator = new CustomerServiceCreator(model); 
            User customerServiceAccount = creator.CreateUser();

            // 3. Save to database via DbContext
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

            // Fix: Use UserId to verify email isn't taken by a different user
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
                // Instead of _context.Users.Remove(user);
                // We update the status. For example:
                user.IsActive = false; 
                
                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["AdminSuccess"] = "Staff member deactivated successfully.";
                
                // Redirect back to the list so you stay on the same page
                return RedirectToAction("ManageCustomerService");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deactivating account: " + ex.Message;
                return RedirectToAction("ManageCustomerService");
            }
        }
        // System Settings
        public IActionResult Home()
        {
            return View();
        }
    }
}