namespace FcmsPro.Core.Entities;

/// <summary>
/// Single row (Id fixed = "admin"). PasswordHash format: "{saltHex}:{hashHex}",
/// PBKDF2-SHA256, 100,000 iterations, 16-byte salt, 256-bit derived key - the
/// same parameters as the PWA's Web Crypto implementation, reimplemented via
/// Rfc2898DeriveBytes (not a literal ported hash; PWA-data import does not
/// carry credentials over, matching Backup.importAll's existing behavior of
/// skipping the auth store).
/// </summary>
public class AdminAccount
{
    public string Id { get; set; } = "admin";
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
