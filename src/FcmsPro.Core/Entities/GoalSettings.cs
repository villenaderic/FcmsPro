namespace FcmsPro.Core.Entities;

/// <summary>
/// Singleton row (Id fixed = 1). Collapses the PWA's 4-row sparse "goals" store
/// into 4 nullable columns - Phase 1 audit §4.6 default. BackupService can still
/// export/import this as the old 4-row array shape for PWA-backup round-tripping.
/// </summary>
public class GoalSettings
{
    public int Id { get; set; } = 1;
    public decimal? MonthlyIncomeGoal { get; set; }
    public decimal? YearlyIncomeGoal { get; set; }
    public decimal? MonthlyExpenseBudget { get; set; }
    public decimal? YearlyExpenseBudget { get; set; }
}
