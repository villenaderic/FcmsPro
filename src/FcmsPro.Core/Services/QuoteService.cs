using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

public class QuoteValidationException : Exception
{
    public QuoteValidationException(string message) : base(message) { }
}

public class QuoteService
{
    private readonly IUnitOfWork _uow;

    public QuoteService(IUnitOfWork uow) => _uow = uow;

    public async Task<Quote> CreateAsync(Quote quote, CancellationToken ct = default)
    {
        Validate(quote);
        quote.Id = Guid.NewGuid();
        quote.CreatedAt = DateTimeOffset.UtcNow;
        quote.UpdatedAt = quote.CreatedAt;

        var seq = await _uow.Counters.NextAsync("quote_seq", ct);
        quote.QuoteNumber = $"QUO-{seq:D5}";

        await _uow.Quotes.AddAsync(quote, ct);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Create,
            Message = $"Created quote: {quote.QuoteNumber}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return quote;
    }

    public async Task UpdateAsync(Quote quote, CancellationToken ct = default)
    {
        Validate(quote);
        quote.UpdatedAt = DateTimeOffset.UtcNow;
        _uow.Quotes.Update(quote);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Quote quote, CancellationToken ct = default)
    {
        quote.IsDeleted = true;
        quote.DeletedAt = DateTimeOffset.UtcNow;
        _uow.Quotes.Update(quote);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Delete,
            Message = $"Moved quote to trash: {quote.QuoteNumber}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Returns a pre-filled draft Commission for the caller (ViewModel) to open
    /// in the New Commission form. Does NOT save the commission and does NOT
    /// store a back-reference to the quote - one-way, manual conversion, exact
    /// port of PWA UX (Phase 1 audit §2.6 / Phase 2 §2 default). Only callable
    /// once the quote's status is Accepted; forces Status = Accepted as a no-op
    /// if it already is, and logs the conversion.
    /// </summary>
    public async Task<Commission> BuildCommissionDraftFromQuoteAsync(Quote quote, CancellationToken ct = default)
    {
        if (quote.Status != QuoteStatus.Accepted)
        {
            quote.Status = QuoteStatus.Accepted;
            quote.UpdatedAt = DateTimeOffset.UtcNow;
            _uow.Quotes.Update(quote);
        }

        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Update,
            Message = $"Converted quote {quote.QuoteNumber} to a commission draft"
        }, ct);
        await _uow.SaveChangesAsync(ct);

        return new Commission
        {
            Title = !string.IsNullOrWhiteSpace(quote.ServiceType) ? quote.ServiceType! : quote.QuoteNumber,
            ServiceType = quote.ServiceType,
            ClientId = quote.ClientId,
            Price = quote.Total,
            DownPayment = quote.DownPayment ?? 0,
            Description = quote.Scope,
            Status = CommissionStatus.Pending
        };
    }

    private static void Validate(Quote quote)
    {
        if (quote.ClientId == Guid.Empty)
            throw new QuoteValidationException("A client must be selected.");
        if (string.IsNullOrWhiteSpace(quote.Scope))
            throw new QuoteValidationException("Scope of work is required.");
        if (quote.Total <= 0)
            throw new QuoteValidationException("Total must be greater than zero.");
    }
}
