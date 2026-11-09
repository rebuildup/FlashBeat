# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

FlashBeat is a Unity 6 (6000.5.6f1) self-made rhythm game (`音ゲー`) that plays music from YouTube videos and judges 8-lane keyboard input against falling notes. Japanese-language project; comments and many identifiers are in Japanese.

## Build, Test, Run

This is a Unity project — there is no CLI build/lint toolchain. Use the Unity Editor or Unity batch mode.

**Open in Editor**
- Open the project root in Unity Hub with Unity 6000.5.6f1.

**Build (Windows 64)**
- Editor menu: `Build → Build Windows 64` (defined in `Assets/Editor/BuildScript.cs`). Output: `Builds/Windows/FlashBeat.exe`.
- Or batch mode:
  ```
  Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneWindows64
  ```

**Build (macOS)**
- Editor menu: `Build → Build macOS`. Output: `Builds/macOS/FlashBeat.app`.
- Or batch mode:
  ```
  Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneMacOS
  ```
- Both builds write the updater script, `update-channel.txt`, and the media-policy manifest into the artifact.
  Release/update contract and packaging (Win64 + macOS) live in `docs/operations/update-release.md`.

**Run Tests**
- Editor: `Window → General → Test Runner → EditMode`.
- Or batch mode:
  ```
  Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml
  ```
- Tests live under `Assets/Tests/Editor/` (asmdef `FlashBeat.Tests`, scoped to `UNITY_INCLUDE_TESTS` + `nunit.framework.dll`). Single-test selection is via the Test Runner UI; no test-name CLI flag is wired up.

**Git LFS** — binary assets (images, fonts, audio, video, large SDF font assets) are tracked via LFS. After a fresh clone: `git lfs pull`.

## Architecture

Three C# assemblies (one asmdef per directory):

| Assembly | Location | Platform | Purpose |
|---|---|---|---|
| `FlashBeat` | `Assets/Scripts/` | All | Runtime gameplay code |
| `FlashBeat.Editor` | `Assets/Editor/` | Editor only | `BuildScript`, `JapaneseFontFixer`, `JapaneseFontAssetBuilder` |
| `FlashBeat.Tests` | `Assets/Tests/Editor/` | Editor only | NUnit tests |

### Runtime structure (FlashBeat)

**One manager per scene** — each scene has a `*SceneManager.cs` MonoBehaviour that owns the scene's UI and per-frame `Update` logic:

- `OpeningSceneManager`, `TitleSceneManager`, `SelectSceneManager`, `GameSceneManager`, `ResultSceneManager`, `OptionSceneManager`, `TipingSceneManager`, `MakeFileSceneManager`

**Global state** — `GManager` (`Assets/Scripts/GManager.cs`) is a `DontDestroyOnLoad` singleton whose **static** fields are the shared state across scenes:
- Song catalog: hardcoded parallel arrays (`SongName`, `Musician`, `SongURL`, `SBPM`, `Slevel`, `Shit`, `SongLong`) built into a `List<SongData>` at static init.
- Live play state: `score`, `combo`, `perfect/great/bad/miss`, `noteSpeed`, `noteTiming`, `Start`, `StartTime`, `played`.
- Settings: `mainVolume`, `effectVolume`, `BGMVolume`, `FlashBG`, `Flash`, `FlashT`.
- High scores: `int[] Hiscore` (length 42, persisted via `SaveLoadManager` to `PlayerPrefs` key `"Hiscore"` as CSV).

Because state is static, `GManager.ResetSession()` must be called between play sessions to clear combo/score counters (called in `GameSceneManager.RetryGame`).

**Gameplay loop** (`GameScene`):
1. `MusicManager` waits for `Space`, then starts a `VideoPlayer` (YouTube playback via `com.ibicha.youtube-player`) and an `AudioSource` (loaded from `Resources/Musics/<songName>`).
2. `NotesManager.OnEnable` reads `Resources/<songName>.json` (`Data` → `Note[]` with `type/num/block/LPB`), computes per-note hit times from BPM/LPB, and instantiates note prefabs on 8 lanes.
3. `Notes.cs` moves notes toward the player each frame using `GManager.noteSpeed`.
4. `Judge.Update` polls `Input.GetKeyDown` against the lane→keys mapping (`LaneKeys` in `Judge.cs`), measures time-lag against `GManager.StartTime`, and scores `Perfect` (≤0.10s) / `Great` (≤0.15s) / `Bad` (≤0.20s) / `Miss`.
5. On end: `MusicManager` fades and `Initiate.Fade("ResultScene", ...)` (from `SimpleFadeSystem`, referenced by the FlashBeat asmdef).

