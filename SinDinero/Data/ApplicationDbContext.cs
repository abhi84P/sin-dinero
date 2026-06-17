using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SinDinero.Models;

namespace SinDinero.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<MonthlyCategorySummary> MonthlySummaries => Set<MonthlyCategorySummary>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(e =>
        {
            e.HasIndex(c => new { c.UserId, c.Name }).IsUnique();
            e.Property(c => c.MonthlyLimit).HasColumnType("decimal(18,2)");
        });

        builder.Entity<Transaction>(e =>
        {
            // SQLite has no native decimal; store with fixed precision.
            e.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            e.HasIndex(t => new { t.UserId, t.Date });

            e.HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MonthlyCategorySummary>(e =>
        {
            e.Property(s => s.Total).HasColumnType("decimal(18,2)");
            // One row per user/month/category — the upsert key.
            e.HasIndex(s => new { s.UserId, s.Year, s.Month, s.CategoryId }).IsUnique();
            e.HasOne(s => s.Category)
                .WithMany()
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
