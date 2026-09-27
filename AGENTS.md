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
| `FlashBeat.Editor` | `Assets/Editor/` | Editor only | `BuildScript`, `JapaneseFontFixer` |
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

1. Drop a notes JSON in `Assets/Resources/<SongName>.json` matching the `Data` schema (`name`, `maxBlock`, `BPM`, `offset`, `notes[]` with `type/num/block/LPB`).
2. Run `MakeFileScene` once after editing the hardcoded `StartTime`/`Mondai` arrays at the top of `MakeFileSceneManager.cs` — it writes `Assets/Resources/<SongName>_text.json`.
3. Update the parallel arrays in `GManager`: `SongName`, `Musician`, `SongURL`, `SBPM`, `Slevel`, `Shit`, `SongLong`, `Hiscore` (length), `totalSong`.
4. In `GameScene.unity`, duplicate a `YoutubePlayer` GameObject, rename it to the song's numeric ID, and set the URL.
5. Wire the new `YoutubePlayer` into `MusicManager.YPlayer`/`VPlayer` arrays.

## Conventions

- **Indentation / encoding**: 4-space indent, LF endings, UTF-8 BOM (`.editorconfig` + `.gitattributes`). No CRLF, no tabs.
- **C# style**: `csharp_style_expression_bodied_methods = false` — use block bodies for methods.
- **Static fields over DI**: `GManager` uses public static fields for cross-scene state, not a dependency-injected container.
- **Caution on deletions**: a comment in `MakeFileSceneManager.cs` warns "削除時はコメントアウト 変数は削除しないようにするのが望ましいです" — when removing functionality, prefer commenting out over deleting variables, because the gameplay scripts rely on a delicate balance of shared static state and parallel arrays.

## Known gotchas

- `BuildScript.cs` references `OpeningScene.unity` (wrong); file is `Opening.unity`.
- `TipingScene].unity` has a stray `]` in the filename.
- `GManager.songs[0]` is a placeholder entry (`id=0`, `title="noSong"`); real songs start at `id=1`. `GManager.songID` is initialized to 1.
- `Assets/Resources/<song>.json` and `<song>_text.json` must exist together — `MusicManager` loads one, `GameSceneManager` loads the other, and either will throw `NullReferenceException` if missing.
- `Library/`, `Temp/`, `Obj/`, `UserSettings/`, `Logs/`, `Build/`, `Builds/` are gitignored — never edit them by hand, they are Unity-generated.
- Several large font assets (`Assets/NotoSansJP-Medium SDF.asset`, `Assets/YuGothB SDF.asset`) are LFS-tracked individually in `.gitattributes` (text → LFS override).

## Constitution / operating profile

- Top-level contract: [`constitution/CONSTITUTION.md`](constitution/CONSTITUTION.md)
- Current Operating Model: [`organization/profiles/release-driven-solo.md`](organization/profiles/release-driven-solo.md)
- Unity/LFS/build/test specifics in this file remain project-specific authority while preserving the Constitution.
- project-init Skills are managed project-locally through `bunx skills` and `skills-lock.json`.
