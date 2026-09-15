using FcmsPro.Core.Enums;

namespace FcmsPro.Core.Entities;

/// <summary>
/// Business-facing audit trail (the in-app "Logs" feature). Distinct from the
/// developer-facing rotating diagnostic log file written via Serilog - see
/// Phase 1 audit §2.10.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AuditLogType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
