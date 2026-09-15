using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Avalonia.ViewModels.Templates;

public partial class TemplateFormViewModel : ObservableObject
{
    private readonly IUnitOfWork _uow;
    private readonly CommissionTemplate? _existing;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _serviceType;
    [ObservableProperty] private decimal? _price;
    [ObservableProperty] private decimal? _downPayment;
    [ObservableProperty] private decimal? _deadlineDays = 7; // decimal for NumericUpDown binding, cast to int on save
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public bool IsEditMode => _existing is not null;
    public string HeaderText => IsEditMode ? "Edit Template" : "New Template";

    public event Action<CommissionTemplate>? Saved;
    public event Action? Cancelled;

    public TemplateFormViewModel(IUnitOfWork uow, CommissionTemplate? existing = null)
    {
        _uow = uow;
        _existing = existing;

        if (existing is null) return;

        Name = existing.Name;
        ServiceType = existing.ServiceType;
        Price = existing.Price;
        DownPayment = existing.DownPayment;
        DeadlineDays = existing.DeadlineDays;
        Description = existing.Description;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Name is required.";
            return;
        }

        IsBusy = true;
        try
        {
            if (_existing is not null)
            {
                _existing.Name = Name;
                _existing.ServiceType = ServiceType;
                _existing.Price = Price ?? 0;
                _existing.DownPayment = DownPayment ?? 0;
                _existing.DeadlineDays = (int)(DeadlineDays ?? 7);
                _existing.Description = Description;
                _existing.UpdatedAt = DateTimeOffset.UtcNow;

                _uow.Templates.Update(_existing);
                await _uow.SaveChangesAsync();
                Saved?.Invoke(_existing);
            }
            else
            {
                var created = new CommissionTemplate
                {
                    Name = Name,
                    ServiceType = ServiceType,
                    Price = Price ?? 0,
                    DownPayment = DownPayment ?? 0,
                    DeadlineDays = (int)(DeadlineDays ?? 7),
                    Description = Description,
                    IsDefault = false
                };
                await _uow.Templates.AddAsync(created);
                await _uow.SaveChangesAsync();
                Saved?.Invoke(created);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}
