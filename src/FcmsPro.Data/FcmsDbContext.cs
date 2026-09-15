using FcmsPro.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FcmsPro.Data;

public class FcmsDbContext : DbContext
{
    public FcmsDbContext(DbContextOptions<FcmsDbContext> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Commission> Commissions => Set<Commission>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<CommissionTemplate> Templates => Set<CommissionTemplate>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<UiPreferences> UiPreferences => Set<UiPreferences>();
    public DbSet<GoalSettings> GoalSettings => Set<GoalSettings>();
    public DbSet<AdminAccount> AdminAccounts => Set<AdminAccount>();
    public DbSet<TermsAcceptance> TermsAcceptances => Set<TermsAcceptance>();
    public DbSet<CommissionAttachment> CommissionAttachments => Set<CommissionAttachment>();
    public DbSet<ClientAttachment> ClientAttachments => Set<ClientAttachment>();
    public DbSet<InvoiceAttachment> InvoiceAttachments => Set<InvoiceAttachment>();
    public DbSet<QuoteAttachment> QuoteAttachments => Set<QuoteAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Client
        modelBuilder.Entity<Client>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.DateAdded);
        });

        // Commission - ClientId is a SOFT reference (no FK constraint), matching
        // PWA orphan-on-delete behavior. See Phase 1 audit §4.2 / Phase 2 §2.
        modelBuilder.Entity<Commission>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired();
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.Property(x => x.DownPayment).HasColumnType("decimal(18,2)");
            e.Property(x => x.Remaining).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.ClientId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Deadline);
            e.HasIndex(x => x.DateAdded);
            // No .HasOne(...).WithMany() configured intentionally - soft reference only.
        });

        // Payment - CommissionId/ClientId are soft references, same reasoning.
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.CommissionId);
            e.HasIndex(x => x.ClientId);
            e.HasIndex(x => x.Date);
        });

        modelBuilder.Entity<Receipt>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PaymentId).IsUnique();
            e.HasIndex(x => x.ReceiptNumber).IsUnique();
            e.Property(x => x.CommissionPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.DownPayment).HasColumnType("decimal(18,2)");
            e.Property(x => x.PreviousPayments).HasColumnType("decimal(18,2)");
            e.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
            e.Property(x => x.RemainingBalance).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasIndex(x => x.ClientId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DueDate);
            e.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(x => x.Discount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Tax).HasColumnType("decimal(18,2)");
            e.Property(x => x.Total).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Quote>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.QuoteNumber).IsUnique();
            e.HasIndex(x => x.ClientId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Total).HasColumnType("decimal(18,2)");
            e.Property(x => x.DownPayment).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Expense>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.Category);
            e.HasIndex(x => x.NextOccurrence);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<CommissionTemplate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.Property(x => x.DownPayment).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Timestamp);
        });

        modelBuilder.Entity<Counter>(e => e.HasKey(x => x.Name));

        modelBuilder.Entity<AppSettings>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<UiPreferences>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<GoalSettings>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<AdminAccount>(e => e.HasKey(x => x.Id));

        modelBuilder.Entity<TermsAcceptance>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AcceptedAt);
        });

        // CommissionAttachment - CommissionId is a soft reference, same
        // reasoning as Payment/Commission above. FileSizeBytes is a plain
        // long (no special column type needed for SQLite's INTEGER affinity).
        modelBuilder.Entity<CommissionAttachment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CommissionId);
        });

        // ClientAttachment/InvoiceAttachment/QuoteAttachment - same
        // soft-reference-by-Id, no-FK-constraint pattern as
        // CommissionAttachment above (see that entity's own comment).
        modelBuilder.Entity<ClientAttachment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ClientId);
        });

        modelBuilder.Entity<InvoiceAttachment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.InvoiceId);
        });

        modelBuilder.Entity<QuoteAttachment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.QuoteId);
        });
    }
}
