using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

public enum TrashItemType { Client, Commission, Payment, Expense, Quote, Invoice }

public record TrashItem(TrashItemType Type, Guid Id, string DisplayName, string Detail, DateTimeOffset DeletedAt);

/// <summary>
/// Unified restore/permanently-delete/purge over the 6 entities that
/// implement ISoftDeletable (Client, Commission, Payment, Expense, Quote,
/// Invoice). Each entity type still goes through its own strongly-typed
/// repository (IUnitOfWork.Clients, .Commissions, etc.) - this service just
/// presents them as one flat list for the Trash page rather than requiring
/// six separate UI sections.
///
/// "Delete" in every list ViewModel now means setting IsDeleted=true /
/// DeletedAt=now (soft delete) - this service is what actually removes a
/// row from the database, either explicitly (PermanentlyDeleteAsync) or
/// automatically once 30 days have passed (PurgeExpiredAsync, called once
/// at app startup).
/// </summary>
public class TrashService
{
    private readonly IUnitOfWork _uow;
    private const int RetentionDays = 30;

    public TrashService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<TrashItem>> GetAllAsync(CancellationToken ct = default)
    {
        var settings = await _uow.Settings.GetAppSettingsAsync(ct);
        var symbol = settings.CurrencySymbol;
        var items = new List<TrashItem>();

        var clients = await _uow.Clients.GetAllAsync(ct);
        items.AddRange(clients.Where(c => c.IsDeleted)
            .Select(c => new TrashItem(TrashItemType.Client, c.Id, c.Name, c.Email ?? c.Phone ?? "", c.DeletedAt ?? c.UpdatedAt)));

        var commissions = await _uow.Commissions.GetAllAsync(ct);
        items.AddRange(commissions.Where(c => c.IsDeleted)
            .Select(c => new TrashItem(TrashItemType.Commission, c.Id, c.Title, $"{symbol}{c.Price:N2}", c.DeletedAt ?? c.UpdatedAt)));

        var payments = await _uow.Payments.GetAllAsync(ct);
        items.AddRange(payments.Where(p => p.IsDeleted)
            .Select(p => new TrashItem(TrashItemType.Payment, p.Id, $"{symbol}{p.Amount:N2} payment", p.Date.ToString(), p.DeletedAt ?? p.CreatedAt)));

        var expenses = await _uow.Expenses.GetAllAsync(ct);
        items.AddRange(expenses.Where(e => e.IsDeleted)
            .Select(e => new TrashItem(TrashItemType.Expense, e.Id, e.Description, $"{symbol}{e.Amount:N2}", e.DeletedAt ?? e.UpdatedAt)));

        var quotes = await _uow.Quotes.GetAllAsync(ct);
        items.AddRange(quotes.Where(q => q.IsDeleted)
            .Select(q => new TrashItem(TrashItemType.Quote, q.Id, q.QuoteNumber, $"{symbol}{q.Total:N2}", q.DeletedAt ?? q.UpdatedAt)));

        var invoices = await _uow.Invoices.GetAllAsync(ct);
        items.AddRange(invoices.Where(i => i.IsDeleted)
            .Select(i => new TrashItem(TrashItemType.Invoice, i.Id, i.InvoiceNumber, $"{symbol}{i.Total:N2}", i.DeletedAt ?? i.UpdatedAt)));

        return items.OrderByDescending(i => i.DeletedAt).ToList();
    }

    public async Task RestoreAsync(TrashItemType type, Guid id, CancellationToken ct = default)
    {
        switch (type)
        {
            case TrashItemType.Client:
                await RestoreEntityAsync(_uow.Clients, id, ct);
                break;
            case TrashItemType.Commission:
                await RestoreEntityAsync(_uow.Commissions, id, ct);
                break;
            case TrashItemType.Payment:
                await RestoreEntityAsync(_uow.Payments, id, ct);
                break;
            case TrashItemType.Expense:
                await RestoreEntityAsync(_uow.Expenses, id, ct);
                break;
            case TrashItemType.Quote:
                await RestoreEntityAsync(_uow.Quotes, id, ct);
                break;
            case TrashItemType.Invoice:
                await RestoreEntityAsync(_uow.Invoices, id, ct);
                break;
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task PermanentlyDeleteAsync(TrashItemType type, Guid id, CancellationToken ct = default)
    {
        switch (type)
        {
            case TrashItemType.Client:
                await RemoveEntityAsync(_uow.Clients, id, ct);
                break;
            case TrashItemType.Commission:
                await RemoveEntityAsync(_uow.Commissions, id, ct);
                break;
            case TrashItemType.Payment:
                await RemoveEntityAsync(_uow.Payments, id, ct);
                break;
            case TrashItemType.Expense:
                await RemoveEntityAsync(_uow.Expenses, id, ct);
                break;
            case TrashItemType.Quote:
                await RemoveEntityAsync(_uow.Quotes, id, ct);
                break;
            case TrashItemType.Invoice:
                await RemoveEntityAsync(_uow.Invoices, id, ct);
                break;
        }
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Called once at app startup (see App.axaml.cs's ResolveStartupWindowAsync)
    /// - anything that's been in the trash longer than RetentionDays gets
    /// permanently removed. Swallows its own errors: a failed purge attempt
    /// should never block the app from starting.
    /// </summary>
    public async Task PurgeExpiredAsync(CancellationToken ct = default)
    {
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays);
            var anyChanges = false;

            anyChanges |= await PurgeEntityAsync(_uow.Clients, cutoff, ct);
            anyChanges |= await PurgeEntityAsync(_uow.Commissions, cutoff, ct);
            anyChanges |= await PurgeEntityAsync(_uow.Payments, cutoff, ct);
            anyChanges |= await PurgeEntityAsync(_uow.Expenses, cutoff, ct);
            anyChanges |= await PurgeEntityAsync(_uow.Quotes, cutoff, ct);
            anyChanges |= await PurgeEntityAsync(_uow.Invoices, cutoff, ct);

            if (anyChanges)
                await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            // Never block app startup over a failed trash purge.
        }
    }

    private static async Task RestoreEntityAsync<T>(IRepository<T> repo, Guid id, CancellationToken ct) where T : class, ISoftDeletable
    {
        var entity = await repo.GetByIdAsync(id, ct);
        if (entity is null) return;
        entity.IsDeleted = false;
        entity.DeletedAt = null;
        repo.Update(entity);
    }

    private static async Task RemoveEntityAsync<T>(IRepository<T> repo, Guid id, CancellationToken ct) where T : class, ISoftDeletable
    {
        var entity = await repo.GetByIdAsync(id, ct);
        if (entity is null) return;
        repo.Remove(entity);
    }

    private static async Task<bool> PurgeEntityAsync<T>(IRepository<T> repo, DateTimeOffset cutoff, CancellationToken ct) where T : class, ISoftDeletable
    {
        var all = await repo.GetAllAsync(ct);
        var expired = all.Where(e => e.IsDeleted && e.DeletedAt is not null && e.DeletedAt < cutoff).ToList();
        foreach (var e in expired)
            repo.Remove(e);
        return expired.Count > 0;
    }
}