**Lyrics overlay** — each song has a parallel `*_text.json` (loaded by `GameSceneManager.Load` into `TextData`) carrying timed `Mondai` strings used for the kanji display during play.

**8-lane keymap** (from `Judge.LaneKeys`):
```
Lane 0: 1 Q A Z        Lane 4: 6 7 Y U H J N M
Lane 1: 2 W S X        Lane 5: 8 I K , <
Lane 2: 3 E D C        Lane 6: 9 O L . >
Lane 3: 4 5 R T F G V B Lane 7: 0 - ^ \ P @ [ ; : ] / _ * `
```

### Test assemblies

`Assets/Tests/Editor/JudgeLogicTests.cs` and `SongDataTests.cs` cover GManager song lookup, `ResetSession`, lane-index wrap-around, and time-lag math. They are the only tests; there is no play-mode/integration suite.

### NoteEditor 統合

`Assets/NoteEditor/` に [setchi/NoteEditor](https://github.com/setchi/NoteEditor) を統合。譜面作成ツールとして独立して動作 (FlashBeat 本体とは別コンパイル単位、Editor のみでビルドされる)。

**取り込み情報** (詳細は `Assets/NoteEditor/IMPORT.md` を参照):
- upstream commit SHA: `189256ef612105f3ccba1440b9fbd88c38a03db6`
- ライセンス: MIT (`Assets/NoteEditor/LICENSE`)
- 取り込み日: 2026-08-07

**使い方**:
- Editor で `Assets/NoteEditor/Scenes/NoteEditor.unity` を開いて起動 (GameScene からは呼ばれない独立シーン)
- 譜面データ保存形式と FlashBeat の `Resources/<曲名>.json` 形式は互換性なし (エクスポート機能が必要なら別タスク)

**ディレクトリ構成**:

| ディレクトリ | 内容 |
|---|---|
| `Scripts/` | NoteEditor 本体 (`Common`, `DTO`, `GLDrawing`, `Model`, `Notes`, `Presenter`, `SoundEffect`, `Utility` の 8 サブディレクトリ) |
| `Art/Materials/`, `Art/Textures/` | UI 用マテリアル・テクスチャ |
| `Prefabs/` | UI プレハブ (5 個) |
| `Scenes/NoteEditor.unity` | NoteEditor エディタシーン |
| `Audio/`, `Shaders/` | 音源・シェーダ |
| `Plugins/UniRx/` | Unity 6 サポート済みの vendored UniRx |

**asmdef 構成** (Editor 限定):
- `UniRx` (vendored lib) + `NoteEditor` (本体、UniRx と TextMeshPro 参照)。両者とも `includePlatforms: ["Editor"]` で `FlashBeat.exe` には含まれない
- FlashBeat 本体 (`Assets/Game/Scripts/FlashBeat.asmdef`) は UniRx を参照しない

## Adding a new song

The comment block at the bottom of `Assets/Scripts/MakeFileSceneManager.cs` documents the canonical 5-step recipe (and is worth reading before changing GManager arrays):

1. Drop a notes JSON in `Assets/Game/Resources/<SongName>.json` matching the `Data` schema (`name`, `maxBlock`, `BPM`, `offset`, `notes[]` with `type/num/block/LPB`).
2. Run `MakeFileScene` once after editing the hardcoded `StartTime`/`Mondai` arrays at the top of `MakeFileSceneManager.cs` — it writes `Assets/Game/Resources/<SongName>_text.json`.
3. Update the parallel arrays in `GManager`: `SongName`, `Musician`, `SongURL`, `SBPM`, `Slevel`, `Shit`, `SongLong`, `Hiscore` (length), `totalSong`.
4. In `GameScene.unity`, duplicate a `YoutubePlayer` GameObject, rename it to the song's numeric ID, and set the URL.
5. Wire the new `YoutubePlayer` into `MusicManager.YPlayer`/`VPlayer` arrays.

## Conventions

- **Indentation / encoding**: 4-space indent, LF endings, UTF-8 BOM (`.editorconfig` + `.gitattributes`). No CRLF, no tabs.
- **C# style**: `csharp_style_expression_bodied_methods = false` — use block bodies for methods.
- **Static fields over DI**: `GManager` uses public static fields for cross-scene state, not a dependency-injected container.
- **Playfield rendering is flat**: the playfield materials use `FlashBeat/FlatUnlit` (unlit) and no light casts or receives shadows (`Light.shadows = None`, `shadowCastingMode = Off`, `receiveShadows = false`) — see `docs/architecture/flat-rendering.md`. Do not switch them back to a lit shader: the picture quad faces away from the directional light, so a lit shader renders the video almost black. `Assets/Editor/FlatRenderCheck.cs` audits this in batchmode.
- **Caution on deletions**: a comment in `MakeFileSceneManager.cs` warns "削除時はコメントアウト 変数は削除しないようにするのが望ましいです" — when removing functionality, prefer commenting out over deleting variables, because the gameplay scripts rely on a delicate balance of shared static state and parallel arrays.

## Known gotchas

- `BuildScript.cs` references `OpeningScene.unity` (wrong); file is `Opening.unity`.
- `TipingScene].unity` has a stray `]` in the filename.
- `GManager.songs[0]` is a placeholder entry (`id=0`, `title="noSong"`); real songs start at `id=1`. `GManager.songID` is initialized to 1.
- `Assets/Game/Resources/<song>.json` and `<song>_text.json` must exist together — `MusicManager` loads one, `GameSceneManager` loads the other, and either will throw `NullReferenceException` if missing.
- `Library/`, `Temp/`, `Obj/`, `UserSettings/`, `Logs/`, `Build/`, `Builds/` are gitignored — never edit them by hand, they are Unity-generated.
- Several large font assets (`Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset`, `Assets/Game/Fonts/Japanese/YuGothB SDF.asset`) are LFS-tracked individually in `.gitattributes` (text → LFS override).

## Cloud deployment credentials

- Infisical (self-hosted `https://secrets.rebuildup.dev`) is the only secrets source of truth. FlashBeat has its own project; never read, copy or reuse another project's Infisical secrets or Cloudflare tokens.
- Binding: `.infisical.json` (project `0645e78f-ae7a-48b3-8597-4986be983496`, environment `dev`), instance metadata in `config/infisical.sh`.
- Inject secrets for one command with `./script/with-secrets <command>` (Windows: `pwsh script/with-secrets.ps1 <command>`). Never commit `.env` files or secret values.
- Cloud CLI: the official `cf` CLI and `void` are pinned in `mise.toml` (project-local, never install globally); run `mise install` first and invoke them as `mise exec -- cf ...` / `mise exec -- void ...`. Always run them under `script/with-secrets` so `CLOUDFLARE_API_TOKEN` / `CLOUDFLARE_ACCOUNT_ID` are injected noninteractively. `wrangler` is not used.
- Current state, required token scopes, rollback and safe boundaries: [`docs/operations/cloudflare-bootstrap.md`](docs/operations/cloudflare-bootstrap.md).
- Festival D1 schema, migrations and the remote apply record: [`docs/operations/d1-migrations.md`](docs/operations/d1-migrations.md).

