using System.Security.Cryptography;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

/// <summary>
/// PBKDF2-SHA256, 100,000 iterations, 16-byte salt, 256-bit derived key -
/// identical parameters to the PWA's Web Crypto implementation (Phase 1 audit
/// §2.12). This is a fresh .NET implementation via Rfc2898DeriveBytes, not a
/// literally portable hash: PWA-data import does not attempt to carry the old
/// hash over (Backup.importAll already skips the auth store today), so a
/// first-run admin password setup is required after importing old data even
/// though all business data comes over intact.
/// </summary>
public class AuthService
{
    private const int Iterations = 100_000;
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32; // 256-bit

    private readonly IUnitOfWork _uow;

    public AuthService(IUnitOfWork uow) => _uow = uow;

    public async Task<bool> IsAdminAccountSetUpAsync(CancellationToken ct = default) =>
        await _uow.AdminAccount.GetAsync(ct) is not null;

    public async Task SetupAdminAccountAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));

        var account = new AdminAccount
        {
            Id = "admin",
            Username = username,
            PasswordHash = HashPassword(password),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _uow.AdminAccount.SaveAsync(account, ct);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Create,
            Message = "Admin account created"
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<bool> VerifyLoginAsync(string password, CancellationToken ct = default)
    {
        var account = await _uow.AdminAccount.GetAsync(ct);
        if (account is null) return false;

        var ok = VerifyPassword(password, account.PasswordHash);

        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Login,
            Message = ok ? "Successful login" : "Failed login attempt"
        }, ct);
        await _uow.SaveChangesAsync(ct);

        return ok;
    }

    public async Task ChangePasswordAsync(string newPassword, CancellationToken ct = default)
    {
        var account = await _uow.AdminAccount.GetAsync(ct)
            ?? throw new InvalidOperationException("No admin account exists.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(newPassword));

        account.PasswordHash = HashPassword(newPassword);
        await _uow.AdminAccount.SaveAsync(account, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        return $"{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromHexString(parts[0]);
        var expectedHash = Convert.FromHexString(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
