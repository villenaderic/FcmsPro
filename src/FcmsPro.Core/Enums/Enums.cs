namespace FcmsPro.Core.Enums;

public enum ClientType
{
    Individual = 0,
    Business = 1,
    Government = 2,
    School = 3,
    Nonprofit = 4,
    Startup = 5,
    Other = 6
}

/// <summary>
/// Linear stepper pipeline: Pending -> InProgress -> Revision -> Completed -> Delivered.
/// Cancelled is a terminal side-state outside the stepper (matches PWA behavior).
/// </summary>
public enum CommissionStatus
{
    Pending = 0,
    InProgress = 1,
    Revision = 2,
    Completed = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum CommissionPriority
{
    Normal = 0,
    High = 1,
    Urgent = 2
}

public enum RecurFrequency
{
    None = 0,
    Weekly = 1,
    Biweekly = 2,
    Monthly = 3
}

public enum PaymentMethod
{
    Cash = 0,
    GCash = 1,
    Maya = 2,
    BankTransfer = 3,
    PayPal = 4,
    Wise = 5,
    Cheque = 6,
    Other = 7
}

public enum InvoiceStatus
{
    Draft = 0,
    Sent = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4
}

public enum QuoteStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4
}

public enum ExpenseCategory
{
    SoftwareTools = 0,
    Equipment = 1,
    Marketing = 2,
    Training = 3,
    InternetUtilities = 4,
    OfficeSupplies = 5,
    TaxesFees = 6,
    Other = 7
}

public enum AuditLogType
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Login = 3,
    Backup = 4,
    Restore = 5
}
