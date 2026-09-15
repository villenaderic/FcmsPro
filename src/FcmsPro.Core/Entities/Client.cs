using FcmsPro.Core.Enums;

namespace FcmsPro.Core.Entities;

public class Client : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ClientType ClientType { get; set; } = ClientType.Individual;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Social { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
