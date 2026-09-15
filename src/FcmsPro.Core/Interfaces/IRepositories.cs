using FcmsPro.Core.Entities;

namespace FcmsPro.Core.Interfaces;

/// <summary>Generic CRUD surface implemented by FcmsPro.Data repositories over EF Core.</summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

public interface IClientRepository : IRepository<Client>
{
    Task<List<Client>> SearchAsync(string? query, CancellationToken ct = default);
}

public interface ICommissionRepository : IRepository<Commission>
{
    Task<List<Commission>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
    Task<List<Commission>> GetByStatusAsync(Enums.CommissionStatus status, CancellationToken ct = default);
}

public interface IPaymentRepository : IRepository<Payment>
{
    Task<List<Payment>> GetByCommissionIdAsync(Guid commissionId, CancellationToken ct = default);
    Task<decimal> SumForCommissionExcludingAsync(Guid commissionId, Guid? excludePaymentId, CancellationToken ct = default);
}

public interface ICommissionAttachmentRepository : IRepository<CommissionAttachment>
{
    Task<List<CommissionAttachment>> GetByCommissionIdAsync(Guid commissionId, CancellationToken ct = default);
}

public interface IClientAttachmentRepository : IRepository<ClientAttachment>
{
    Task<List<ClientAttachment>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
}

public interface IInvoiceAttachmentRepository : IRepository<InvoiceAttachment>
{
    Task<List<InvoiceAttachment>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);
}

public interface IQuoteAttachmentRepository : IRepository<QuoteAttachment>
{
    Task<List<QuoteAttachment>> GetByQuoteIdAsync(Guid quoteId, CancellationToken ct = default);
}

public interface IReceiptRepository : IRepository<Receipt>
{
    Task<Receipt?> GetByPaymentIdAsync(Guid paymentId, CancellationToken ct = default);
}

public interface IInvoiceRepository : IRepository<Invoice> { }
public interface IQuoteRepository : IRepository<Quote> { }
public interface IExpenseRepository : IRepository<Expense> { }
public interface ITemplateRepository : IRepository<CommissionTemplate> { }
public interface IAuditLogRepository : IRepository<AuditLog> { }

public interface ICounterRepository
{
    /// <summary>Atomically increments and returns the new value for the named counter.</summary>
    Task<int> NextAsync(string counterName, CancellationToken ct = default);
}

public interface ISettingsRepository
{
    Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default);
    Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default);

    Task<UiPreferences> GetUiPreferencesAsync(CancellationToken ct = default);
    Task SaveUiPreferencesAsync(UiPreferences prefs, CancellationToken ct = default);

    Task<GoalSettings> GetGoalSettingsAsync(CancellationToken ct = default);
    Task SaveGoalSettingsAsync(GoalSettings goals, CancellationToken ct = default);
}

public interface IAdminAccountRepository
{
    Task<AdminAccount?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(AdminAccount account, CancellationToken ct = default);
}

public interface ITermsRepository
{
    Task<TermsAcceptance?> GetLatestAsync(CancellationToken ct = default);
    Task AddAsync(TermsAcceptance acceptance, CancellationToken ct = default);
}

/// <summary>
/// Wraps SaveChangesAsync plus explicit transaction control, used by services
/// (e.g. PaymentService) that must combine several repository operations into
/// one atomic commit - see Phase 1 audit's note on Payment+Receipt atomicity.
/// </summary>
public interface IUnitOfWork
{
    IClientRepository Clients { get; }
    ICommissionRepository Commissions { get; }
    IPaymentRepository Payments { get; }
    ICommissionAttachmentRepository CommissionAttachments { get; }
    IClientAttachmentRepository ClientAttachments { get; }
    IInvoiceAttachmentRepository InvoiceAttachments { get; }
    IQuoteAttachmentRepository QuoteAttachments { get; }
    IReceiptRepository Receipts { get; }
    IInvoiceRepository Invoices { get; }
    IQuoteRepository Quotes { get; }
    IExpenseRepository Expenses { get; }
    ITemplateRepository Templates { get; }
    IAuditLogRepository AuditLogs { get; }
    ICounterRepository Counters { get; }
    ISettingsRepository Settings { get; }
    IAdminAccountRepository AdminAccount { get; }
    ITermsRepository Terms { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Runs the given work inside a single DB transaction; commits on success, rolls back on exception.</summary>
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);
}
