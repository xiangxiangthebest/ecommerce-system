using EcommerceSystem.Data;
using EcommerceSystem.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EcommerceSystem.Factories;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string role)
        {
            ViewBag.Role = role;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto dto, string role)
        {
            ViewBag.Role = role;

            // check user
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

            return user.Role switch
            {
                "Admin" => RedirectToAction("Home", "Admin"),
                "Customer" => RedirectToAction("Home", "Customer"),
                "Seller" => RedirectToAction("Home", "Seller"),
                "CustomerService" => RedirectToAction("Home", "CustomerService"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        [HttpGet]
        public IActionResult RegisterCustomer()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RegisterCustomer(RegisterCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            bool emailExists = await _context.Users
                .AnyAsync(x => x.Email == dto.Email);

            if (emailExists)
            {
                ModelState.AddModelError("Email", "Email already exists");
                return View(dto);
            }

            IUserFactory factory = new CustomerFactory(dto);

            var user = factory.CreateUser();

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
                
            return RedirectToAction("Login", new { role = "Customer" });
        }

        [HttpGet]
        public IActionResult RegisterSeller()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RegisterSeller(RegisterSellerDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .FirstOrDefault()?.ErrorMessage;
                return View(dto);
            }

            if (await _context.Users.AnyAsync(x => x.Email == dto.Email))
            {
                ViewBag.Error = "Email already linked to an account. <br />Please try another email.";
                return View(dto);
            }

            if (await _context.Seller.AnyAsync(x => x.ShopName == dto.ShopName))

            {
                ViewBag.Error = "This Shop Name is already taken. <br />Please try another name.";
                return View(dto);
            }

            if (await _context.Seller.AnyAsync(x => x.PhoneNumber == dto.PhoneNumber))

            {
                ViewBag.Error = "This Phone Number is already linked to an account. <br />Please try another phone number.";
                return View(dto);
            }

            if (dto.Password != dto.ConfirmPassword)
            {
                ViewBag.Error = "Passwords do not match";
                return View(dto);
            }

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

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
            
        }
    }
}