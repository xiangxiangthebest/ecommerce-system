namespace EcommerceSystem.StrategyPattern;
using EcommerceSystem.Enums;
using EcommerceSystem.Models;

public class Request
{
     public User UserId { get; set; }
    // public List<Order>? Order { get; set; } = new();
    public Order? Order { get; set; } = new();
    public User? ReportedUser { get; set; }
    public AfterSaleServiceType? AfterSaleServiceType { get; set; }
    public RequestIssueType RequestIssueType { get; set; }
    public string Description { get; set; }
    public string AttachmentFile { get; set; }
    public string ReviewedBy { get; set; }
    public string Result { get; set; }
    private IRequestStrategy _strategy;

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

