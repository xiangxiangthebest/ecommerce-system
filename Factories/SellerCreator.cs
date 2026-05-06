using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Factories
{
    public class SellerCreator : UserCreator
    {
        private readonly RegisterSellerDto _dto;

        public SellerCreator(RegisterSellerDto dto)
        {
            _dto = dto;
        }

        public override User CreateUser()
        {
            return new Seller
            {
                FullName = _dto.FullName,
                NRICNumber = _dto.NRICNumber,
                State = _dto.State,
                PostalCode = _dto.PostalCode,
                DetailAddress = _dto.DetailAddress,
                TIN = _dto.TIN,
                ShopName = _dto.ShopName,
                PickupAddress = _dto.PickupAddress,
                Email = _dto.Email,
                PhoneNumber = _dto.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(_dto.Password),
                Role = "Seller"
            };
        }
    }
}