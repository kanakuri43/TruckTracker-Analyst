# 販売管理・配送管理データ連携ツール（TruckTracker-Analyst）

社内の SQL Server（販売管理）と Supabase（配送管理 = TruckTracker）を連携し、
**得意先別の収益 = 粗利（販売管理） − 配送経費（配送管理）** を算出するデスクトップアプリ。
現在 Excel（`C:\Users\User3292\Desktop\秋田配送- 得意先別.xlsx`）で手作業している集計をアプリ化する。
元の要望: `やりたいこと.md`

## 絶対ルール
- **両DBとも必ず読み取り専用**。INSERT / UPDATE / DELETE / DDL / マイグレーションを発行するコードを書かない。
  - SQL Server: 接続文字列に `ApplicationIntent=ReadOnly`、実行するSQLは `SELECT` のみ。`sa`（パスワードなし）で接続する前提のため、DB側の権限では守られない → **コードで強制**する（`ReadOnlySqlGuard`: SELECT 以外は例外）。
  - Supabase: anon key の RLS が `allow_all`（書込可）のため、DB側でも守られない → **HTTP GET のみ**を発行するクライアントに限定する（POST/PATCH/DELETE のメソッド自体を持たせない）。
- 接続情報（server / user / password / anon key）は `appsettings.json` に置き、**git管理外**（`.gitignore`）。ソース・ログ・ドキュメントに直書きしない。`appsettings.sample.json` のみコミット。
- SQL は必ずパラメータ化（得意先コード等の IN 句も文字列連結しない）。

## Tech stack
- C# / .NET 10（SDK 10.0.x 導入済み）/ WPF
- MahApps.Metro（UI）
- 4層構造 + MVVM（CommunityToolkit.Mvvm）
- SQL Server: Microsoft.Data.SqlClient + Dapper（読み取りのみ）
- Supabase: PostgREST（`/rest/v1/...`）を HttpClient + System.Text.Json で GET
- テスト: xUnit

## 4層構造（依存は上→下の一方向）
```
TruckTrackerAnalyst.slnx
src/
  TruckTrackerAnalyst.Presentation    WPF / MahApps / View・ViewModel（MVVM）。Application にのみ依存
  TruckTrackerAnalyst.Application     ユースケース・ポート(IFooRepository)・DTO。Domain にのみ依存
  TruckTrackerAnalyst.Domain          エンティティ・値オブジェクト・**収益計算ロジック**。依存なし
  TruckTrackerAnalyst.Infrastructure  SQL Server / Supabase 実装（ポートの実装）。Application に依存
tests/
  TruckTrackerAnalyst.Tests           Domain / Application の単体テスト
```
- 計算ロジックは Domain に純粋関数として置き、DBなしでテストできるようにする。
- Presentation の DI 構成ルート（`App.xaml.cs`）でのみ Infrastructure を参照してよい。
- ViewModel から直接 DB / HttpClient を触らない。

## データソース

### 販売管理（SQL Server）
- Server `172.16.1.231` / DB `KamiAK_O` / user `sa` / パスワードなし（接続設定は `appsettings.json`）
- 粗利取得（`売上入金データ H` LEFT JOIN `売上入金明細データ D` ON `H.インデックス番号 = D.インデックス番号`）:
  ```sql
  SELECT H.得意先コード, MIN(得意先名) AS 得意先名,
         SUM(D.売価金額) - SUM(D.原価金額) AS profit
  FROM 売上入金データ H
  LEFT JOIN 売上入金明細データ D ON H.本支店コード = D.本支店コード AND H.インデックス番号 = D.インデックス番号
  WHERE 処理区分 = 1
    AND H.伝票日付 BETWEEN @from AND @to      -- int 形式 yyyyMMdd
  GROUP BY H.得意先コード
  ```
  - **結合は `本支店コード` も含める**（主キー先頭列。`インデックス番号`のみだと索引が効かず13秒→含めると0.05秒。結果は同一）。元のExcel用SQLは `インデックス番号`のみ。
  - Excel では IN 句で対象得意先を絞っていたが、アプリでは **期間内の全得意先を取得 → メモリ上で配送先とコード結合**（IN句を避ける）。

