# 開発計画（TODO）

凡例: [ ] 未着手 / [x] 完了

## Phase 0: 足場
- [x] ソリューション作成（Domain / Application / Infrastructure / Presentation / Tests）
- [x] 参照方向を 4層ルールどおりに設定、`.gitignore`、`appsettings.sample.json`
- [x] ビルド成功

## Phase 1: Domain（収益計算）— DB不要で完結
- [x] `PeriodMonths`（日割り月数）+ テスト
- [x] `ProfitCalculator`（回数集計 / 支店別按分 / 同一コード合算 / 粗利結合 / 損益 / 警告）
- [x] 回帰テスト: Excel「6月得意先別」(196回, 経費1,065,000, 粗利合計3,283,378) と一致。損益合計は意図的にExcelと差異（Excel 2,245,546 はエラー行の経費を除外 → アプリは 3,283,378−1,065,000 = 2,218,378）
- [x] 回帰テスト: 「6~8月累計」は実DB突合（`Quarter_Jun_Aug_vs_Excel`）で確認。経費3,195,000一致、粗利は全社一致（Excelの VLOOKUP 誤り除く）。固定データのテストは作らず（Excel保存後にデータが編集されているため）

## Phase 2: Infrastructure（読み取り専用データ取得）
- [x] `ReadOnlySqlGuard`（SELECT以外を拒否）+ テスト
- [x] SQL Server: 期間内 得意先別粗利の取得（`ApplicationIntent=ReadOnly`）
- [x] Supabase: GET専用クライアント（reports / stop_records / branches、分割取得）
- [x] 実DBでの疎通確認（読み取りのみ）→ Excel値と突合（`TT_INTEGRATION=1 dotnet test --filter June2026_vs_Excel`）

## Phase 3: Application
- [x] `AnalyzeProfitUseCase`（期間・回数モード → 得意先別結果。支店絞り込みは支店が1つのため見送り＝複数支店になったら追加）
- [x] ポート: `ISalesProfitRepository`, `IDeliveryRepository`（Phase 2 で作成済み）

## Phase 4: Presentation（WPF + MahApps.Metro）
- [x] MetroWindow、DI構成、条件入力 → 集計実行（DIコンテナなし・App.OnStartup で手動組み立て）
- [x] 得意先別DataGrid + 合計行、損益マイナス赤、警告表示
- [x] CSV出力、エラー/ローディング表示（CSVはBOM付きUTF-8。保存ダイアログ操作のみ未確認）

## Phase 5: 仕上げ
- [x] 未決事項の確認・反映（CLAUDE.md 末尾）
- [x] 実データで Excel と最終突合
- [x] 配布（publish）手順

## Review
### Phase 2（2026-11-13）
- 実DB（読み取りのみ）で2026年6月を突合。粗利は一致、差異は下記2点のみで原因特定済み。
  - Excel の VLOOKUP が近似一致で、売上なしの新田紙函製造所(225941)に第一印刷の粗利 14,362 を誤計上。アプリは「売上なし」(粗利0)。
  - トラック未設定の日報2件（塚田印刷+1、ワークセンター+1）→ 回数198（Excel 196）。経費は支店未確定のため按分対象外。
- ポート（ISalesProfitRepository / IDeliveryRepository）は Phase 2 で Application に前倒し作成済み。
### Phase 3
- UseCase は販売管理・配送管理を並行取得 → Domain の計算に委譲するだけの薄い層。支店フィルターは YAGNI で未実装。
### Phase 4
- UIオートメーションで実機確認: 2026年10月 → 23行・回数159・経費1,065,000・粗利3,594,486・損益2,529,486、損益マイナス行は赤字。
- 集計に約14秒（SQL Server/Supabase のどちらが支配的かは未計測 → Phase 5 で計測し、遅ければ改善）。
- CSV整形は Application 層（ProfitCsv）で単体テスト済み。ファイル保存ダイアログは手動確認が必要。
### Phase 5
- 速度: 集計14秒 → 約1秒（SQL結合に本支店コードを追加。SQL 13.4s → 0.05s）。
- 突合: 6〜8月累計の粗利は全社一致。回数は各社±1（Excel保存後のデータ編集による）。
- 修正: コードを引けない記録を同名の他の記録のコードで補完（本体の集計と同じ）→ とみや・成文社・東北印刷が Excel と一致。
- 未決事項を確定/整理（CLAUDE.md「確定事項」「未決事項」）。aborted は0件、トラック未設定は1日報のみで影響小。
- 配布: `dotnet publish`（12MB、.NET 10 Desktop Runtime 必要）。配布版の起動確認済み。
- 残り（機能追加ではなく運用）: 月ごとの経費変更、コード未設定の配達先のマスタ設定。
