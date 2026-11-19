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

    [ObservableProperty] private DateTime? _from = new DateTime(PrevMonth.Year, PrevMonth.Month, 1);
    [ObservableProperty] private DateTime? _to = new DateTime(PrevMonth.Year, PrevMonth.Month, DateTime.DaysInMonth(PrevMonth.Year, PrevMonth.Month));
    [ObservableProperty] private bool _netMode;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ExportCsvCommand))] private ProfitReport? _report;

    public ObservableCollection<RowVm> Rows { get; } = [];

    /// <summary>保存先の選択（ダイアログは View が提供。キャンセル時 null）。</summary>
    public Func<string?>? PickSavePath { get; set; }

    // 条件を変えたら即再集計（更新ボタンなし）
    partial void OnFromChanged(DateTime? value) => _ = ReloadAsync();
    partial void OnToChanged(DateTime? value) => _ = ReloadAsync();
    partial void OnNetModeChanged(bool value) => _ = ReloadAsync();

    private int _version;   // 最新の集計要求だけ画面に反映する（連続変更時に古い結果で上書きしない）

    /// <summary>現在の条件で集計して一覧を更新する。例外は Error に表示し外へ投げない。</summary>
    public async Task ReloadAsync()
    {
        var v = ++_version;
        if (From is not { } f || To is not { } t) { IsBusy = false; return; }
        if (f > t) { Error = "期間の開始日が終了日より後になっています。"; IsBusy = false; return; }

        IsBusy = true; Error = null;
        try
        {
            var r = await useCase.ExecuteAsync(DateOnly.FromDateTime(f), DateOnly.FromDateTime(t),
                NetMode ? CountMode.Net : CountMode.Count);
            if (v != _version) return;
            Rows.Clear();
            foreach (var x in r.Rows) Rows.Add(new RowVm(x));
            Report = r;
        }
        catch (Exception ex) { if (v == _version) Error = "集計に失敗しました: " + ex.Message; }
        finally { if (v == _version) IsBusy = false; }
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