### 配送管理（Supabase / TruckTracker）
- リポジトリ: `C:\Users\User3292\source\repos\TruckTracker`（スキーマ: `supabase/schema.sql`、参考実装: `public/js/admin.js` の `aggregateDestData` / `periodMonths`）
- Supabase URL / anon key は TruckTracker の `public/config.js` と同じ（`appsettings.json` に設定）。
- 使うテーブル: `reports`(date, truck_id, status) → `trucks`(branch_id) → `branches`(monthly_expense) / `stop_records`(report_id, destination_name, arrived_at, course_stop_id) → `course_stops` → `destinations`(sales_customer_code)
- 取得イメージ（GETのみ）:
  - `reports?select=id,date,truck_id,status,trucks(branch_id,branches(name,monthly_expense))&date=gte.{from}&date=lte.{to}`
  - `stop_records?select=report_id,destination_name,arrived_at,course_stops(destinations(sales_customer_code))&report_id=in.(...)`（URL長に注意し分割取得）

## 収益計算ロジック（Excel と TruckTracker の `aggregateDestData` から確定）
1. **対象配送**: 期間内の `stop_records` のうち `arrived_at IS NOT NULL`（配達完了）。TruckTracker の CSV 出力と同条件（reports.status では絞らない）。
2. **回数**: 配達先ごとの到着記録数。`正味回数` モード（1コース内の配達先数で按分: 1/stops）も切替可能（デフォルトは通常回数）。
3. **期間経費** = `branches.monthly_expense × 期間月数`。月数は端数月を日割り（`periodMonths`: 例 6/1〜8/31 → 3、10/15〜10/31 → 17/31）。
4. **1配送あたり経費**（支店ごと）= 期間経費 ÷ その支店の回数合計。
5. **得意先別経費** = Σ（支店ごとの 1配送あたり経費 × 得意先のその支店での回数）。
   - 検算: Excel 6月 = 1,065,000 ÷ 196回 ≒ 5,434円/回、6〜8月累計 = 3,195,000 ÷ 522回 ≒ 6,121円/回。
6. **粗利**: `destinations.sales_customer_code` ＝ 販売管理 `得意先コード` で結合（Excel の `VLOOKUP(VALUE(B2), I:K, 3)` 相当。コードは文字列↔数値変換に注意）。
7. **営業利益** = 粗利 − 得意先別経費。合計行あり。
8. **同一コードの複数配達先は1得意先に合算**（同名の他の記録のコードで補完しても決まらない配達先は、配達先名単位で別行）。
9. **異常系の表示**（Excel では `#VALUE!` / `#N/A` になっていた箇所をアプリでは明示する）:
   - コード未設定 → 粗利は算出不可（空欄）。備考「得意先コード未設定のため粗利算出不可（経費のみ計上）」。
   - コードはあるが期間内に売上なし → 粗利は 0 扱い。備考「期間内売上なし（粗利0円で計算）」。
10. 金額は円・整数表示（内部計算は decimal、表示時に四捨五入）。Excel の合計は丸め後合計なので ±1円ずれることがある。
11. **距離加重**（画面のスライダーで指定。0% = 上記の従来計算）: 支店ごとに 期間経費 × { (1−s) × 回数/総回数 + s × (回数×距離)/Σ(回数×距離) }。s = 距離分(%)、0〜100% を25%刻み、初期50%。
    - 距離 = 支店（`branches.lat/lng`）→ 配達先（`destinations.lat/lng`）の直線距離（Haversine、`Geo.Km`）。
    - 座標なしの記録は同名の配達先の距離で補完し、それでもなければ座標ありの到着記録の平均距離を使う。支店の全記録が 0km なら回数のみで按分。

