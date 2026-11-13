using MahApps.Metro.Controls;
using Microsoft.Win32;

namespace TruckTrackerAnalyst.Presentation;

public partial class MainWindow : MetroWindow
{
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.PickSavePath = () =>
        {
            var d = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"収益_{vm.From:yyyyMMdd}-{vm.To:yyyyMMdd}.csv" };
            return d.ShowDialog(this) == true ? d.FileName : null;
        };
    }
}
