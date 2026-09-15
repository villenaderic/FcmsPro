using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

public class TemplateService
{
    private readonly IUnitOfWork _uow;

    public TemplateService(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// The 8 starter templates, seeded into SQLite on first database creation
    /// (FcmsPro.Data/Seed) rather than synthesized in-memory on every load like
    /// the PWA does - see Phase 1 audit §2.9 / Phase 2 §2.
    /// </summary>
    public static IReadOnlyList<CommissionTemplate> DefaultTemplates => new List<CommissionTemplate>
    {
        new() { Name = "Icon Commission", ServiceType = "Icon", Price = 500, DownPayment = 250, DeadlineDays = 3, IsDefault = true },
        new() { Name = "Half-Body Illustration", ServiceType = "Illustration", Price = 1500, DownPayment = 750, DeadlineDays = 7, IsDefault = true },
        new() { Name = "Full-Body Illustration", ServiceType = "Illustration", Price = 2500, DownPayment = 1250, DeadlineDays = 10, IsDefault = true },
        new() { Name = "Logo Package", ServiceType = "Branding", Price = 3000, DownPayment = 1500, DeadlineDays = 14, IsDefault = true },
        new() { Name = "Character Design Sheet", ServiceType = "Character Design", Price = 3500, DownPayment = 1750, DeadlineDays = 14, IsDefault = true },
        new() { Name = "Chibi Commission", ServiceType = "Chibi", Price = 400, DownPayment = 200, DeadlineDays = 3, IsDefault = true },
        new() { Name = "Comic Page", ServiceType = "Comic", Price = 2000, DownPayment = 1000, DeadlineDays = 10, IsDefault = true },
        new() { Name = "Emote Set (5)", ServiceType = "Emotes", Price = 1200, DownPayment = 600, DeadlineDays = 7, IsDefault = true },
    };

    /// <summary>
    /// Deletes rows where IsDefault = true and re-inserts the seed set,
    /// preserving any user-created (non-default) templates untouched.
    /// </summary>
    public async Task ResetDefaultsAsync(CancellationToken ct = default)
    {
        var all = await _uow.Templates.GetAllAsync(ct);
        foreach (var t in all.Where(t => t.IsDefault))
            _uow.Templates.Remove(t);

        foreach (var seed in DefaultTemplates)
        {
            await _uow.Templates.AddAsync(new CommissionTemplate
            {
                Id = Guid.NewGuid(),
                Name = seed.Name,
                ServiceType = seed.ServiceType,
                Price = seed.Price,
                DownPayment = seed.DownPayment,
                DeadlineDays = seed.DeadlineDays,
                Description = seed.Description,
                IsDefault = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }, ct);
        }

        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Computes an absolute deadline (today + DeadlineDays) and returns a
    /// pre-filled draft Commission for the caller to open in the New Commission
    /// form - same one-way, no-auto-save pattern as Quote conversion.
    /// </summary>
    public Commission BuildCommissionDraftFromTemplate(CommissionTemplate template) => new()
    {
        Title = template.Name,
        ServiceType = template.ServiceType,
        Price = template.Price,
        DownPayment = template.DownPayment,
        Description = template.Description,
        Deadline = DateOnly.FromDateTime(DateTime.Today).AddDays(template.DeadlineDays)
    };
}
