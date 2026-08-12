# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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

**Run Tests**
- Editor: `Window → General → Test Runner → EditMode`.
- Or batch mode:
  ```
  Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml
  ```
- Tests live under `Assets/Tests/Editor/` (asmdef `FlashBeat.Tests`, scoped to `UNITY_INCLUDE_TESTS` + `nunit.framework.dll`). Single-test selection is via the Test Runner UI; no test-name CLI flag is wired up.

**Git LFS** — binary assets (images, fonts, audio, video, large SDF font assets) are tracked via LFS. After a fresh clone: `git lfs pull`.

## Architecture

Five C# assemblies (one asmdef per directory):

| Assembly | Location | Platform | Purpose |
|---|---|---|---|
| `FlashBeat` | `Assets/Game/Scripts/FlashBeat.asmdef` | All | Runtime gameplay code (subdirs: `Common`, `Gameplay`, `Persistence`, `Songs`, `UI`) |
| `FlashBeat.Editor` | `Assets/Editor/FlashBeat.Editor.asmdef` | Editor only | Build scripts, JapaneseFontFixer, scene wiring, GManager auto-updater |
| `NoteEditor` | `Assets/NoteEditor/NoteEditor.asmdef` | All (excluded by scene list) | Vendored setchi/NoteEditor (MIT); runtime-present but never built into `FlashBeat.exe` because `NoteEditor.unity` is not in `EditorBuildSettings.scenes` |
| `NoteEditor.Editor` | `Assets/Editor/NoteEditor.Editor/NoteEditor.Editor.asmdef` | Editor only | Editor-only NoteEditor extensions: `MigrateLegacyTextJson`, `YouTubeImportDialog`, `YouTubeImportRegistrar` |
| `FlashBeat.Tests` | `Assets/Tests/Editor/FlashBeat.Tests.asmdef` | Editor only | NUnit tests (defines `UNITY_INCLUDE_TESTS`, references `nunit.framework.dll`) |

### Runtime structure (FlashBeat)

**One manager per scene** — each scene has a `*SceneManager.cs` MonoBehaviour that owns the scene's UI and per-frame `Update` logic:

- `OpeningSceneManager`, `TitleSceneManager`, `SelectSceneManager`, `GameSceneManager`, `ResultSceneManager`, `OptionSceneManager`, `TypingSceneManager`, `MakeFileSceneManager`

**Global state** — `GManager` (`Assets/Game/Scripts/Songs/GManager.cs`) is a `DontDestroyOnLoad` singleton whose **static** fields are the shared state across scenes:
- Song catalog: hardcoded parallel arrays (`SongName`, `Musician`, `SongURL`, `SBPM`, `Slevel`, `Shit`, `SongLong`) built into a `SongData[] Songs` at static init via `BuildSongs()`.
- Live play state: `score`, `combo`, `perfect/great/bad/miss`, `noteSpeed`, `noteTiming`, `Start`, `StartTime`, `played`.
- Settings: `mainVolume`, `effectVolume`, `BGMVolume`, `FlashBG`, `Flash`, `FlashT`.
- High scores: `int[] Hiscore` (length 42, persisted via `SaveLoadManager` to `PlayerPrefs` key `"Hiscore"` as CSV).

Because state is static, `GManager.ResetSession()` must be called between play sessions to clear combo/score counters (called in `GameSceneManager.RetryGame`).

**Gameplay loop** (`GameScene`):
1. `MusicManager` waits for `Space`, then starts a `VideoPlayer` (URL resolved by `YouTubeStreamResolver`) and an `AudioSource` (loaded from `Resources/Musics/<songName>`).
2. `NotesManager.OnEnable` reads `Resources/<songName>.json` (`name`, `maxBlock`, `BPM`, `offset`, `notes[]` with `type/num/block/LPB`, plus the merged `lyrics` block), computes per-note hit times from BPM/LPB, and instantiates note prefabs on 8 lanes.
3. `Notes.cs` moves notes toward the player each frame using `GManager.noteSpeed`.
4. `Judge.Update` polls `Input.GetKeyDown` against the lane→keys mapping (`LaneKeys` in `Judge.cs`), measures time-lag against `GManager.StartTime`, and scores `Perfect` (≤0.10s) / `Great` (≤0.15s) / `Bad` (≤0.20s) / `Miss`.
5. On end: `MusicManager` fades and `Initiate.Fade("ResultScene", ...)` (from `SimpleFadeSystem`, referenced by the FlashBeat asmdef).

