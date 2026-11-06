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
| Wireframes FHD 1920x1080 | Title / Select (+typing, Ready) / History / Ranking / Game (+over 1M, Ready, Loading, YouTube error) / Pause (+Settings, Display, Ranking, Keys) / Result (+Register) / Option (+Play, Display, Data) |
| Components v5 | Controls (raised): KeyCap, LegendItem, Tag, Button Primary/Secondary/Icon, MenuButton, TabItem, FilterButton, SearchField / Lists (recessed): SettingRow, RankRow, PlayerRow, HistoryRow, SongRow / Data: StatRow, BigStat, ScoreRail |
| Design System v5 | layers / controls vs lists / shapes / ranking |

UI 変更はこのファイルの該当 Unit/Screen を正とし、コンポーネントは `Prefab/*` 命名に対応させる。
