using System.ComponentModel.DataAnnotations;

namespace SinDinero.Models;

public class Category
{
    public int Id { get; set; }

    [Required]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    public TransactionType Type { get; set; } = TransactionType.Expense;

    // Optional monthly spending cap (expense categories). Null = no limit.
    [Range(0, 1_000_000_000)]
    public decimal? MonthlyLimit { get; set; }

    // Owner — Identity user id. Set server-side, not a form field, so no
    // [Required] (would fail form validation before the code assigns it).
    public string UserId { get; set; } = string.Empty;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
