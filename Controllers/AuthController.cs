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
        private readonly IUserService _userService;

        public AuthController(IUserService userService)
        {
            _userService = userService;
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