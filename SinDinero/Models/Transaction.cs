using System.ComponentModel.DataAnnotations;

namespace SinDinero.Models;

public class Transaction
{
    public int Id { get; set; }

    // Always stored positive. Income vs expense comes from the Category type.
    [Range(0.01, 1_000_000_000)]
    public decimal Amount { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(200)]
    public string? Note { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    // Owner — Identity user id. Denormalized for fast per-user queries.
    // Set server-side, not a form field, so no [Required] (would fail form
    // validation before the code assigns it).
    public string UserId { get; set; } = string.Empty;
}
