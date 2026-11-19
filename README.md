# FlashBeat

YouTube 動画の楽曲に合わせて 8 レーンのキーボード入力を判定する、自作リズムゲーム（音ゲー）。Unity 6 製のゲーム本体と、フェスティバル向けの Web スタジオ（`web/`）で構成される。

## リポジトリ構成

| パス | 内容 |
|---|---|
| `Assets/` | Unity プロジェクト本体（C#。asmdef 構成と runtime 構造は `docs/architecture/game-runtime.md`） |
| `web/` | フェスティバル Web アプリ（Void / React / Cloudflare Workers）。ルールは `web/AGENTS.md` |
| `contracts/` | Web と Unity の間で凍結された API / 譜面契約（OpenAPI・JSON Schema） |
| `docs/` | architecture / operations / ADR などのドキュメント |
| `script/` `tools/` | ビルド・検証・受入ツール |

## 開発の入口

- 作業契約（AI エージェント・自動化ともに最初に読む）: [`AGENTS.md`](AGENTS.md)
- 運用・リリース・展示ドキュメントの一覧: [`docs/operations/README.md`](docs/operations/README.md)
- ゲーム runtime・譜面データ形式・テスト: [`docs/architecture/game-runtime.md`](docs/architecture/game-runtime.md)

## 前提と bootstrap

- Unity 6000.5.6f1（Unity Hub 経由）。clone 後は `git lfs pull` で LFS アセットを取得する
- 補助ツール（Bun・dotnet・`cf`・`void` など）は `mise install` で project-local に導入する（`mise.toml` / `mise.lock` で版を固定）
- agent 環境（vendored Skills と `skills-lock.json`）の整合性: `mise run skills-check`

## ゲームの起動

- 対象プラットフォーム: Windows 64 / macOS のスタンドアロンビルド
- ビルド: Unity Editor のメニュー `Build → Build Windows 64` / `Build → Build macOS`、または batch `-executeMethod BuildScript.BuildStandaloneWindows64` / `BuildStandaloneMacOS`（詳細は `docs/operations/update-release.md`）
- Editor からの動作確認: `Assets/Game/Scenes/TitleScene.unity` を開いて Play（楽曲は `Assets/Game/Resources/` 同梱分とリモートカタログから選択）

## 中断からの再開（開発者・エージェント向け）

- セッション中断・クラッシュ後の作業再開は会話履歴ではなく Issue / PR / branch / checkpoint を正本とする。手順は `agent-recovery` Skill（`.agents/skills/agent-recovery/SKILL.md`）を参照

## 検証

- C# 型チェック（Unity 不要）: `script/compile-check.sh`
- EditMode テスト（Unity batch mode）: `Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml`
- リポジトリ整合性（PR の `verify` CI と同じ検査）: `bash script/check-repo-integrity.sh`
- `web/` 側のセットアップとテスト: `web/AGENTS.md` のコマンド群
