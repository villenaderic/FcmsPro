using System.Collections.ObjectModel;
using FcmsPro.Core.Enums;

namespace FcmsPro.Avalonia.ViewModels.Commissions;

/// <summary>
/// One status column in the kanban view. Holds the SAME CommissionRowViewModel
/// instances as CommissionsListViewModel.Rows (not copies) - each row's
/// SelectedStatus/StatusChangeRequested wiring already works identically
/// whether the row is displayed in the table or a kanban card, since it's
/// the same object either way.
/// </summary>
public class KanbanColumnViewModel
{
    public CommissionStatus Status { get; }
    public string DisplayName => Status switch
    {
        CommissionStatus.InProgress => "In Progress",
        var s => s.ToString()
    };

    public ObservableCollection<CommissionRowViewModel> Items { get; } = new();

    public KanbanColumnViewModel(CommissionStatus status) => Status = status;
}
