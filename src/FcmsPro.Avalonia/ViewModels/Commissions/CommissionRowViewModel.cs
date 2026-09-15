using System;
using CommunityToolkit.Mvvm.ComponentModel;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;

namespace FcmsPro.Avalonia.ViewModels.Commissions;

/// <summary>
/// Wraps a Commission for the list/kanban row so the quick-status dropdown
/// can snapshot the PREVIOUS status before it changes - required by
/// CommissionService.QuickStatusChangeAsync, which needs to know what the
/// status was transitioning FROM to correctly apply the recurrence-spawn
/// rule (Phase 1 audit §2.2: spawn only fires on a transition INTO Delivered,
/// not on every subsequent save of an already-Delivered commission). Binding
/// a ComboBox directly to Commission.Status would overwrite the old value
/// before we could read it, so this wrapper's SelectedStatus setter captures
/// it first and raises StatusChangeRequested for the list ViewModel to act on.
/// </summary>
public partial class CommissionRowViewModel : ObservableObject
{
    public Commission Commission { get; }

    [ObservableProperty]
    private CommissionStatus _selectedStatus;

    public event Action<CommissionRowViewModel, CommissionStatus /* previous */>? StatusChangeRequested;

    public CommissionRowViewModel(Commission commission)
    {
        Commission = commission;
        _selectedStatus = commission.Status;
    }

    partial void OnSelectedStatusChanged(CommissionStatus value)
    {
        var previous = Commission.Status;
        if (previous == value) return;
        StatusChangeRequested?.Invoke(this, previous);
    }

    public string Title => Commission.Title;
    public decimal Price => Commission.Price;
    public decimal Remaining => Commission.Remaining;
    public DateOnly? Deadline => Commission.Deadline;
    public CommissionPriority Priority => Commission.Priority;
}
