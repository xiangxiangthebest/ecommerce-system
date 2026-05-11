using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Data;
using EcommerceSystem.Models;
using System.Security.Claims;

namespace EcommerceSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerController : Controller
    {
        private readonly AppDbContext _context;

        public CustomerController(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // HOME PAGE
        // =========================
        public IActionResult Home()
        {
            ViewBag.Category = _context.Category.ToList();

            var products = _context.Products.ToList();

            return View(products);
        }

        // =========================
        // PROFILE PAGE
        // =========================
        public async Task<IActionResult> Profile()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            var customer = await _context.Users
                .OfType<Customer>()
                .FirstOrDefaultAsync(x => x.Email == email);

            if (customer == null)
                return NotFound();

            return View(customer);
        }

        // =========================
        // UPDATE PROFILE
        // =========================
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(Customer model, IFormFile profileImage)
        {
            var customer = await _context.Users
                .OfType<Customer>()
                .FirstOrDefaultAsync(x => x.UserId == model.UserId);

            if (customer == null)
                return NotFound();

            customer.FullName = model.FullName;
            customer.Email = model.Email;
            customer.Phone = model.Phone;
            customer.Address = model.Address;
            customer.Gender = model.Gender;
            customer.Birthday = model.Birthday;

            if (profileImage != null && profileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profile");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid() + Path.GetExtension(profileImage.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(stream);
                }

                customer.ProfilePicture = "/images/profile/" + fileName;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Profile");
        }
    }
}