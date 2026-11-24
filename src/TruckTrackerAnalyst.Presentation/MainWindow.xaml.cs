using MahApps.Metro.Controls;
using Microsoft.Win32;

namespace TruckTrackerAnalyst.Presentation;

public partial class MainWindow : MetroWindow
{
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Scatter.Rows = vm.Rows;
        vm.PickSavePath = () =>
        {
            var d = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"収益_{vm.From:yyyyMM}-{vm.To:yyyyMM}.csv" };
            return d.ShowDialog(this) == true ? d.FileName : null;
        };
        Loaded += (_, _) => _ = vm.ReloadAsync();   // 起動時に初期条件で表示
    }
}
