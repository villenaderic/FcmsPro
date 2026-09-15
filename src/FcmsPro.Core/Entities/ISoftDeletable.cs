namespace FcmsPro.Core.Entities;

/// <summary>
/// Implemented by the entities with a "Delete" action in the UI that's
/// risky enough to warrant a recoverable trash instead of an immediate hard
/// delete: Client, Commission, Payment, Expense, Quote, Invoice. Deliberately
/// NOT implemented by supporting/generated records (Receipt, AuditLog,
/// CommissionAttachment, Template) - those either have no delete action, or
/// deleting them isn't the kind of "lost a financial record" mistake this
/// exists to protect against.
///
/// TrashService (and each entity's own repository GetAllAsync, updated to
/// filter !IsDeleted) is where this interface actually gets used - the
/// generic EfRepository&lt;T&gt; base class deliberately does NOT filter on
/// this itself, since T isn't constrained to ISoftDeletable and most
/// entities in the app don't implement it.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}
