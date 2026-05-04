using EcommerceSystem.Data;
using EcommerceSystem.DTOs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EcommerceSystem.Factories;
using EcommerceSystem.Interfaces;
using Microsoft.AspNetCore.Authentication;

namespace EcommerceSystem.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // LOGIN PAGE
        // =========================

        [HttpGet]
        public IActionResult Login(string role)
        {
            ViewBag.Role = role;

            return View();
        }

        // =========================
        // LOGIN PROCESS
        // =========================

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto dto, string role)
        {
            ViewBag.Role = role;

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == dto.Email);

            if (user == null)
            {
                ViewBag.Error = "Account does not exist";
                return View();
            }

            if (user.Role != role)
            {
                ViewBag.Error = $"This account is not a {role}";
                return View();
            }

            bool valid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);

            if (!valid)
            {
                ViewBag.Error = "Invalid password";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Updated Redirection Logic
            if (user.Role == "Seller")
            {
                return RedirectToAction("Index", "Seller"); // Sends to SellerController
            }
            else if (user.Role == "Customer")
            {
                return RedirectToAction("Index", "Customer"); // Sends to CustomerController
            }

            return RedirectToAction("Index", "Home");
        }

        // =========================
        // CUSTOMER REGISTER PAGE
        // =========================

        [HttpGet]
        public IActionResult RegisterCustomer()
        {
            return View();
        }

        // =========================
        // CUSTOMER REGISTER PROCESS
        // =========================

        [HttpPost]
        public async Task<IActionResult> RegisterCustomer(RegisterCustomerDto dto)
        {
            bool emailExists = await _context.Users
                .AnyAsync(x => x.Email == dto.Email);

            if (emailExists)
            {
                ViewBag.Error = "Email already exists";
                return View(dto);
            }

            if (dto.Password != dto.ConfirmPassword)
            {
                ViewBag.Error = "Passwords do not match";
                return View(dto);
            }

            IUserFactory factory = new CustomerFactory(dto);

            var user = factory.CreateUser();

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            return RedirectToAction("Login", new { role = "Customer" });
        }

        // =========================
        // SELLER REGISTER PAGE
        // =========================

        [HttpGet]
        public IActionResult RegisterSeller()
        {
            return View();
        }

        // =========================
        // SELLER REGISTER PROCESS
        // =========================

        [HttpPost]
        public async Task<IActionResult> RegisterSeller(RegisterSellerDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Please fill in all required fields";
                return View(dto);
            }

            // 1. Check if Email is taken
            if (await _context.Users.AnyAsync(x => x.Email == dto.Email))
            {
                ViewBag.Error = "Email already linked to an account. <br />Please try another email.";
                return View(dto);
            }

            // 2. Check if Shop Name is taken (New Check)
            if (await _context.Users.AnyAsync(x => x.ShopName == dto.ShopName))
            {
                ViewBag.Error = "This Shop Name is already taken. <br />Please try name.";
                return View(dto);
            }

            // 3. Check if Phone Number is taken (New Check)
            if (await _context.Users.AnyAsync(x => x.PhoneNumber == dto.PhoneNumber))
            {
                ViewBag.Error = "This Phone Number is already linked to an account. <br />Please try phone number.";
                return View(dto);
            }

            if (dto.Password != dto.ConfirmPassword)
            {
                ViewBag.Error = "Passwords do not match";
                return View(dto);
            }

            // If all checks pass, proceed with creation[cite: 1, 11]
            IUserFactory factory = new SellerFactory(dto);
            var user = factory.CreateUser();
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login", new { role = "Seller" });
        }

        [HttpPost]
        public async Task<IActionResult> CreateCustomerService(RegisterCustomerServiceDto dto)
        {
            IUserFactory factory = new CustomerServiceFactory(dto);

            var user = factory.CreateUser();

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // =========================
        // LOGOUT
        // =========================

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            
            // Redirect to the general Login selection page instead of home
            return RedirectToAction("Login", "Auth"); 
        }
    }
}