using FcmsPro.Core.Enums;

namespace FcmsPro.Core.Entities;

public class Expense : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Mirrors Commission.RecurFrequency (same enum, same four values) so an
    /// expense that repeats on a schedule - a software subscription, a
    /// monthly retainer - doesn't have to be re-entered by hand every time.
    /// Unlike a commission, an expense has no status lifecycle to hook a
    /// spawn event off of, so this is time-based instead: see NextOccurrence.
    /// </summary>
    public RecurFrequency RecurFrequency { get; set; } = RecurFrequency.None;

    /// <summary>
    /// Only meaningful when RecurFrequency != None. The next calendar date on
    /// which ExpenseService.ProcessDueRecurrencesAsync should spawn a copy of
    /// this expense. Set on create/update whenever RecurFrequency changes,
    /// and advanced by ExpenseService each time a copy is spawned. Null when
    /// RecurFrequency is None.
    /// </summary>
    public DateOnly? NextOccurrence { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
