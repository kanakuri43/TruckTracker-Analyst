using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TruckTrackerAnalyst.Presentation;

/// <summary>
/// 回数 × 距離の散布図（試作）。点の色 = 営業利益の黒字/赤字、白抜きの輪 = 回数按分と距離按分で黒字・赤字が逆転する得意先。
/// メイン画面のタブに置く。Rows（一覧）が再集計されたら描き直す。
/// </summary>
public sealed class ScatterChart : DockPanel
{
    static readonly Brush Profit = Freeze("#2a78d6"), Loss = Freeze("#e34948"), NoData = Freeze("#9a9993"),
        Ink = Freeze("#52514e"), GridLine = Freeze("#e4e3df");

    ObservableCollection<RowVm> _rows = [];
    readonly Canvas _canvas = new() { Background = Brushes.White, ClipToBounds = true };
    readonly TextBlock _footer = new() { Foreground = Ink, Margin = new Thickness(0, 6, 0, 0) };

    /// <summary>描画対象の一覧。画面と同じ寿命のコレクションを渡す（購読解除しない）。</summary>
    public ObservableCollection<RowVm> Rows
    {
        set { _rows = value; _rows.CollectionChanged += OnRowsChanged; Draw(); }
    }

    public ScatterChart()
    {
        var legend = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        legend.Children.Add(Key(Dot(Profit, false), "黒字"));
        legend.Children.Add(Key(Dot(Loss, false), "赤字"));
        legend.Children.Add(Key(Dot(NoData, false), "粗利なし（コード未設定）"));
        legend.Children.Add(Key(Dot(Ink, true), "回数按分と距離按分で黒字・赤字が逆転"));
        Margin = new Thickness(0, 8, 0, 0);
        SetDock(legend, Dock.Top);
        SetDock(_footer, Dock.Bottom);
        Children.Add(legend);
        Children.Add(_footer);
        Children.Add(_canvas);

        _canvas.SizeChanged += (_, _) => Draw();
    }

    bool _pending;

    // Clear→Add の連続を1回の描画にまとめる
    void OnRowsChanged(object? s, EventArgs e)
    {
        if (_pending) return;
        _pending = true;
        Dispatcher.BeginInvoke(() => { _pending = false; Draw(); });
    }

    void Draw()
    {
        _canvas.Children.Clear();
        double w = _canvas.ActualWidth, h = _canvas.ActualHeight;
        const double left = 56, bottom = 40, top = 12, right = 16;
        if (w < left + right + 50 || h < top + bottom + 50) return;

        var pts = _rows.Where(r => r.Km is not null).ToList();
        _footer.Text = $"{pts.Count} 件表示" + (_rows.Count > pts.Count ? $"（座標なし {_rows.Count - pts.Count} 件は除外）" : "")
                       + "　点にマウスを合わせると詳細を表示";
        var (xMax, xStep) = Axis(pts.Select(r => (double)r.Count).DefaultIfEmpty(1).Max());
        var (yMax, yStep) = Axis(pts.Select(r => (double)r.Km!.Value).DefaultIfEmpty(1).Max());
        double X(double v) => left + v / xMax * (w - left - right);
        double Y(double v) => h - bottom - v / yMax * (h - top - bottom);

        for (var xv = 0.0; xv <= xMax + 1e-9; xv += xStep)
        {
            Add(new Line { X1 = X(xv), X2 = X(xv), Y1 = Y(0), Y2 = Y(yMax), Stroke = GridLine, StrokeThickness = 1 });
            Text($"{xv:0.#}", X(xv) - 10, Y(0) + 4);
        }
        for (var yv = 0.0; yv <= yMax + 1e-9; yv += yStep)
        {
            Add(new Line { X1 = X(0), X2 = X(xMax), Y1 = Y(yv), Y2 = Y(yv), Stroke = GridLine, StrokeThickness = 1 });
            Text($"{yv:0.#}", 8, Y(yv) - 8);
        }
        Text("回数", X(xMax) - 30, h - 18);
        Text("平均距離 (km)", left + 4, 0);

        // 逆転を最後に描いて前面に出す
        foreach (var r in pts.OrderBy(r => r.R.SignFlips))
        {
            var brush = r.Net is null ? NoData : r.Net < 0 ? Loss : Profit;
            var dot = Dot(brush, r.R.SignFlips);
            dot.ToolTip = $"{r.Name}\n回数 {r.Count:0.##}　距離 {r.Km:0.0} km\n経費 {r.Expense:N0}　粗利 {r.Profit:N0}\n営業利益 {r.Net:N0}"
                          + (r.Note == "" ? "" : "\n" + r.Note);
            ToolTipService.SetInitialShowDelay(dot, 0);
            Canvas.SetLeft(dot, X((double)r.Count) - dot.Width / 2);
            Canvas.SetTop(dot, Y((double)r.Km!.Value) - dot.Height / 2);
            Add(dot);
            if (r.R.SignFlips) Text(r.Name.Replace("株式会社", ""), X((double)r.Count) + 9, Y((double)r.Km.Value) - 9);   // 逆転だけ直接ラベル
        }
    }

    void Add(UIElement e) => _canvas.Children.Add(e);

    void Text(string s, double x, double y)
    {
        var t = new TextBlock { Text = s, Foreground = Ink, FontSize = 11, IsHitTestVisible = false };
        Canvas.SetLeft(t, x); Canvas.SetTop(t, y);
        Add(t);
    }

    /// <summary>通常 = 塗り 10px（白の輪で重なりを分離）、逆転 = 白抜きの太い輪 14px。</summary>
    static Ellipse Dot(Brush b, bool flips) => flips
        ? new Ellipse { Width = 14, Height = 14, Stroke = b, StrokeThickness = 3, Fill = Brushes.White }
        : new Ellipse { Width = 10, Height = 10, Fill = b, Stroke = Brushes.White, StrokeThickness = 1.5 };

    static StackPanel Key(Ellipse dot, string label)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
        dot.VerticalAlignment = VerticalAlignment.Center;
        p.Children.Add(dot);
        p.Children.Add(new TextBlock { Text = label, Foreground = Ink, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return p;
    }

    /// <summary>目盛り間隔 = 最大値/5 を 1/2/5×10^n に切り上げ、上限 = 最大値を目盛り間隔の倍数に切り上げ（例: 54 → 間隔20・上限60）。</summary>
    static (double Max, double Step) Axis(double v)
    {
        if (v <= 0) return (1, 0.2);
        var raw = v / 5;
        var p = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = new[] { 1, 2, 5, 10 }.Select(m => m * p).First(c => c >= raw);
        return (Math.Ceiling(v / step) * step, step);
    }

    static Brush Freeze(string hex)
    {
        var b = (Brush)new BrushConverter().ConvertFrom(hex)!;
        b.Freeze();
        return b;
    }
}