## UI 方針（MahApps.Metro）
- 条件指定（期間・支店・回数モード）→ 集計実行 → 得意先別グリッド（配送先名 / 得意先コード / 回数 / 期間経費 / 粗利 / 営業利益 / 備考）＋合計行。
- 営業利益マイナスは赤。CSV/Excel（.csv で可）出力。
- 長時間処理は async/await + ローディング表示、UIスレッドをブロックしない。

## 開発の進め方
- 計画・進捗は `tasks/todo.md`（フェーズ単位）。作業前に読み、完了時に更新する。
- フェーズごとに「ビルド成功 + テスト緑」を確認してから次へ。Excel の 6月・6〜8月累計シートの値を**回帰テストの期待値**にする。
- 非自明な変更は先に計画を提示。シンプル優先（YAGNI）、不要な抽象化をしない。
- コミットメッセージ・UI文言・コメントは日本語で可。コード識別子は英語。

## 最新版の実行ファイル（必須ルール）
- **プログラムを修正したら、必ず Debug ビルドして下記 exe を最新にしてから作業完了とする。**
  `C:\Users\User3292\source\repos\TruckTracker-Analyst\src\TruckTrackerAnalyst.Presentation\bin\Debug\net10.0-windows\TruckTrackerAnalyst.Presentation.exe`
- 更新コマンド: `dotnet build TruckTrackerAnalyst.slnx`（Debug）。完了報告時に exe の更新日時が今回のビルドになっていることを確認する。
- アプリ起動中は exe/dll がロックされビルドに失敗する → 起動中なら閉じてから（または終了させてから）ビルドする。
- 接続情報 `appsettings.json` は同フォルダに自動コピーされる（git管理外）。

## コマンド
```
dotnet build TruckTrackerAnalyst.slnx
dotnet test  tests/TruckTrackerAnalyst.Tests
dotnet run   --project src/TruckTrackerAnalyst.Presentation
```

## 配布（publish）
```
dotnet publish src/TruckTrackerAnalyst.Presentation -c Release -o publish
```
- `publish/` フォルダごと配布する（約12MB、framework-dependent）。利用PCに **.NET 10 Desktop Runtime** が必要。
- `publish/appsettings.json` に接続情報（sa・anon key）が含まれる。**社内配布のみ**。外部に渡さない・git管理しない（`publish/` は `.gitignore` 済み）。
- 日常の最新版確認用の exe は Debug 版（上記「最新版の実行ファイル」）。

## 確定事項・既知の差異（Excel との突合結果）
- 実DB（読み取りのみ）と Excel 6月・6〜8月累計を突合済み。**粗利は全得意先で一致**。
- Excel の粗利 VLOOKUP は近似一致のため、売上なしコード（例: 新田紙函製造所 225941）に別得意先の粗利が入る誤りがある（6月 +14,362円、累計 +584,259円）。アプリは完全一致が正。
- 営業利益合計は Excel（エラー行の経費を除外）と意図的に異なる。アプリ = 粗利合計 − 経費合計。
- コードを引けない到着記録（`course_stop` 紐付け切れ）は、**同名の配達先の他の記録のコードで補完**（TruckTracker 本体の集計と同じ）。
- 回数は各社±1回ずれる場合がある。Excel 保存（2026-09-03）後に配送データが編集されたため（粗利は一致）。
- 備考欄は1行に収まる短い文（`WarningText`、画面とCSV共通）。売上なし: 粗利0円で計算。コード未設定: 粗利は算出不可（空欄）で経費のみ計上。
- `reports.status = 'aborted'` は 6〜8月に0件で影響なし。TruckTracker 本体と同じく除外しない。
- トラック未設定の日報（2026-06-11 市内15時便、status=planned、到着記録2件）: 回数には含め、支店不明のため経費は按分されない。

## 未決事項
- 月間経費が月ごとに変わる場合の扱い（現状 branches.monthly_expense は単一値）。経費を変更したら過去月の結果も変わる点に注意。
- 配達先マスタにコード未設定の先（三傳商事・近藤とっぱん）の紐付け運用（マスタ側で `sales_customer_code` を設定すれば解消）。
- 複数支店になったら支店での絞り込みを追加。
