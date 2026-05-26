using EcommerceSystem.Data;
using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using EcommerceSystem.Factories;
using Microsoft.EntityFrameworkCore;

public class UserService : IUserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> LoginAsync(LoginDto dto, string role)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Email == dto.Email);

        if (user == null || !user.IsActive || user.Role != role)
            return null;

        bool valid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        return valid ? user : null;
    }

    public async Task<bool> RegisterCustomerAsync(RegisterCustomerDto dto)
    {
        if (await _context.Users.AnyAsync(x => x.Email == dto.Email))
            return false;

        UserCreator creator = new CustomerCreator(dto);
        var user = creator.CreateUser();

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RegisterSellerAsync(RegisterSellerDto dto)
    {
        if (await _context.Users.AnyAsync(x => x.Email == dto.Email))
            return false;

        UserCreator creator = new SellerCreator(dto);
        var user = creator.CreateUser();

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return true;
    }
}