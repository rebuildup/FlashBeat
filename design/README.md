# design/ — UI デザインの正本 (pen.dev)

`flashbeat-ui.pen` が UI のベース (Issue #30、オーナー指示 2026-11-07)。
pen.dev CLI をプロジェクトローカルに導入済み (`@pen.dev/cli`)。

## 使い方

認証は `PEN_CLI_KEY` (Infisical FlashBeat プロジェクト) 経由。必ず `script/with-secrets` を使う:

```bash
cd design
../script/with-secrets bash -c './node_modules/.bin/pen \
  --in flashbeat-ui.pen --out flashbeat-ui.pen \
  --prompt "..." --agent pen.dev'
```

- **`--agent pen.dev` を使うこと** (ホストランタイム、PEN_CLI_KEY で動く)。
  既定の `claude` はローカル Claude Code を呼ぶ (レート制限で失敗する)。
  `--agent zai` は Zai の API キーを要求するが、Infisical のキーは Mistral API 用なので 401 になる。
- `--model` 未指定時は pen.dev 側の既定モデル。
- プロンプトでは「既存の全フレームを保持する」ことを明示すること (エージェントは
  指示が曖昧だと新しい空ファイルを作ることがある)。

## 構成

### v6 (現行の正本 — Issue #218)

| Unit | 内容 |
|---|---|
| Screens v6 | 共通フレーム `rw7zx` (1728×972) を使う v6 画面群: Select (`OENRv` / 該当なし `AqFtx` / 設定 `wSjAm`) / History / Ranking / Game プレイフィールド (`nrfw8`) / ポーズ (`uDNC0`) / 曲ローディング (`g4Q3F`) / HUD (`Uc9v6` FHD 1920×1080, `E51yo` 2K 2560×1440, `QrSwA` Mac 2560×1664) |
| Components v6 | `o0zh7` (5400×1160): 共通フレーム本体 / 選択中の曲パネル `dRztp` / Dialog / スコアゲージ `PyPE1` / 歌詞枠 `HcP8N` |

- **正本画像**: `design/exports/v6/screens/` (ノード ID 対応表は `docs/ui/v6-screen-contract.md` §14)。
- **契約 (座標・トークン・素材インベントリ)**: [`docs/ui/v6-screen-contract.md`](../docs/ui/v6-screen-contract.md)。機械可読な全量は `docs/design/v6/flashbeat-v6-nodes.json`。
- **サイズ仕様**: `ui-size-profiles.json` の `fhd` / `2k` / `mac` (1920×1080 / 2560×1440 / 2560×1664)。旧 Mac 1440×900 仕様は廃止。

### v5 (旧スタック)

| Unit | 内容 |
|---|---|
| Screens v5 · layers & shapes (FHD) | Title / Select (+typing, Ready) / History / Ranking / Game (+over 1M, Ready, Loading, YouTube error) / Pause (+Settings, Display, Ranking, Keys) / Result (+Register) / Option (+Play, Display, Data)。各画面の PNG は `design/exports/screens/` |
| Components v5 | Controls (raised): KeyCap, LegendItem, Tag, Button Primary/Secondary/Icon, MenuButton, TabItem, FilterButton, SearchField / Lists (recessed): SettingRow, RankRow, PlayerRow, HistoryRow, SongRow / Data: StatRow, BigStat, ScoreRail |
| Design System v5 | layers / controls vs lists / shapes / ranking |

v5 は **旧スタックの実装 (Assets/Game/Scripts/UI/V5/**) の正本** であり、v6 への移行進行中。v6 の画面を実装するときは **v6 の正本 (上表) を優先**する。v5 の agreed 済み機能を v6 側で消さないこと。

## 設計の正本 (Issue #100 / #218)

- 正本は `flashbeat-ui.pen` の **v6 フレーム群** (`Components v6` / Select / Game Playfield / HUD 3 解像度 / Pause / Loading)。
- 旧 v5 フレーム (`Components v5` / `Screens v5 · layers & shapes (FHD)` / `Design System v5`) は **v5 スタックの正本** として残す。v6 実装の基準にはしない。
- 旧 `Wireframes FHD 1920x1080` (ワイヤーフレーム) は削除済み。ワイヤーフレームを実装・トークン抽出の基準にしない。
- `.pen` が参照する画像は `design/assets/` (相対パス `assets/...`) に置く。`.pen` は暗号化されているため必ず pencil MCP ツール経由で読む。
- `ui-size-profiles.json`: Unity UI のサイズ仕様 (UISizeProfile: Constant Pixel Size x1、`fhd` / `2k` / `mac` を画面比で切替、値は実ピクセル)。v6 の HUD は **1 枚の 1920×1080 テンプレート** に `s = min(w/1920, h/1080)` を適用する方式が正 (§8 参照)。現行 Unity シーンの `ScaleWithScreenSize 1920x1080` はこの仕様と一致していない (#88/#96)。

## Worker が .pen を読む/編集する方法

pencil MCP が使えない環境 (omp 等) では pen CLI の interactive モードを使う。認証は Infisical の `PEN_CLI_KEY`:

```bash
cd design
../script/with-secrets bash -c "./node_modules/.bin/pen interactive --out /tmp/flashbeat-ui-scratch.pen --in flashbeat-ui.pen"
# pen > get_app_state()
# pen > execute({ input: 'Get((n,c)=>{c.skipChildren();Print(n.id,n.name)})' })   # トップレベル一覧
```

- 読むだけなら `--out` は一時ファイルにする (正本を直接上書きしない)。
- まず `design/exports/**/*.png` (正本から書き出した 1920x1080 PNG) を見て、構造の詳細が要るときだけ interactive を使う。
- Unity の UI サイズは `design/ui-size-profiles.json` (fhd/2k/mac、Constant Pixel Size x1) に従う。
