using EcommerceSystem.Data;
using EcommerceSystem.DTOs;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace EcommerceSystem.Controllers
{
    public class AuthController : Controller
    {
        private readonly IUserService _userService;
        private readonly AppDbContext _context;  // ← ADD THIS

        public AuthController(IUserService userService, AppDbContext context)  // ← ADD context
        {
            _userService = userService;
            _context = context;  // ← ADD THIS
        }

        [HttpGet]
        public IActionResult Login(string role)
        {
            ViewBag.Role = role;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto dto, string role)
        {
            var user = await _userService.LoginAsync(dto, role);
            
            if (user == null)
            {
                ViewBag.Role = role;
                ViewBag.Error = "Invalid credentials";
                return View(dto);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            if (user.Role == "Customer")
                return RedirectToAction("Home", "Customer");

            if (user.Role == "Seller")
                return RedirectToAction("Home", "Seller");

            if (user.Role == "CustomerService")
                return RedirectToAction("Home", "CustomerService");

            if (user.Role == "Admin")
                return RedirectToAction("Home", "Admin");

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult RegisterCustomer()
        {
            ViewBag.Role = "Customer";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCustomer(RegisterCustomerDto model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Role = "Customer";
                return View(model);
            }

            await _userService.RegisterCustomerAsync(model);

            return RedirectToAction("Login", new { role = "Customer" });
        }

        [HttpGet]
        public IActionResult RegisterSeller()
        {
            ViewBag.Role = "Seller";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterSeller(RegisterSellerDto model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Role = "Seller";
                return View(model);
            }

            // ── 1. Email uniqueness — checked across ALL user types ──────────
            // If a@gmail.com is used by a Customer, Seller, or CustomerService,
            // no one else can register with that same email.
            bool emailTaken =
                await _context.Users.AnyAsync(u => u.Email == model.Email) ||
                await _context.Seller.AnyAsync(s => s.Email == model.Email);

            if (emailTaken)
            {
                TempData["RegisterError"] = "This Email Address is already registered. Please use a different email.";
                ViewBag.Role = "Seller";
                ViewBag.StartAtStep = 2;
                return View(model);
            }

            // ── 2. Shop name uniqueness ──────────────────────────────────────
            bool shopNameTaken = await _context.Seller
                .AnyAsync(s => s.ShopName == model.ShopName);

            if (shopNameTaken)
            {
                TempData["RegisterError"] = "This Shop Name is already taken. Please pick a unique name.";
                ViewBag.Role = "Seller";
                ViewBag.StartAtStep = 2;
                return View(model);
            }

            // ── 3. Contact number uniqueness ─────────────────────────────────
            bool contactTaken = await _context.Seller
                .AnyAsync(s => s.PhoneNumber == model.PhoneNumber);

            if (contactTaken)
            {
                TempData["RegisterError"] = "This Contact Number is already registered. Please use a different number.";
                ViewBag.Role = "Seller";
                ViewBag.StartAtStep = 2;
                return View(model);
            }

            // ── All checks passed — proceed with registration ─────────────────
            await _userService.RegisterSellerAsync(model);

            return RedirectToAction("Login", new { role = "Seller" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}