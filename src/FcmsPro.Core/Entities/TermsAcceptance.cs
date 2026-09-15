namespace FcmsPro.Core.Entities;

/// <summary>
/// Stored in SQLite (not a flag file) so acceptance is auditable and correctly
/// re-prompts on a terms version bump. No PWA equivalent - new for this app.
/// </summary>
public class TermsAcceptance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TermsVersion { get; set; } = string.Empty;
    public bool Accepted { get; set; }
    public DateTimeOffset AcceptedAt { get; set; } = DateTimeOffset.UtcNow;
}
