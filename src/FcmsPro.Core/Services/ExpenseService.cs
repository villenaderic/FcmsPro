using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

public class ExpenseValidationException : Exception
{
    public ExpenseValidationException(string message) : base(message) { }
}

public class ExpenseService
{
    private readonly IUnitOfWork _uow;

    public ExpenseService(IUnitOfWork uow) => _uow = uow;

    public async Task<Expense> CreateAsync(Expense expense, CancellationToken ct = default)
    {
        Validate(expense);
        expense.Id = Guid.NewGuid();
        expense.CreatedAt = DateTimeOffset.UtcNow;
        expense.UpdatedAt = expense.CreatedAt;
        expense.NextOccurrence = expense.RecurFrequency == RecurFrequency.None
            ? null
            : AddRecurInterval(expense.Date, expense.RecurFrequency);

        await _uow.Expenses.AddAsync(expense, ct);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Create,
            Message = $"Added expense: {expense.Description}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return expense;
    }

    public async Task UpdateAsync(Expense expense, CancellationToken ct = default)
    {
        Validate(expense);
        expense.UpdatedAt = DateTimeOffset.UtcNow;
        expense.NextOccurrence = expense.RecurFrequency == RecurFrequency.None
            ? null
            : expense.NextOccurrence ?? AddRecurInterval(expense.Date, expense.RecurFrequency);
        _uow.Expenses.Update(expense);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Expense expense, CancellationToken ct = default)
    {
        expense.IsDeleted = true;
        expense.DeletedAt = DateTimeOffset.UtcNow;
        _uow.Expenses.Update(expense);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Called once at app startup (App.axaml.cs's ResolveStartupWindowAsync,
    /// alongside TrashService.PurgeExpiredAsync). For every non-deleted
    /// expense with RecurFrequency != None whose NextOccurrence has arrived,
    /// spawns a new one-off copy dated on that occurrence and advances
    /// NextOccurrence forward. Catches up on multiple missed periods (e.g.
    /// the app wasn't opened for two months of a monthly expense) but caps
    /// the catch-up at 24 spawns per source expense so a long-neglected
    /// weekly recurrence can't flood the list in one startup.
    /// Swallows its own errors, same reasoning as PurgeExpiredAsync: a
    /// failed recurrence check should never block the app from starting.
    /// </summary>
    public async Task ProcessDueRecurrencesAsync(CancellationToken ct = default)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var all = await _uow.Expenses.GetAllAsync(ct);
            var due = all.Where(e => !e.IsDeleted
                && e.RecurFrequency != RecurFrequency.None
                && e.NextOccurrence is not null
                && e.NextOccurrence <= today).ToList();

            if (due.Count == 0) return;

            const int maxCatchUpPerExpense = 24;

            foreach (var source in due)
            {
                var occurrence = source.NextOccurrence!.Value;
                var spawnedCount = 0;

                while (occurrence <= today && spawnedCount < maxCatchUpPerExpense)
                {
                    var copy = new Expense
                    {
                        Description = source.Description,
                        Category = source.Category,
                        Amount = source.Amount,
                        Date = occurrence,
                        Notes = source.Notes,
                        RecurFrequency = RecurFrequency.None,
                        NextOccurrence = null,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    await _uow.Expenses.AddAsync(copy, ct);
                    await _uow.AuditLogs.AddAsync(new AuditLog
                    {
                        Type = AuditLogType.Create,
                        Message = $"Auto-created recurring expense: {copy.Description}"
                    }, ct);

                    occurrence = AddRecurInterval(occurrence, source.RecurFrequency);
                    spawnedCount++;
                }

                source.NextOccurrence = occurrence;
                source.UpdatedAt = DateTimeOffset.UtcNow;
                _uow.Expenses.Update(source);
            }

            await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            // Never block app startup over a failed recurrence check.
        }
    }

    private static DateOnly AddRecurInterval(DateOnly date, RecurFrequency frequency) => frequency switch
    {
        RecurFrequency.Weekly => date.AddDays(7),
        RecurFrequency.Biweekly => date.AddDays(14),
        RecurFrequency.Monthly => date.AddMonths(1),
        _ => date
    };

    private static void Validate(Expense expense)
    {
        if (string.IsNullOrWhiteSpace(expense.Description))
            throw new ExpenseValidationException("Description is required.");
        if (expense.Amount <= 0)
            throw new ExpenseValidationException("Amount must be greater than zero.");
    }
}
