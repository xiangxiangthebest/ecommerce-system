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
                TempData["Error"] = "Seller not found.";
                return RedirectToAction("ManageSellers");
            }
 
            seller.IsApproved = true;
            await _context.SaveChangesAsync();
 
            TempData["Success"] = $"{seller.ShopName} has been approved successfully.";
            return RedirectToAction("ManageSellers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeSeller(int id)
        {
            var seller = await _context.Seller.FindAsync(id);
            if (seller == null)
            {
                TempData["Error"] = "Seller not found.";
                return RedirectToAction("ManageSellers");
            }
 
            seller.IsApproved = false;
            await _context.SaveChangesAsync();
 
            TempData["Success"] = $"{seller.ShopName}'s approval has been revoked.";
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

                TempData["Success"] = "Customer Service account created successfully!";
                return RedirectToAction("ManageCustomerService");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving: " + ex.Message);
                return View(model);
            }
        }

        // POST: Confirm and perform deletion
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

                TempData["Success"] = "Staff member deactivated successfully.";
                
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