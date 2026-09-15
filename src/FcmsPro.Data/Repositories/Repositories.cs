using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FcmsPro.Data.Repositories;

public class EfRepository<T> : IRepository<T> where T : class
{
    protected readonly FcmsDbContext Db;
    protected readonly DbSet<T> Set;

    public EfRepository(FcmsDbContext db)
    {
        Db = db;
        Set = db.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Set.FindAsync(new object?[] { id }, ct);

    public async Task<List<T>> GetAllAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    /// <summary>
    /// Attaches entity as Modified. Confirmed via crash log this was throwing
    /// "The instance of entity type 'X' cannot be tracked because another
    /// instance with the same key value... is already being tracked" for
    /// both Invoice (InvoiceService.MarkPaidAsync) and Commission
    /// (CommissionService.UpdateAsync) - same root cause, same fix, since
    /// both go through this one method.
    ///
    /// Root cause: IUnitOfWork/FcmsDbContext lives for the whole app session
    /// (DI-scoped per window, not per operation), so its change tracker
    /// accumulates entries across actions. A plain Set.Update(entity) call
    /// on a freshly-loaded/AsNoTracking instance collides with an older,
    /// already-tracked instance for the same row's Id that's still sitting
    /// in the tracker from an earlier Update()/SaveChangesAsync in this same
    /// context - EF's identity map only allows one live tracked instance per
    /// key. Fix: before attaching, detach any stale tracked instance with a
    /// matching key so the identity map has room for the new one.
    /// </summary>
    public void Update(T entity)
    {
        var entry = Db.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            var keyProps = entry.Metadata.FindPrimaryKey()!.Properties;
            var keyValues = keyProps.Select(p => entry.Property(p.Name).CurrentValue).ToArray();

            var stale = Db.ChangeTracker.Entries<T>().FirstOrDefault(e =>
                !ReferenceEquals(e.Entity, entity) &&
                keyProps.Select(p => e.Property(p.Name).CurrentValue).SequenceEqual(keyValues));

            if (stale is not null)
                stale.State = EntityState.Detached;
        }

        Set.Update(entity);
    }

    public void Remove(T entity) => Set.Remove(entity);
}

public class ClientRepository : EfRepository<Client>, IClientRepository
{
    public ClientRepository(FcmsDbContext db) : base(db) { }

    public async Task<List<Client>> SearchAsync(string? query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync(ct);

        var q = query.Trim().ToLower();
        return await Set.AsNoTracking()
            .Where(c => c.Name.ToLower().Contains(q)
                     || (c.Email != null && c.Email.ToLower().Contains(q))
                     || (c.Phone != null && c.Phone.Contains(q)))
            .ToListAsync(ct);
    }
}

public class CommissionRepository : EfRepository<Commission>, ICommissionRepository
{
    public CommissionRepository(FcmsDbContext db) : base(db) { }

    public async Task<List<Commission>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(c => c.ClientId == clientId).ToListAsync(ct);

    public async Task<List<Commission>> GetByStatusAsync(CommissionStatus status, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(c => c.Status == status).ToListAsync(ct);
}

public class PaymentRepository : EfRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(FcmsDbContext db) : base(db) { }

    public async Task<List<Payment>> GetByCommissionIdAsync(Guid commissionId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(p => p.CommissionId == commissionId && !p.IsDeleted).ToListAsync(ct);

    public async Task<decimal> SumForCommissionExcludingAsync(Guid commissionId, Guid? excludePaymentId, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().Where(p => p.CommissionId == commissionId && !p.IsDeleted);
        if (excludePaymentId.HasValue)
            query = query.Where(p => p.Id != excludePaymentId.Value);

        // SQLite's EF Core provider cannot translate Sum() on a decimal
        // column into SQL ("SQLite cannot apply aggregate operator 'Sum' on
        // expressions of type 'decimal'") - same category of provider
        // limitation as the earlier DateTimeOffset ORDER BY issue (SQLite
        // has no native DECIMAL type, and the decimal<->TEXT/REAL mapping
        // doesn't support server-side aggregate translation). Confirmed via
        // crash log: this was breaking BOTH recording a payment
        // (PaymentService.RecordPaymentAsync) and changing a commission's
        // status via the row dropdown (CommissionService.UpdateAsync), since
        // both call this method. A commission's payment list is always
        // small (one freelancer's own records), so materializing first and
        // summing client-side has no meaningful performance cost.
        var amounts = await query.Select(p => p.Amount).ToListAsync(ct);
        return amounts.Sum();
    }
}

public class CommissionAttachmentRepository : EfRepository<CommissionAttachment>, ICommissionAttachmentRepository
{
    public CommissionAttachmentRepository(FcmsDbContext db) : base(db) { }

    public async Task<List<CommissionAttachment>> GetByCommissionIdAsync(Guid commissionId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(a => a.CommissionId == commissionId)
            .OrderByDescending(a => a.UploadedAt).ToListAsync(ct);
}

public class ClientAttachmentRepository : EfRepository<ClientAttachment>, IClientAttachmentRepository
{
    public ClientAttachmentRepository(FcmsDbContext db) : base(db) { }

    public async Task<List<ClientAttachment>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.UploadedAt).ToListAsync(ct);
}

public class InvoiceAttachmentRepository : EfRepository<InvoiceAttachment>, IInvoiceAttachmentRepository
{
    public InvoiceAttachmentRepository(FcmsDbContext db) : base(db) { }

