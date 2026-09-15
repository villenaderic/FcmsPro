using FcmsPro.Core.Interfaces;
using FcmsPro.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FcmsPro.Data;

public class UnitOfWork : IUnitOfWork, IAsyncDisposable
{
    private readonly FcmsDbContext _db;

    public UnitOfWork(FcmsDbContext db)
    {
        _db = db;
        Clients = new ClientRepository(db);
        Commissions = new CommissionRepository(db);
        Payments = new PaymentRepository(db);
        CommissionAttachments = new CommissionAttachmentRepository(db);
        ClientAttachments = new ClientAttachmentRepository(db);
        InvoiceAttachments = new InvoiceAttachmentRepository(db);
        QuoteAttachments = new QuoteAttachmentRepository(db);
        Receipts = new ReceiptRepository(db);
        Invoices = new InvoiceRepository(db);
        Quotes = new QuoteRepository(db);
        Expenses = new ExpenseRepository(db);
        Templates = new TemplateRepository(db);
        AuditLogs = new AuditLogRepository(db);
        Counters = new CounterRepository(db);
        Settings = new SettingsRepository(db);
        AdminAccount = new AdminAccountRepository(db);
        Terms = new TermsRepository(db);
    }

    public IClientRepository Clients { get; }
    public ICommissionRepository Commissions { get; }
    public IPaymentRepository Payments { get; }
    public ICommissionAttachmentRepository CommissionAttachments { get; }
    public IClientAttachmentRepository ClientAttachments { get; }
    public IInvoiceAttachmentRepository InvoiceAttachments { get; }
    public IQuoteAttachmentRepository QuoteAttachments { get; }
    public IReceiptRepository Receipts { get; }
    public IInvoiceRepository Invoices { get; }
    public IQuoteRepository Quotes { get; }
    public IExpenseRepository Expenses { get; }
    public ITemplateRepository Templates { get; }
    public IAuditLogRepository AuditLogs { get; }
    public ICounterRepository Counters { get; }
    public ISettingsRepository Settings { get; }
    public IAdminAccountRepository AdminAccount { get; }
    public ITermsRepository Terms { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        // Single-user desktop app: no real contention, but we still want the
        // insert-then-recompute sequences (e.g. Payment+Receipt, Refund+Recalc)
        // to be atomic against a mid-operation crash. SQLite supports nested
        // savepoints via EF Core's execution strategy, so this is safe even if
        // called from within another transaction scope.
        //
        // Explicitly typed as Func<Task> below (rather than a bare lambda) to
        // avoid the compiler picking EF Core's generic Func<DbContext, TState,
        // CancellationToken, Task<TResult>> overload instead of the simple
        // Func<Task> one.
        var strategy = _db.Database.CreateExecutionStrategy();
        Func<Task> operation = async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await work();
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        };
        await strategy.ExecuteAsync(operation);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
