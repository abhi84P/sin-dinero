using Microsoft.EntityFrameworkCore;
using SinDinero.Data;
using SinDinero.Models;

namespace SinDinero.Services;

// Single owner of Transaction mutations. Every add/delete also adjusts the
// MonthlyCategorySummary row so the dashboard reads precomputed totals.
public class TransactionService(ApplicationDbContext db)
{
    public async Task AddAsync(Transaction tx)
    {
        db.Transactions.Add(tx);
        await ApplyDeltaAsync(tx.UserId, tx.Date.Year, tx.Date.Month, tx.CategoryId, tx.Amount);
        await db.SaveChangesAsync();
    }

    // Edit: reverse the old row's contribution, then apply the new values.
    public async Task UpdateAsync(Transaction tx, decimal oldAmount, DateOnly oldDate, int oldCategoryId)
    {
        await ApplyDeltaAsync(tx.UserId, oldDate.Year, oldDate.Month, oldCategoryId, -oldAmount);
        await ApplyDeltaAsync(tx.UserId, tx.Date.Year, tx.Date.Month, tx.CategoryId, tx.Amount);
        db.Transactions.Update(tx);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Transaction tx)
    {
        db.Transactions.Remove(tx);
        await ApplyDeltaAsync(tx.UserId, tx.Date.Year, tx.Date.Month, tx.CategoryId, -tx.Amount);
        await db.SaveChangesAsync();
    }

    // Upsert the summary row by the given signed delta.
    private async Task ApplyDeltaAsync(string userId, int year, int month, int categoryId, decimal delta)
    {
        var summary = await db.MonthlySummaries.FirstOrDefaultAsync(s =>
            s.UserId == userId && s.Year == year && s.Month == month && s.CategoryId == categoryId);

        if (summary is null)
        {
            db.MonthlySummaries.Add(new MonthlyCategorySummary
            {
                UserId = userId,
                Year = year,
                Month = month,
                CategoryId = categoryId,
                Total = delta
            });
        }
        else
        {
            summary.Total += delta;
            if (summary.Total == 0)
                db.MonthlySummaries.Remove(summary);
        }
    }

    // Backfill / repair: recompute every summary row for a user from scratch.
    public async Task RebuildAsync(string userId)
    {
        var existing = await db.MonthlySummaries.Where(s => s.UserId == userId).ToListAsync();
        db.MonthlySummaries.RemoveRange(existing);

        var rows = await db.Transactions
            .Where(t => t.UserId == userId)
            .GroupBy(t => new { t.Date.Year, t.Date.Month, t.CategoryId })
            .Select(g => new MonthlyCategorySummary
            {
                UserId = userId,
                Year = g.Key.Year,
                Month = g.Key.Month,
                CategoryId = g.Key.CategoryId,
                Total = g.Sum(t => t.Amount)
            })
            .ToListAsync();

        db.MonthlySummaries.AddRange(rows);
        await db.SaveChangesAsync();
    }
}
