using EcommerceSystem.Enums;
using EcommerceSystem.Interfaces;
using System.ComponentModel.DataAnnotations.Schema;


namespace EcommerceSystem.Models;
public class Request
{
    public int RequestId { get; set; }

    public int CustomerId { get; set; }
    public User? Customer { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public RequestServiceType RequestServiceType { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public RequestIssueType RequestIssueType { get; set; }
    public string? RequestedItemsJson { get; set; }
    public string? ApproveItemsJson { get; set; }
    public DateTime? SolvedAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<RequestImage> Images { get; set; } = new();
    public CustomerService? ReviewByCS { get; set; }
    public int? ReviewByCsId { get; set; }
    public string Status { get; set; } = "Pending";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal? ApprovedRefundAmount { get; set; } = 0m;
    private IRequestStrategy? _strategy;
    

    public void SetRequestStrategy(IRequestStrategy strategy)
    {
        _strategy = strategy;
    }

    public void PerformSolve()
    {
        _strategy?.Solve(this);
    }
}



// Request request = new Request
// {
//     RequestIssueType = "Order Issue",
//     Description = "Received damaged item"
// };

// CustomerService cs = new CustomerService();
// IRequestStrategy strategy = new RefundStrategy();

// cs.AssignStrategy(request, strategy);

// // Step 5: Execute strategy
// request.PerformSolve();