using EcommerceSystem.Enums;
using EcommerceSystem.Interfaces;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceSystem.Models;
public class Request
{
    public int RequestId { get; set; }

    // who created request
    public int RequestUserId { get; set; }
    public User? RequestUser { get; set; }

    // optional: order context
    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    // optional: report another user
    public int? ReportedUserId { get; set; }
    public User? ReportedUser { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public RequestServiceType RequestServiceType { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public RequestIssueType RequestIssueType { get; set; }
    public string? RequestedItemsJson { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<RequestImage> Images { get; set; } = new();
    // public string? ReviewedBy { get; set; }
    public CustomerService? ReviewByCS { get; set; }
    public int? ReviewByCsId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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

