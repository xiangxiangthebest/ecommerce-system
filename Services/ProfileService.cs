using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services
{
    public class ProfileService : IProfileService
    {
        private readonly AppDbContext _context;
        private readonly IProfileImageStorage _imageStorage;

        public ProfileService(AppDbContext context, IProfileImageStorage imageStorage)
        {
            _context = context;
            _imageStorage = imageStorage;
        }

        public async Task<Customer?> GetProfileAsync(int customerId)
        {
            return await _context.Users
                .OfType<Customer>()
                .Include(x => x.Addresses)
                .Include(x => x.CustomerVouchers)
                    .ThenInclude(cv => cv.Voucher)
                .FirstOrDefaultAsync(x => x.UserId == customerId);
        }

        public async Task<OperationResult<User>> UpdateProfileAsync(int customerId, Customer updated, IFormFile? profileImage)
        {
            var customer = await _context.Users
                .OfType<Customer>()
                .FirstOrDefaultAsync(x => x.UserId == customerId);

            if (customer == null) return OperationResult<User>.Fail("Customer not found.");

            var originalBirthday = customer.Birthday;

            // Update fields (business rules live here now)
            customer.FullName = updated.FullName;
            customer.Email = updated.Email;
            customer.PhoneNumber = updated.PhoneNumber;
            customer.Phone = updated.Phone;
            customer.Gender = updated.Gender;
            customer.Birthday = updated.Birthday;

            if (profileImage != null && profileImage.Length > 0)
            {
                var path = await _imageStorage.SaveProfileImageAsync(profileImage);
                customer.ProfilePicture = path;
            }

            await _context.SaveChangesAsync();

            if (originalBirthday != customer.Birthday && customer.Birthday.HasValue)
            {
                await AssignBirthdayVoucherAsync(customer);
            }

            return OperationResult<User>.Ok(customer);
        }

        private async Task AssignBirthdayVoucherAsync(Customer customer)
        {
            if (!customer.Birthday.HasValue)
                return;

            var birthday = customer.Birthday.Value;
            var now = DateTime.Now;
            var voucherYear = now.Year;

            if (birthday.Month < now.Month)
            {
                voucherYear = now.Year + 1;
            }

            var startOfMonth = new DateTime(voucherYear, birthday.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

            var existing = await _context.CustomerVouchers
                .Include(cv => cv.Voucher)
                .Where(cv => cv.CustomerId == customer.UserId)
                .Where(cv => cv.Voucher.Type == "Birthday")
                .Where(cv => cv.Voucher.StartDate.Month == startOfMonth.Month && cv.Voucher.StartDate.Year == startOfMonth.Year)
                .Where(cv => !cv.IsUsed)
                .FirstOrDefaultAsync();

            if (existing != null)
                return;

            var code = $"BIRTHDAY-{customer.UserId}-{birthday.Month:D2}{voucherYear}";
            var voucher = new Voucher
            {
                Code = code,
                Name = "Birthday voucher",
                Type = "Birthday",
                DiscountValue = 15m,
                MinimumSpend = 0m,
                StartDate = startOfMonth,
                EndDate = endOfMonth,
                Quantity = 1,
                IsActive = true
            };

            _context.Vouchers.Add(voucher);
            await _context.SaveChangesAsync();

            _context.CustomerVouchers.Add(new CustomerVoucher
            {
                CustomerId = customer.UserId,
                VoucherId = voucher.VoucherId,
                AssignedAt = DateTime.UtcNow,
                IsUsed = false
            });

            await _context.SaveChangesAsync();
        }

        public async Task<OperationResult> AddAddressAsync(int customerId, DeliveryField address)
        {
            var customer = await _context.Users
                .OfType<Customer>()
                .Include(x => x.Addresses)
                .FirstOrDefaultAsync(x => x.UserId == customerId);

            if (customer == null) return OperationResult.Fail("Customer not found.");

            if (customer.Addresses.Count >= 3)
                return OperationResult.Fail("Maximum 3 addresses allowed.");

            if (!customer.Addresses.Any())
            {
                address.IsDefault = true;
            }
            else if (address.IsDefault)
            {
                foreach (var addr in customer.Addresses)
                    addr.IsDefault = false;
            }

            address.UserId = customerId;
            address.Customer = null;

            _context.DeliveryField.Add(address);
            await _context.SaveChangesAsync();

            return OperationResult.Ok();
        }

        public async Task<OperationResult> EditAddressAsync(int customerId, DeliveryField updated)
        {
            var address = await _context.DeliveryField
                .Include(x => x.Customer!)
                    .ThenInclude(x => x.Addresses)
                .FirstOrDefaultAsync(x => x.AddressId == updated.AddressId && x.UserId == customerId);

            if (address == null) return OperationResult.Fail("Address not found.");

            address.RecipientName = updated.RecipientName;
            address.PhoneNumber = updated.PhoneNumber;
            address.AddressLine1 = updated.AddressLine1;
            address.AddressLine2 = updated.AddressLine2;
            address.City = updated.City;
            address.Postcode = updated.Postcode;
            address.State = updated.State;

            if (updated.IsDefault)
            {
                foreach (var addr in address.Customer!.Addresses)
                    addr.IsDefault = false;

                address.IsDefault = true;
            }

            await _context.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> RemoveAddressAsync(int customerId, int addressId)
        {
            var address = await _context.DeliveryField
                .FirstOrDefaultAsync(x => x.AddressId == addressId && x.UserId == customerId);

            if (address == null) return OperationResult.Fail("Address not found.");

            bool wasDefault = address.IsDefault;

            _context.DeliveryField.Remove(address);
            await _context.SaveChangesAsync();

            if (wasDefault)
            {
                var next = await _context.DeliveryField
                    .Where(x => x.UserId == customerId)
                    .OrderBy(x => x.CreatedAt)
                    .FirstOrDefaultAsync();

                if (next != null)
                {
                    next.IsDefault = true;
                    await _context.SaveChangesAsync();
                }
            }

            return OperationResult.Ok();
        }

        public async Task<OperationResult> ChangePasswordAsync(int customerId, string oldPassword, string newPassword)
        {
            var customer = await _context.Users
                .OfType<Customer>()
                .FirstOrDefaultAsync(x => x.UserId == customerId);

            if (customer == null) return OperationResult.Fail("Customer not found.");

            bool correct = BCrypt.Net.BCrypt.Verify(oldPassword, customer.PasswordHash);
            if (!correct) return OperationResult.Fail("Current password is incorrect.");

            if (oldPassword == newPassword)
                return OperationResult.Fail("New password cannot be same as current password.");

            customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            return OperationResult.Ok();
        }
    }
}