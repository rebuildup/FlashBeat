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

| Unit | 内容 |
|---|---|
| Screens v5 · layers & shapes (FHD) | Title / Select (+typing, Ready) / History / Ranking / Game (+over 1M, Ready, Loading, YouTube error) / Pause (+Settings, Display, Ranking, Keys) / Result (+Register) / Option (+Play, Display, Data)。各画面の PNG は `design/exports/screens/` |
| Components v5 | Controls (raised): KeyCap, LegendItem, Tag, Button Primary/Secondary/Icon, MenuButton, TabItem, FilterButton, SearchField / Lists (recessed): SettingRow, RankRow, PlayerRow, HistoryRow, SongRow / Data: StatRow, BigStat, ScoreRail |
| Design System v5 | layers / controls vs lists / shapes / ranking |

UI 変更はこのファイルの **v5 の Screens / Components / Design System** を正とし、コンポーネントは `Prefab/*` 命名に対応させる。

## 設計の正本 (Issue #100)

- 正本は `flashbeat-ui.pen` の **v5 の 3 フレーム**のみ: `Components v5` / `Screens v5 · layers & shapes (FHD)` / `Design System v5`。
- 旧 `Wireframes FHD 1920x1080` (ワイヤーフレーム) は削除済み。ワイヤーフレームを実装・トークン抽出の基準にしない。
- `.pen` が参照する画像は `design/assets/` (相対パス `assets/...`) に置く。`.pen` は暗号化されているため必ず pencil MCP ツール経由で読む。
- `ui-size-profiles.json`: Unity UI のサイズ仕様 (UISizeProfile: Constant Pixel Size x1、`fhd` / `2k` / `mac` を画面比で切替、値は実ピクセル)。現行 Unity シーンの `ScaleWithScreenSize 1920x1080` はこの仕様と一致していない (#88/#96)。

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
