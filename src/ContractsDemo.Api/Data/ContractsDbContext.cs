using Microsoft.EntityFrameworkCore;

namespace ContractsDemo.Api.Data;

public class ContractsDbContext : DbContext
{
    public ContractsDbContext(DbContextOptions<ContractsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Contract> Contracts => Set<Contract>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Contract>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.ContractType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Value).HasPrecision(18, 2);
            entity.Property(e => e.SalesforceAccountId).HasMaxLength(18).IsRequired();
            entity.Property(e => e.SalesforceContactId).HasMaxLength(18).IsRequired();
            entity.Property(e => e.AccountName).HasMaxLength(200);
            entity.Property(e => e.ContactName).HasMaxLength(200);
            entity.Property(e => e.ContactEmail).HasMaxLength(200);
            entity.Property(e => e.ValidationMessage).HasMaxLength(500);
            entity.Property(e => e.SignedBy).HasMaxLength(200);

            entity.HasIndex(e => e.SalesforceAccountId);
            entity.HasIndex(e => e.Status);
        });
    }
}
