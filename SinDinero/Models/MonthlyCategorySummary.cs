namespace SinDinero.Models;

// Precomputed aggregate: total amount per user / month / category.
// Maintained incrementally by TransactionService so the dashboard never has
// to GROUP BY the full Transactions table.
public class MonthlyCategorySummary
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int Year { get; set; }
    public int Month { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public decimal Total { get; set; }
}
