using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

public class ClientValidationException : Exception
{
    public ClientValidationException(string message) : base(message) { }
}

public class ClientService
{
    private readonly IUnitOfWork _uow;

    public ClientService(IUnitOfWork uow) => _uow = uow;

    public async Task<Client> CreateAsync(Client client, CancellationToken ct = default)
    {
        Validate(client);
        client.Id = Guid.NewGuid();
        client.DateAdded = DateTimeOffset.UtcNow;
        client.UpdatedAt = client.DateAdded;

        await _uow.Clients.AddAsync(client, ct);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Create,
            Message = $"Added client: {client.Name}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return client;
    }

    public async Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        Validate(client);
        client.UpdatedAt = DateTimeOffset.UtcNow;
        _uow.Clients.Update(client);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Update,
            Message = $"Updated client: {client.Name}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Soft-deletes the client (moves it to the trash - see TrashService)
    /// without touching related commissions - they are left with a
    /// dangling ClientId, matching PWA behavior (Phase 1 audit §4.2 /
    /// Phase 2 §2 default). UI is expected to warn the user before calling
    /// this, same as the PWA's confirm-dialog copy ("commissions... will
    /// remain but show no client").
    /// </summary>
    public async Task DeleteAsync(Client client, CancellationToken ct = default)
    {
        client.IsDeleted = true;
        client.DeletedAt = DateTimeOffset.UtcNow;
        _uow.Clients.Update(client);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Delete,
            Message = $"Moved client to trash: {client.Name}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Live-computed profile totals - never stored on the Client record, matching
    /// PWA behavior (Phase 1 audit §3).
    /// </summary>
    public async Task<ClientProfileStats> GetProfileStatsAsync(Guid clientId, CancellationToken ct = default)
    {
        var commissions = await _uow.Commissions.GetByClientIdAsync(clientId, ct);
        decimal totalPaid = 0m;
        decimal outstanding = 0m;

        foreach (var c in commissions)
        {
            var payments = await _uow.Payments.GetByCommissionIdAsync(c.Id, ct);
            totalPaid += payments.Sum(p => p.Amount);
            outstanding += c.Remaining;
        }

        return new ClientProfileStats(commissions.Count, totalPaid, outstanding);
    }

    private static void Validate(Client client)
    {
        if (string.IsNullOrWhiteSpace(client.Name))
            throw new ClientValidationException("Client name is required.");

        if (!string.IsNullOrWhiteSpace(client.Email) && !IsValidEmail(client.Email))
            throw new ClientValidationException("Email format is invalid.");
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

public record ClientProfileStats(int CommissionCount, decimal TotalPaid, decimal OutstandingBalance);