**YouTube streaming** — `YouTubeStreamResolver` (`Assets/Game/Scripts/Gameplay/YouTubeStreamResolver.cs`) spawns `yt-dlp.exe` (itag=18, ~360p mp4) to resolve a videoId to a direct `googlevideo.com` URL just-in-time (URLs expire in ~6h). Auto-detects `yt-dlp.exe` from `PATH`, common Python install dirs, or a project-bundled `Assets/StreamingAssets/yt-dlp/yt-dlp.exe`; override via `Edit > Project Settings > FlashBeat > YouTube`. The bundled `com.ibicha.youtube-player` package's Invidious path is no longer used at runtime — its public instance list has decayed and 0/13 candidate hosts return valid JSON. `InvidiousInstanceProber` (Editor menu `Tools → FlashBeat → Probe Invidious Instances`) exists only as a diagnostic tool.

**Lyrics overlay** — lyrics are stored inline in each chart JSON under a `lyrics` field (loaded by `GameSceneManager.LoadTextData`, which prefers the merged chart over any legacy `*_text.json`). Legacy `_text.json` files were migrated into the chart JSON in commit `14e3422` via `NoteEditor.Editor.MigrateLegacyTextJson`.

**8-lane keymap** (from `Judge.LaneKeys`):
```
Lane 0: 1 Q A Z        Lane 4: 6 7 Y U H J N M
Lane 1: 2 W S X        Lane 5: 8 I K , <
Lane 2: 3 E D C        Lane 6: 9 O L . >
Lane 3: 4 5 R T F G V B Lane 7: 0 - ^ \ P @ [ ; : ] / _ * `
```

### Test assemblies

36 EditMode tests under `Assets/Tests/Editor/`. Coverage spans GManager song lookup, `ResetSession`, lane-index wrap-around, time-lag math, NoteEditor `EditDataSerializer` (Furigana roundtrip + merged lyrics), `LyricsTabPresenter` (add/remove/update + Index bounds-check), `FlashBeatSongLoader` (Resources scan), `MigrateLegacyTextJson` (legacy → merged chart), and YouTube import wiring. There is no play-mode/integration suite. `SingletonTestHelper` provides a `TeardownSingletons` pattern for static-state cleanup between tests (notably required by `EditData.Lyrics` arrays).

### NoteEditor 統合

`Assets/NoteEditor/` に [setchi/NoteEditor](https://github.com/setchi/NoteEditor) を統合。譜面作成ツールとして独立して動作。`NoteEditor.asmdef` は all-platform だが `Assets/NoteEditor/Scenes/NoteEditor.unity` が build scenes list に含まれないため `FlashBeat.exe` には bundle されない (asmdef による editor 制限ではなく scene list による除外)。

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

**asmdef 構成**:
- `UniRx` (vendored lib) — `includePlatforms: []` (all platforms); NoteEditor だけが compile 時に参照する
- `NoteEditor` 本体 — `includePlatforms: []` だが `Assets/NoteEditor/Scenes/NoteEditor.unity` が build scenes list に含まれていないため `FlashBeat.exe` には bundle されない
- `NoteEditor.Editor` (Editor 限定、`includePlatforms: ["Editor"]`) — editor-only extension (`MigrateLegacyTextJson`, YouTube 取り込み dialog/registrar)
- FlashBeat 本体 (`Assets/Game/Scripts/FlashBeat.asmdef`) は UniRx を参照しない、NoteEditor は参照する

## Adding a new song

The 5 manual steps below are still valid, but the canonical path is now the Editor menu `Tools → NoteEditor → Add Song to FlashBeat...` (`Assets/Editor/MakeFileSceneManager.cs`), which orchestrates `SongMetaDialog`, `YouTubeSceneWiring`, and `GManagerParallelArrayUpdater` automatically and writes a stub `*_text.json` if lyrics are missing. The orchestrator queues writes via `DeferredEditorActions` if Play Mode is active.

Manual recipe (still valid for scripted/headless use):

1. Drop a notes JSON in `Assets/Game/Resources/<SongName>.json` matching the `Data` schema (`name`, `maxBlock`, `BPM`, `offset`, `notes[]` with `type/num/block/LPB`, plus optional `lyrics` block).
2. Update the parallel arrays in `GManager`: `SongName`, `Musician`, `SongURL`, `SBPM`, `Slevel`, `Shit`, `SongLong`, `Hiscore` (length), `totalSong`. The `GManagerParallelArrayUpdater` regex edits handle this without manual array editing.
3. In `GameScene.unity`, the `YoutubePlayer` GameObject + `MusicManager.YPlayer`/`VPlayer` array are wired automatically by `YouTubeSceneWiring` — no manual scene editing required.
4. Lyrics are stored inline in the chart JSON `lyrics` field (preferred) or written as a stub `<SongName>_text.json` by `MakeFileSceneManager.WriteTextJsonStub`. Legacy `_text.json` files were migrated in commit `14e3422`.

## Conventions

- **Indentation / encoding**: 4-space indent, LF endings, UTF-8 BOM (`.editorconfig` + `.gitattributes`). No CRLF, no tabs.
- **C# style**: `csharp_style_expression_bodied_methods = false` — use block bodies for methods.
- **Static fields over DI**: `GManager` uses public static fields for cross-scene state, not a dependency-injected container.
- **Caution on deletions**: `GManager.SongName`/`Musician`/`SongURL`/`SBPM`/`Slevel`/`Shit`/`SongLong` arrays must stay in lockstep — when removing a song, prefer commenting out the row across all 7 parallel arrays (and the matching `Resources/<song>.json`) over deleting variables, because the gameplay scripts index these arrays positionally.

## Known gotchas

- `GManager.Songs[0]` is a placeholder entry (`id=0`, `title="noSong"`); real songs start at `id=1`. `GManager.songID` is initialized to 1.
- `Assets/Resources/<song>.json` must exist with the merged `lyrics` field — `MusicManager` loads the chart, `GameSceneManager.LoadTextData` reads the inline `lyrics` block. Legacy `<song>_text.json` is no longer required; if present, the merged chart takes precedence.
- `Library/`, `Temp/`, `Obj/`, `UserSettings/`, `Logs/`, `Build/`, `Builds/`, `TestResults.xml` are gitignored — never edit them by hand, they are Unity-generated.
- Several large font assets (`Assets/NotoSansJP-Medium SDF.asset`, `Assets/YuGothB SDF.asset`) are LFS-tracked individually in `.gitattributes` (text → LFS override).
- `Assets/Editor/TempRuntimeProbe.cs.tmp` is a stray in-progress file (`.tmp` suffix) — not part of the build, leave alone unless cleaning up.
- `GManager.SongName`/`Musician`/`SongURL`/`SBPM`/`Slevel`/`Shit`/`SongLong` arrays and the `Resources/<song>.json` chart must stay in lockstep. Editor tools `GManagerParallelArrayUpdater` + `SongMetaDialog` (Editor menu) append entries programmatically and queue via `DeferredEditorActions` to defer past Play Mode. `YouTubeSceneWiring` auto-wires the new `YoutubePlayer` GameObject into `GameScene.unity` + `MusicManager`.
- `NoteEditor.Editor` callbacks (YouTube import registration, deferred editor actions) use a `[InitializeOnLoad]` static registrar pattern — see `Assets/Editor/NoteEditor.Editor/YouTubeImportRegistrar.cs` and the `feedback_noteeditor_asmdef` memory note for the cross-asmdef callback pattern.