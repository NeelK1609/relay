using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Relay.Domain;

namespace Relay.Crm.Sql;

public sealed class RelayDbContext(DbContextOptions<RelayDbContext> options) : DbContext(options)
{
    public DbSet<ServiceAccount> Accounts => Set<ServiceAccount>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<SupportCase> Cases => Set<SupportCase>();
    public DbSet<CaseMessage> CaseMessages => Set<CaseMessage>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<DemoState> DemoState => Set<DemoState>();
    public DbSet<ChatSession> Chats => Set<ChatSession>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasSequence<int>("CaseNumbers").StartsAt(1001);

        b.Entity<ServiceAccount>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.AccountNumber).IsUnique();
            e.Property(x => x.Balance).HasPrecision(12, 2);
        });

        b.Entity<Customer>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId);
        });

        b.Entity<Staff>(e => e.Property(x => x.Id).ValueGeneratedNever());

        b.Entity<SupportCase>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Number).HasDefaultValueSql("NEXT VALUE FOR CaseNumbers").ValueGeneratedOnAdd();
            e.HasIndex(x => x.Number).IsUnique();
            // Stored as int so ORDER BY ranks Urgent above Low.
            e.Property(x => x.Priority).HasConversion<int>();
            e.HasIndex(x => new { x.Status, x.Priority });
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.AssignedTo).WithMany().HasForeignKey(x => x.AssignedStaffId).OnDelete(DeleteBehavior.SetNull);
            e.Property<byte[]>("Version").IsRowVersion();
            e.ComplexProperty(x => x.Insights, i =>
            {
                i.Property(x => x.Summary).HasMaxLength(1000);
                i.Property(x => x.Reason).HasMaxLength(1000);
            });
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(m => m.CaseId);
            e.Navigation(x => x.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<CaseMessage>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Body).HasMaxLength(8000);
            e.HasIndex(x => new { x.CaseId, x.CreatedAt });
        });

        b.Entity<KnowledgeArticle>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Body).HasMaxLength(8000);
        });

        b.Entity<ChatSession>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property<byte[]>("Version").IsRowVersion();
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<SupportCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => x.CaseId);
        });
    }
}

/// <summary>When the demo data was last loaded, for the nightly reset.</summary>
public sealed class DemoState
{
    public int Id { get; set; }
    public DateTimeOffset SeededAt { get; set; }
}

public sealed class RelayDbContextDesignFactory : IDesignTimeDbContextFactory<RelayDbContext>
{
    public RelayDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<RelayDbContext>()
            .UseSqlServer("Server=localhost;Database=relaydb;Integrated Security=false")
            .Options);
}
