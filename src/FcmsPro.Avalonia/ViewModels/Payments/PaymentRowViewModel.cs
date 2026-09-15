using FcmsPro.Core.Entities;

namespace FcmsPro.Avalonia.ViewModels.Payments;

/// <summary>
/// Payment only stores CommissionId/ClientId (soft references, Phase 2 §2),
/// so the list ViewModel resolves display names once via dictionary lookups
/// built from a single batch load of Commissions/Clients, rather than an
/// N+1 query per row.
/// </summary>
public class PaymentRowViewModel
{
    public Payment Payment { get; }
    public string CommissionTitle { get; }
    public string ClientName { get; }

    public PaymentRowViewModel(Payment payment, string commissionTitle, string clientName)
    {
        Payment = payment;
        CommissionTitle = commissionTitle;
        ClientName = clientName;
    }
}