## Constitution / operating profile

- Top-level contract: [`constitution/CONSTITUTION.md`](constitution/CONSTITUTION.md)
- Current Operating Model: [`organization/profiles/release-driven-solo.md`](organization/profiles/release-driven-solo.md)
- Unity/LFS/build/test specifics in this file remain project-specific authority while preserving the Constitution.

## Skills (能力ごとの担当)

Skills are vendored into `.agents/skills/` and `.claude/skills/` and pinned by `skills-lock.json`. Do not edit a vendored copy; send the change upstream instead. The adoption decision and its reasons are recorded in [`docs/adr/ADR-0001-agent-environment.md`](docs/adr/ADR-0001-agent-environment.md) section 6.

| 能力 | 担当 | 使う場面 |
| --- | --- | --- |
| routing | この `AGENTS.md` | 常に |
| delivery | `github-delivery` | Issue / branch / PR / release |
| recovery | `agent-recovery` | 中断・crash・sandbox 再作成からの再開 |
| orchestration | `parallel-orchestration` | subagent / 複数 worker への分解と統合 |
| supervisor runtime | `herdr-runtime` | Herdr pane で worker を起動・監視する |
| execution isolation | `sandbox-runtime` | worker ごとの mutable runtime 分離 |
| workspace lifecycle | `worktree-workflow` | worktree の作成・掃除・build cache |
| quality gate | `quality-gate` | verification level、test / CI / build の設計 |
| design-before-build | `design-refinement` | 非自明な feature / architecture / product design |
| engineering decisions | `engineering-decisions` | 実装判断と user への escalation 境界 |
| debugging | `diagnosing-bugs` | 再現する失敗・性能劣化の原因調査 |
| TDD | `tdd` | test-first / red-green-refactor を明示した時 |
| code review | `code-review` | branch / PR / WIP の diff review |
| correctness | `correctness-assurance` | 不変条件と最小十分な保証手段 |
| naming | `code-naming` | 新規命名・rename・命名 review |
| security advisory | `security-maintenance` | dependency / runtime の脆弱性判断 |
| security audit | `security-audit` | 未知の security defect の能動探索 |
| secrets | `secrets-management` | Infisical の project / path / 権限 / 注入 |
| estimation | `agent-delivery-estimation` | 中長期の release / capacity 見積もり |
| onboarding | `onboarding` | fresh clone から開発・復旧できる docs |
| prose | `writing-discipline` | 残る文章を書く |
| Japanese prose | `natural-japanese` | 日本語の自然さ・読みやすさ |
| interaction | `interaction-discipline` | 質問・blocker・進捗の見せ方 |
| policy evaluation | `policy-evaluation` | Constitution / Skill / 環境自体の評価 |
| skill selection | `skill-selection` | 導入 Skill の選定・見直し |
| visual design | `design-intent` | UI の visual direction が未確定な時 |
| Cloudflare | `cf-cli` + `workers-best-practices` | API / D1 / Worker の操作と review（multi） |
| React / frontend | `web/AGENTS.md` | `web/` の実装。汎用 React Skill は使わない |
| release planning | GitHub Issues | Linear は採用していない |
| skill authoring | なし | 変更は upstream (`rebuildup/project-init` 等) へ PR |
| ideation / document formats / motion | なし | 必要になった時点で `skill-selection` で評価する |

