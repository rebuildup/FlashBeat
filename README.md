# FlashBeat

YouTube の映像・音源に合わせて、8 lane の keyboard 入力で falling note を判定する Unity 製の音ゲー。展示会用の Windows 64 ビルドを GitHub Releases で配布する。

- Engine: Unity `6000.5.6f1`
- Target: Windows 64（展示ビルド）。Editor は macOS / Windows
- 譜面作成: `Assets/NoteEditor/`（vendored setchi/NoteEditor）
- Web Studio: `web/`（Void + Vite+ + React 19。Unity とは独立）

## はじめる

```sh
mise install --locked     # bun / uv / cf / void を repository の pin で用意する
git lfs pull              # 画像・音源・font など 156 個の LFS asset
```

Unity Hub で `6000.5.6f1` の Editor を開き、project root を追加する。`Library/` は worktree ごとに作られる生成物で、commit しない。

Web Studio だけを触る場合:

```sh
cd web
vp install
```

## 検証

```sh
# Unity EditMode tests（-quit を付けない）
Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml

# Windows 64 build
Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneWindows64

# agent 環境（vendored Skill と skills-lock.json の一致）
mise run skills-check
```

`web/` の format / type / test / build は [`web/AGENTS.md`](web/AGENTS.md) の command を使う。Windows ビルドの CI は [`.github/workflows/build-win64.yml`](.github/workflows/build-win64.yml)。

## ドキュメント

| 読むもの | 内容 |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | agent の入口。作業手順、house rule、Skill の担当表 |
| [`docs/architecture/game-runtime.md`](docs/architecture/game-runtime.md) | assembly、scene、gameplay loop、譜面、NoteEditor、既知の罠 |
| [`web/AGENTS.md`](web/AGENTS.md) | Web Studio の stack、command、layout |
| [`docs/operations/`](docs/operations/) | release、Cloudflare、D1、更新 |
| [`design/`](design/) | pen.dev の UI design (`flashbeat-ui.pen`) |
| [`Assets/NoteEditor/IMPORT.md`](Assets/NoteEditor/IMPORT.md) | NoteEditor の取り込み元と license |
| [`docs/adr/`](docs/adr/) | 長期の判断記録 |

## 開発の流れ

durable な作業は GitHub Issue で始める。branch は Issue 番号だけ（例: `56`）。最初の meaningful commit の直後に push し、Draft PR を作る。ticket PR は active な release 統合 branch へ、release PR だけが `main` へ向かう。landing は merge commit のみ。

詳細は `github-delivery` Skill と [`AGENTS.md`](AGENTS.md)。

## 復旧

中断した作業は会話ではなく Issue → branch → PR → committed docs から再構成する。手順は `agent-recovery` Skill。

## 既知の制約

- YouTube のメディアは repository に含めない。実行時に `yt-dlp` で解決する。
- `Assets/Resources/` と `Assets/Game/Resources/` の両方に旧 chart JSON が残る。新しい曲は `Assets/Game/Resources/` を正とする。
- `web/` は既存の npm lockfile を使う。その他の JS / TS は Bun を使う。