    public async Task<List<InvoiceAttachment>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(a => a.InvoiceId == invoiceId)
            .OrderByDescending(a => a.UploadedAt).ToListAsync(ct);
}

public class QuoteAttachmentRepository : EfRepository<QuoteAttachment>, IQuoteAttachmentRepository
{
    public QuoteAttachmentRepository(FcmsDbContext db) : base(db) { }

    public async Task<List<QuoteAttachment>> GetByQuoteIdAsync(Guid quoteId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(a => a.QuoteId == quoteId)
            .OrderByDescending(a => a.UploadedAt).ToListAsync(ct);
}

public class ReceiptRepository : EfRepository<Receipt>, IReceiptRepository
{
    public ReceiptRepository(FcmsDbContext db) : base(db) { }

    public async Task<Receipt?> GetByPaymentIdAsync(Guid paymentId, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(r => r.PaymentId == paymentId, ct);
}

public class InvoiceRepository : EfRepository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(FcmsDbContext db) : base(db) { }
}

public class QuoteRepository : EfRepository<Quote>, IQuoteRepository
{
    public QuoteRepository(FcmsDbContext db) : base(db) { }
}

public class ExpenseRepository : EfRepository<Expense>, IExpenseRepository
{
    public ExpenseRepository(FcmsDbContext db) : base(db) { }
}

public class TemplateRepository : EfRepository<CommissionTemplate>, ITemplateRepository
{
    public TemplateRepository(FcmsDbContext db) : base(db) { }
}

public class AuditLogRepository : EfRepository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(FcmsDbContext db) : base(db) { }
}

public class CounterRepository : ICounterRepository
{
    private readonly FcmsDbContext _db;
    public CounterRepository(FcmsDbContext db) => _db = db;

    public async Task<int> NextAsync(string counterName, CancellationToken ct = default)
    {
        var counter = await _db.Counters.FindAsync(new object?[] { counterName }, ct);
        if (counter is null)
        {
            counter = new Counter { Name = counterName, Value = 0 };
            _db.Counters.Add(counter);
        }
        counter.Value += 1;
        // Caller (via IUnitOfWork.SaveChangesAsync / ExecuteInTransactionAsync) persists this.
        return counter.Value;
    }
}

public class SettingsRepository : ISettingsRepository
{
    private readonly FcmsDbContext _db;
    public SettingsRepository(FcmsDbContext db) => _db = db;

    public async Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default) =>
        await _db.AppSettings.FindAsync(new object?[] { 1 }, ct)
            ?? new AppSettings { Id = 1 };

    public async Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        settings.Id = 1;
        var existing = await _db.AppSettings.FindAsync(new object?[] { 1 }, ct);
        if (existing is null)
            _db.AppSettings.Add(settings);
        else
            _db.Entry(existing).CurrentValues.SetValues(settings);
    }

    public async Task<UiPreferences> GetUiPreferencesAsync(CancellationToken ct = default) =>
        await _db.UiPreferences.FindAsync(new object?[] { 1 }, ct)
            ?? new UiPreferences { Id = 1 };

    public async Task SaveUiPreferencesAsync(UiPreferences prefs, CancellationToken ct = default)
    {
        prefs.Id = 1;
        var existing = await _db.UiPreferences.FindAsync(new object?[] { 1 }, ct);
        if (existing is null)
            _db.UiPreferences.Add(prefs);
        else
            _db.Entry(existing).CurrentValues.SetValues(prefs);
    }

    public async Task<GoalSettings> GetGoalSettingsAsync(CancellationToken ct = default) =>
        await _db.GoalSettings.FindAsync(new object?[] { 1 }, ct)
            ?? new GoalSettings { Id = 1 };

    public async Task SaveGoalSettingsAsync(GoalSettings goals, CancellationToken ct = default)
    {
        goals.Id = 1;
        var existing = await _db.GoalSettings.FindAsync(new object?[] { 1 }, ct);
        if (existing is null)
            _db.GoalSettings.Add(goals);
        else
            _db.Entry(existing).CurrentValues.SetValues(goals);
    }
}

public class AdminAccountRepository : IAdminAccountRepository
{
    private readonly FcmsDbContext _db;
    public AdminAccountRepository(FcmsDbContext db) => _db = db;

    public async Task<AdminAccount?> GetAsync(CancellationToken ct = default) =>
        await _db.AdminAccounts.FindAsync(new object?[] { "admin" }, ct);

    public async Task SaveAsync(AdminAccount account, CancellationToken ct = default)
    {
        var existing = await _db.AdminAccounts.FindAsync(new object?[] { account.Id }, ct);
        if (existing is null)
            _db.AdminAccounts.Add(account);
        else
            _db.Entry(existing).CurrentValues.SetValues(account);
    }
}

public class TermsRepository : ITermsRepository
{
    private readonly FcmsDbContext _db;
    public TermsRepository(FcmsDbContext db) => _db = db;

    public async Task<TermsAcceptance?> GetLatestAsync(CancellationToken ct = default)
    {
        // SQLite's EF Core provider can't translate ORDER BY on a
        // DateTimeOffset column into SQL ("SQLite does not support
        // expressions of type 'DateTimeOffset' in ORDER BY clauses") - this
        // table only ever has a handful of rows (one per terms-version
        // acceptance event), so materializing first and ordering client-side
        // (as the exception message itself suggests) has no real cost.
        var all = await _db.TermsAcceptances.AsNoTracking().ToListAsync(ct);
        return all.OrderByDescending(t => t.AcceptedAt).FirstOrDefault();
    }

    public async Task AddAsync(TermsAcceptance acceptance, CancellationToken ct = default) =>
        await _db.TermsAcceptances.AddAsync(acceptance, ct);
}