Linear 系 Skill (`linear-release-control`) と pack 全体の router (`using-superpowers`, `poteto-mode` 等) は不採用。入口は root contract だけが持つ。

Spec Kit の `speckit-*` Skill（`.agents/skills/`, `.claude/skills/`）は、`docs/agent/spec-kit-workflow.md` の spec / plan / tasks workflow で使う。project-init の能力表とは別の layer として扱う。

外部 Skill が pack 内の未導入 Skill を参照する場合は、この repository の担当で読み替える。`tdd` の `codebase-design` は `design-refinement` を、`code-review` の `/setup-matt-pocock-skills` は GitHub Issue / PR を tracker として使う（setup command は実行しない）。

### Skill の管理

```sh
bun evals/policy-evaluation/vendor-skills.mts            # upstream から再取得して lock を更新
bun evals/policy-evaluation/vendor-skills.mts --check    # lock が最新か確認（network 必要）
mise run skills-check                                     # vendored と lock の hash 検査（offline）
```

`skills-lock.json` の各 entry は upstream の commit SHA で pin され、`computedHash` は skill directory 配下の全通常ファイル（`.git/` と `node_modules/` を除く）を lower-case 相対パス昇順で「パス + 生バイト列」の連結として sha256 したもの。`mise run skills-check` はこの定義を実装した eval script であり、CI でも同じ検査を走らせる。
