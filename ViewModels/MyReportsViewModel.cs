using EcommerceSystem.Models;

namespace EcommerceSystem.Models.ViewModels
{
    public class MyReportsViewModel
    {
        public List<ProductReport> ProductReports { get; set; } = new();
        public List<ReviewReport> ReviewReports { get; set; } = new();
    }
}
