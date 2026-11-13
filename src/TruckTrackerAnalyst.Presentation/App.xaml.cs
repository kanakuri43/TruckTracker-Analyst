using System.IO;
using System.Text.Json;
using System.Windows;
using TruckTrackerAnalyst.Application;
using TruckTrackerAnalyst.Infrastructure;

namespace TruckTrackerAnalyst.Presentation;

public partial class App : System.Windows.Application
{
    // 構成ルート。DIコンテナは使わず手で組み立てる（依存は4つだけ）。
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) throw new FileNotFoundException("appsettings.json が見つかりません。appsettings.sample.json を元に作成してください。", path);
            var cfg = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
            var sql = cfg.GetProperty("SqlServer").GetProperty("ConnectionString").GetString()!;
            var sb = cfg.GetProperty("Supabase");

            var useCase = new AnalyzeProfitUseCase(
                new SqlServerSalesProfitRepository(sql),
                new SupabaseDeliveryRepository(new SupabaseReadOnlyClient(sb.GetProperty("Url").GetString()!, sb.GetProperty("AnonKey").GetString()!)));
            new MainWindow(new MainViewModel(useCase)).Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
