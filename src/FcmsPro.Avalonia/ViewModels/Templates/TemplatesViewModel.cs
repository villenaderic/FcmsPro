using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using FcmsPro.Avalonia.Services;
using FcmsPro.Avalonia.ViewModels.Shell;

namespace FcmsPro.Avalonia.ViewModels.Templates;

public partial class TemplatesViewModel : ObservableObject, ICreatablePage
{
    private readonly TemplateService _templateService;
    private readonly CommissionService _commissionService;
    private readonly IUnitOfWork _uow;
    private readonly DialogService _dialogService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<CommissionTemplate> Templates { get; } = new();

    /// <summary>True once a load has completed and found nothing (should be rare - defaults are always seeded) - drives the empty-state illustration in TemplatesView.</summary>
    public bool HasNoResults => !IsLoading && Templates.Count == 0;

    public TemplatesViewModel(TemplateService templateService, CommissionService commissionService,
        IUnitOfWork uow, DialogService dialogService)
    {
        _templateService = templateService;
        _commissionService = commissionService;
        _uow = uow;
        _dialogService = dialogService;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var all = await _uow.Templates.GetAllAsync();
            Templates.Clear();
            foreach (var t in all.OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name))
                Templates.Add(t);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load templates: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoResults));
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var formVm = new TemplateFormViewModel(_uow);
        await ShowFormAsync(formVm);
    }

    [RelayCommand]
    private async Task EditAsync(CommissionTemplate? template)
    {
        if (template is null) return;
        var formVm = new TemplateFormViewModel(_uow, template);
        await ShowFormAsync(formVm);
    }

    private async Task ShowFormAsync(TemplateFormViewModel formVm)
    {
        var window = new Views.Templates.TemplateFormWindow { DataContext = formVm };
        CommissionTemplate? result = null;
        formVm.Saved += t => { result = t; window.Close(); };
        formVm.Cancelled += window.Close;

        await _dialogService.ShowAsync<object>(window);
        if (result is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(CommissionTemplate? template)
    {
        if (template is null) return;
        var confirmed = await _dialogService.ConfirmAsync(
            "Delete Template?", $"\"{template.Name}\" will be permanently deleted.",
            isDestructive: true, confirmLabel: "Delete");
        if (!confirmed) return;

        _uow.Templates.Remove(template);
        await _uow.SaveChangesAsync();
        await LoadAsync();
    }

    /// <summary>
    /// Deletes rows where IsDefault=true and re-inserts the seed set,
    /// preserving user-created templates untouched (Phase 1 audit §2.9).
    /// </summary>
    [RelayCommand]
    private async Task ResetDefaultsAsync()
    {
        var confirmed = await _dialogService.ConfirmAsync(
            "Reset Default Templates?",
            "Any edits you've made to the default templates will be reverted. Your own custom templates are not affected.",
            confirmLabel: "Reset");
        if (!confirmed) return;

        await _templateService.ResetDefaultsAsync();
        await LoadAsync();
    }

    /// <summary>
    /// Computes an absolute deadline (today + DeadlineDays) and opens the
    /// normal Commission create form pre-filled - same one-way, no-auto-save
    /// pattern as Quote conversion (Phase 1 audit §2.9).
    /// </summary>
    [RelayCommand]
    private async Task UseTemplateAsync(CommissionTemplate? template)
    {
        if (template is null) return;

        var draft = _templateService.BuildCommissionDraftFromTemplate(template);
        var formVm = new ViewModels.Commissions.CommissionFormViewModel(_commissionService, _uow, draft, isDraft: true);
        var window = new Views.Commissions.CommissionFormWindow { DataContext = formVm };

        formVm.Saved += _ => window.Close();
        formVm.Cancelled += window.Close;
        await _dialogService.ShowAsync<object>(window);
    }
}
