using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TruckTrackerAnalyst.Application;
using TruckTrackerAnalyst.Domain;

namespace TruckTrackerAnalyst.Presentation;

public sealed record RowVm(CustomerProfit R)
{
    public string Name => R.Name;
    public string Code => R.Code ?? "";
    public decimal Count => R.Count;
    public decimal Expense => R.Expense;
    public decimal? Profit => R.Profit;
    public decimal? Net => R.Net;
    public string Note => WarningText.Of(R);
    public bool IsLoss => R.Net < 0;
}

public partial class MainViewModel(AnalyzeProfitUseCase useCase) : ObservableObject
{
    private static readonly DateTime PrevMonth = DateTime.Today.AddMonths(-1);

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private DateTime? _from = new DateTime(PrevMonth.Year, PrevMonth.Month, 1);

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private DateTime? _to = new DateTime(PrevMonth.Year, PrevMonth.Month, DateTime.DaysInMonth(PrevMonth.Year, PrevMonth.Month));

    [ObservableProperty] private bool _netMode;
    [ObservableProperty] private string? _error;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))] private bool _isBusy;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ExportCsvCommand))] private ProfitReport? _report;

    public ObservableCollection<RowVm> Rows { get; } = [];

    /// <summary>保存先の選択（ダイアログは View が提供。キャンセル時 null）。</summary>
    public Func<string?>? PickSavePath { get; set; }

    private bool CanAnalyze() => !IsBusy && From is { } f && To is { } t && f <= t;

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        IsBusy = true; Error = null;
        try
        {
            var r = await useCase.ExecuteAsync(DateOnly.FromDateTime(From!.Value), DateOnly.FromDateTime(To!.Value),
                NetMode ? CountMode.Net : CountMode.Count);
            Rows.Clear();
            foreach (var x in r.Rows) Rows.Add(new RowVm(x));
            Report = r;
        }
        catch (Exception ex) { Error = "集計に失敗しました: " + ex.Message; }
        finally { IsBusy = false; }
    }

    private bool CanExport() => Report is not null;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportCsv()
    {
        if (PickSavePath?.Invoke() is not { } path) return;
        try { File.WriteAllText(path, ProfitCsv.Build(Report!), new UTF8Encoding(true)); }   // BOM付き: Excelで文字化けしない
        catch (Exception ex) { Error = "CSV出力に失敗しました: " + ex.Message; }
    }
}

