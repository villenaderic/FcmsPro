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

    /// <summary>
    /// The full set of status options for this row's dropdown, exposed
    /// directly on the row instead of via a $parent[ItemsControl] binding
    /// back up to CommissionsListViewModel. That indirection was the cause
    /// of two separate bugs: inside the kanban board's nested ItemsControls
    /// it resolved to the wrong ancestor entirely, and even on the flat
    /// table (where the binding path itself was correct) it introduced a
    /// timing gap between when ItemsSource resolves and when SelectedItem
    /// does, since they came from two different objects at different points
    /// in the binding graph - the dropdown could render blank until manually
    /// reselected, only catching up to the value SelectedStatus already had.
    /// Exposing the same static list directly on the row itself means
    /// ItemsSource and SelectedItem both resolve from this one object,
    /// removing that gap entirely - and it never needed to come from the
    /// parent in the first place, every row shows the exact same six values.
    /// </summary>
    public static IReadOnlyList<CommissionStatus> StatusOptions { get; } = Enum.GetValues<CommissionStatus>();

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
