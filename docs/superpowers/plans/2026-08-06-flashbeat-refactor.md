# FlashBeat Refactoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor FlashBeat to consolidate song data into `SongData[]`, restore YouTube streaming as the primary playback path, organize `Assets/` directory structure, rename Japanese Hierarchy/script names to English, and remove dead code — without changing gameplay behavior.

**Architecture:**
- `SongData[] Songs` replaces GManager's 7 parallel arrays as the single source of truth. Consumers use `GManager.GetSong(id)` or `GManager.Songs[id]`.
- YouTube streaming via `InvidiousVideoPlayer` is the primary video path; local mp4 (`VideoPlayer`) is the fallback.
- `Assets/` reorganizes into `Art/{Materials,Prefabs}`, `Fonts/Japanese/`.
- Hierarchy/script names English-ify except display-only Japanese labels.

**Tech Stack:** Unity 6000.5.6f1, Unity Test Framework (NUnit), Unity MCP tools (`mcp__UnityMCP__*`), `com.ibicha.youtube-player` (Invidious API).

---

## File Structure

### Files to modify (runtime C#)
| File | Purpose |
|---|---|
| `Assets/Scripts/GManager.cs` | Convert to `SongData[] Songs` single source |
| `Assets/Scripts/MusicManager.cs` | Consume `Songs[]` instead of `SongName[]` |
| `Assets/Scripts/GameSceneManager.cs` | Consume `Songs[]` + remove dead code |
| `Assets/Scripts/NotesManager.cs` | Consume `Songs[]` |
| `Assets/Scripts/SelectSceneManager.cs` | Consume `Songs[]` |
| `Assets/Scripts/OptionSceneManager.cs` | Consume `Songs[]` |
| `Assets/Scripts/ResultSceneManager.cs` | Consume `Songs[]` |
| `Assets/Scripts/BGFlash.cs` | Consume `Songs[]` |
| `Assets/Scripts/TipingSceneManager.cs` | Consume `Songs[]` (file renamed in Task 23) |
| `Assets/Scripts/MakeFileSceneManager.cs` | Consume `Songs[]` + update song addition procedure |

### Files to rename
| Old | New |
|---|---|
| `Assets/Scripts/Light.cs` | `Assets/Scripts/LaneFlash.cs` |
| `Assets/Scripts/TipingSceneManager.cs` | `Assets/Scripts/TypingSceneManager.cs` |
| `Assets/Scenes/TipingScene].unity` | `Assets/Scenes/TypingScene.unity` |

### Files to move
| Old | New |
|---|---|
| `Assets/Material/` (whole folder) | `Assets/Art/Materials/` |
| `Assets/Prefab/` (whole folder) | `Assets/Art/Prefabs/` |
| `Assets/NotoSansJP-Medium.ttf` | `Assets/Fonts/Japanese/NotoSansJP-Medium.ttf` |
| `Assets/NotoSansJP-Medium SDF.asset` | `Assets/Fonts/Japanese/NotoSansJP-Medium SDF.asset` |
| `Assets/YuGothB.ttc` | `Assets/Fonts/Japanese/YuGothB.ttc` |
| `Assets/YuGothB SDF.asset` | `Assets/Fonts/Japanese/YuGothB SDF.asset` |
| `Assets/japanese_full.txt` | `Assets/Fonts/Japanese/japanese_full.txt` |
| `Assets/Scenes/tttt.unity` | `Assets/Scenes/Legacy/tttt.unity` |

### Material renames (inside `Assets/Art/Materials/`)
| Old | New |
|---|---|
| `ゲーム背景.mat` | `GameBackground.mat` |
| `ノーツ.mat` | `Note.mat` |
| `パーティクル.mat` | `Particle.mat` |
| `ライト.mat` | `LaneLight.mat` |
| `レーン.mat` | `Lane.mat` |
| `判定線.mat` | `JudgeLine.mat` |
| `背景.mat` | `Background.mat` |
| `黒半透明.mat` | `BlackSemiTransparent.mat` |

### Prefab renames (inside `Assets/Art/Prefabs/`)
| Old | New |
|---|---|
| `スクリーン.prefab` | `Screen.prefab` |
| `ノーツ.prefab` | `Note.prefab` |
| `Image (1).prefab` | `ScreenVideo.prefab` |
| `Image (1) 1.prefab` | `FullscreenOverlay.prefab` |
| `BackGround.prefab` | `Background.prefab` |
| `Particle System.prefab` | `ParticleSystem.prefab` |
| `Directional Light.prefab` | `DirectionalLight.prefab` |

### Hierarchy renames in `GameScene.unity`
| Old | New |
|---|---|
| `ライト` | `LaneLights` |
| `ライト1`〜`ライト8` | `LaneLight1`〜`LaneLight8` |
| `スコアゲージ` | `ScoreGauge` |
| `判定線` | `JudgeLine` |
| `レーン` | `Lane` |
| `問題` | `QuestionText` |
| `ふりがな` | `FuriganaText` |
| `ローマ字` | `RomajiText` |
| `スクリーン` | `Screen` |
| `サムネイル` | `Thumbnail` |

### Test files
| File | Purpose |
|---|---|
| `Assets/Tests/Editor/SongDataTests.cs` | Update for `Songs[]` array |
| `Assets/Tests/Editor/JudgeLogicTests.cs` | Update if any GManager field references break |

### Editor files
| File | Purpose |
|---|---|
| `Assets/Editor/BuildScript.cs` | Fix `OpeningScene.unity` → `Opening.unity` |

### Files NOT touched
- `Assets/Resources/` (runtime load paths must not change)
- `Assets/Scripts/FlashBeat.asmdef` (already references `YoutubePlayer`)
- `Packages/manifest.json` (keeps `com.ibicha.youtube-player`)
- `Assets/Scripts/YoutubePlayy.cs` (already deleted)
- `Assets/Prefab/YoutubePlayer.prefab` and `YoutubePlayer 1.prefab` (already configured with `InvidiousVideoPlayer`)
- `GameScene` child objects `0`〜`26` (already have `InvidiousVideoPlayer`)
- `InvidiousInstance` GameObject (already in scene)

---

## Conventions

- **Indentation:** 4 spaces, LF line endings, UTF-8 BOM (per `.editorconfig` + `.gitattributes`).
- **Commits:** One commit per task. Commit message uses conventional prefixes (`test:`, `refactor:`, `chore:`, `feat:`, `fix:`).
- **Unity MCP:** When MCP tools are available, prefer them for scene/asset operations; otherwise use Unity Editor menu/manual operations. Steps note which.
- **TDD:** New C# logic uses test-first. Scene/asset/YAML edits verify by reading the file and Unity console.
- **In Unity context:** Do not run `git commit` while Unity Editor has unsaved scene changes — save scene first or use Editor menu.

---

# Phase 0: Pre-flight Verification

## Task 1: Record current Unity Editor state

**Files:**
- Read: Unity Editor console
- Read: `Assets/Tests/Editor/*.cs`

- [ ] **Step 1: Open Unity Editor with the project**

Open Unity Hub → FlashBeat project. Wait for compilation to finish.

- [ ] **Step 2: Capture baseline compile state**

Read: `mcp__UnityMCP__read_console` (action=get, types=["error","warning"], count=20, format=detailed)
Save the output as `docs/superpowers/plans/baseline-console.md` for reference.

- [ ] **Step 3: Capture baseline test state**

Open `Window → General → Test Runner → EditMode → Run All`. Wait for completion.
Record pass/fail counts. Save summary to `docs/superpowers/plans/baseline-tests.md`.

- [ ] **Step 4: Verify git working tree is committed**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git status --short
```

Expected: only the files modified in earlier sessions (MusicManager.cs, prefabs, scene) appear. No untracked changes from spec writing.

- [ ] **Step 5: No commit needed for this task**

This is a baseline capture. No code changes.

---

# Phase 1: SongData[] Consolidation

The bulk of the work. Each consumer of `GManager.SongName[]`, `Musician[]`, `SongURL[]`, `SBPM[]`, `Slevel[]`, `Shit[]`, `SongLong[]`, `songs` (List) migrates to `GManager.Songs[]` (array) or `GManager.GetSong(id)`.

## Task 2: Write failing tests for new `Songs` array API

**Files:**
- Modify: `Assets/Tests/Editor/SongDataTests.cs`

- [ ] **Step 1: Replace existing test file with updated tests**

Overwrite `Assets/Tests/Editor/SongDataTests.cs` with:

```csharp
using NUnit.Framework;
using UnityEngine;

public class SongDataTests
{
    [Test]
    public void TestSongsArrayNotEmpty()
    {
        Assert.Greater(GManager.Songs.Length, 0, "Songs array should not be empty.");
    }

    [Test]
    public void TestSongsIndexMatchesId()
    {
        for (int i = 0; i < GManager.Songs.Length; i++)
        {
            Assert.AreEqual(i, GManager.Songs[i].id, $"Songs[{i}].id should be {i}.");
        }
    }

    [Test]
    public void TestSongsIndexZeroIsPlaceholder()
    {
        Assert.AreEqual("noSong", GManager.Songs[0].title, "Songs[0] should be the noSong placeholder.");
    }

    [Test]
    public void TestSongIdOneTitleUnchanged()
    {
        Assert.AreEqual("ロストアンブレラ", GManager.Songs[1].title, "Songs[1].title must match prior SongName[1].");
    }

    [Test]
    public void TestGetSongValidAndInvalidId()
    {
        SongData song = GManager.GetSong(1);
        Assert.IsNotNull(song, "Song ID 1 should exist.");
        Assert.AreEqual("ロストアンブレラ", song.title);

        Assert.IsNull(GManager.GetSong(-1), "Negative song ID should return null.");
        Assert.IsNull(GManager.GetSong(999), "Out-of-bounds song ID should return null.");
    }

    [Test]
    public void TestTotalSongDerivedFromSongsLength()
    {
        Assert.AreEqual(GManager.Songs.Length - 1, GManager.totalSong, "totalSong should equal Songs.Length - 1 (excluding noSong placeholder).");
    }

    [Test]
    public void TestVideoIdMigrationMatchesPriorSongUrl()
    {
        // GManager.SongURL must remain valid for the duration of the migration.
        // After Task 4 (MusicManager update), this can be tightened further.
        Assert.AreEqual(GManager.SongURL[1], GManager.Songs[1].videoId, "Songs[1].videoId must equal SongURL[1].");
        Assert.AreEqual(GManager.SongURL[10], GManager.Songs[10].videoId, "Songs[10].videoId must equal SongURL[10].");
        Assert.AreEqual(GManager.SongURL[26], GManager.Songs[26].videoId, "Songs[26].videoId must equal SongURL[26].");
    }

    [Test]
    public void TestResetSession()
    {
        GManager.perfect = 10;
        GManager.great = 5;
        GManager.bad = 2;
        GManager.miss = 1;
        GManager.score = 500000;
        GManager.combo = 15;

        GManager.ResetSession();

        Assert.AreEqual(0, GManager.perfect);
        Assert.AreEqual(0, GManager.great);
        Assert.AreEqual(0, GManager.bad);
        Assert.AreEqual(0, GManager.miss);
        Assert.AreEqual(0, GManager.score);
        Assert.AreEqual(0, GManager.combo);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Use MCP:
```python
mcp__UnityMCP__run_tests(mode="EditMode", test_names=["SongDataTests.TestSongsArrayNotEmpty", "SongDataTests.TestSongsIndexMatchesId", "SongDataTests.TestSongsIndexZeroIsPlaceholder", "SongDataTests.TestSongIdOneTitleUnchanged", "SongDataTests.TestGetSongValidAndInvalidId", "SongDataTests.TestTotalSongDerivedFromSongsLength", "SongDataTests.TestVideoIdMigrationMatchesPriorSongUrl", "SongDataTests.TestResetSession"])
```

Or: `Window → Test Runner → EditMode → Run All`.

Expected: ALL SongDataTests fail with compile errors referencing `GManager.Songs` (does not exist yet) or `SongData.videoId` (not renamed yet). `TestResetSession` passes (existing API).

- [ ] **Step 3: Commit failing tests**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Tests/Editor/SongDataTests.cs
git commit -m "test: add failing tests for GManager.Songs[] array API"
```

---

## Task 3: Refactor `GManager.SongData` to use `Songs` array

**Files:**
- Modify: `Assets/Scripts/GManager.cs`

- [ ] **Step 1: Rename `SongData.youtubeUrl` field to `videoId`**

In `Assets/Scripts/GManager.cs`, edit the `SongData` class (lines 7–29):

Replace:
```csharp
    public string youtubeUrl;

    public SongData(int id, string title, string musician, string youtubeUrl, int bpm, int level, int totalHits, float duration)
    {
        this.id = id;
        this.title = title;
        this.musician = musician;
        this.youtubeUrl = youtubeUrl;
        this.bpm = bpm;
        this.level = level;
        this.totalHits = totalHits;
        this.duration = duration;
    }
```

With:
```csharp
    public string videoId;

    public SongData(int id, string title, string musician, string videoId, int bpm, int level, int totalHits, float duration)
    {
        this.id = id;
        this.title = title;
        this.musician = musician;
        this.videoId = videoId;
        this.bpm = bpm;
        this.level = level;
        this.totalHits = totalHits;
        this.duration = duration;
    }
```

- [ ] **Step 2: Replace `List<SongData> songs` with `SongData[] Songs` and update initialiser**

In `Assets/Scripts/GManager.cs`:

Replace:
```csharp
    public static readonly List<SongData> songs = new List<SongData>();
```

With:
```csharp
    public static readonly SongData[] Songs = BuildSongs();

    private static SongData[] BuildSongs()
    {
        var built = new SongData[SongName.Length];
        for (int i = 0; i < SongName.Length; i++)
        {
            built[i] = new SongData(
                i,
                SongName[i],
                Musician[i],
                SongURL[i],
                SBPM[i],
                Slevel[i],
                Shit[i],
                SongLong[i]
            );
        }
        return built;
    }
```

- [ ] **Step 3: Remove the static constructor and `InitializeSongs` method**

In `Assets/Scripts/GManager.cs`, delete lines 83–104:

```csharp
    static GManager()
    {
        InitializeSongs();
    }

    private static void InitializeSongs()
    {
        songs.Clear();
        for (int i = 0; i < SongName.Length; i++)
        {
            songs.Add(new SongData(
                i,
                SongName[i],
                Musician[i],
                SongURL[i],
                SBPM[i],
                Slevel[i],
                Shit[i],
                SongLong[i]
            ));
        }
    }
```

- [ ] **Step 4: Keep parallel arrays as transitional read-only (do not delete yet)**

Leave `SongName`, `Musician`, `SongURL`, `SBPM`, `Slevel`, `Shit`, `SongLong` arrays in place. Consumers migrate in Tasks 4–11. After all consumers migrate, these arrays are removed in Task 13.

Note: `totalSong` already equals `26` (matches `Songs.Length - 1`). Leave it as a static field for now; can be derived later.

- [ ] **Step 5: Update `GetSong` to use `Songs` array**

Replace:
```csharp
    public static SongData GetSong(int id)
    {
        if (id >= 0 && id < songs.Count)
        {
            return songs[id];
        }
        return null;
    }
```

With:
```csharp
    public static SongData GetSong(int id)
    {
        if (id >= 0 && id < Songs.Length)
        {
            return Songs[id];
        }
        return null;
    }
```

- [ ] **Step 6: Wait for Unity compilation**

Watch `mcp__UnityMCP__editor_state` (resource) until `is_compiling=false`.

- [ ] **Step 7: Run SongDataTests, expect new tests pass, old test (TestSongRepositoryInitialization) compile-fails**

Use MCP:
```python
mcp__UnityMCP__run_tests(mode="EditMode", test_names=["SongDataTests.TestSongsArrayNotEmpty", "SongDataTests.TestSongsIndexMatchesId", "SongDataTests.TestSongsIndexZeroIsPlaceholder", "SongDataTests.TestSongIdOneTitleUnchanged", "SongDataTests.TestGetSongValidAndInvalidId", "SongDataTests.TestTotalSongDerivedFromSongsLength", "SongDataTests.TestVideoIdMigrationMatchesPriorSongUrl", "SongDataTests.TestResetSession"])
```

Expected: `TestSongsArrayNotEmpty`, `TestSongsIndexMatchesId`, `TestSongsIndexZeroIsPlaceholder`, `TestSongIdOneTitleUnchanged`, `TestGetSongValidAndInvalidId`, `TestTotalSongDerivedFromSongsLength`, `TestVideoIdMigrationMatchesPriorSongUrl`, `TestResetSession` all PASS. (The old `TestSongRepositoryInitialization` no longer exists in the new test file.)

- [ ] **Step 8: Check console for compilation errors**

```python
mcp__UnityMCP__read_console(action="get", types=["error"], count=20)
```

Expected: only errors from consumer files (MusicManager.cs, etc.) referencing `songs.Count` or `youtubeUrl` — those are fixed in Tasks 4–11. Zero errors in `GManager.cs`.

- [ ] **Step 9: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Scripts/GManager.cs
git commit -m "refactor: consolidate song data into SongData[] Songs array

- Rename SongData.youtubeUrl to SongData.videoId
- Replace List<SongData> songs with SongData[] Songs
- Remove InitializeSongs static initializer; inline construction in BuildSongs
- Update GetSong to index Songs array
- Keep parallel arrays (SongName/Musician/SongURL/etc.) for migration period"
```

---

## Task 4: Update `MusicManager.cs` to use `Songs[]`

**Files:**
- Modify: `Assets/Scripts/MusicManager.cs`

- [ ] **Step 1: Read current file**

Already on disk; confirm content via `Read` tool if needed.

- [ ] **Step 2: Replace parallel-array references with `Songs[]` accesses**

In `Assets/Scripts/MusicManager.cs`:

Replace line 30:
```csharp
        songName = GManager.SongName[GManager.songID];
```

With:
```csharp
        songName = GManager.Songs[GManager.songID].title;
```

Replace line 40:
```csharp
        for(int i = 0; i < GManager.SongURL.Length; i++)
```

With:
```csharp
        for(int i = 0; i < GManager.Songs.Length; i++)
```

Replace line 99:
```csharp
            if (GManager.step_time >= GManager.SongLong[GManager.songID]-1.0f)
```

With:
```csharp
            if (GManager.step_time >= GManager.Songs[GManager.songID].duration-1.0f)
```

- [ ] **Step 3: Wait for compilation**

Read `mcp__UnityMCP__editor/state` until `is_compiling=false`.

- [ ] **Step 4: Read console for errors**

```python
mcp__UnityMCP__read_console(action="get", types=["error"], count=10)
```

Expected: zero errors in `MusicManager.cs`.

- [ ] **Step 5: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Scripts/MusicManager.cs
git commit -m "refactor: update MusicManager to use Songs[] array"
```

---

## Task 5: Update `GameSceneManager.cs` to use `Songs[]` + remove dead code

**Files:**
- Modify: `Assets/Scripts/GameSceneManager.cs`

- [ ] **Step 1: Read current file to find dead commented-out code**

Read `Assets/Scripts/GameSceneManager.cs`. Identify all `//` blocks of code that are commented out (i.e., unreachable lines like `// audioSource.Play();`).

- [ ] **Step 2: Replace `SongName[]` reference at line 107**

```csharp
        songName = GManager.SongName[GManager.songID];
```

With:
```csharp
        songName = GManager.Songs[GManager.songID].title;
```

- [ ] **Step 3: Remove dead commented-out code blocks**

Search the file for `^//` lines that are NOT documentation comments (i.e., lines that look like `// someCode();`). Delete them. Keep `///` XML doc comments and `//` lines that explain intent.

For example, if you find:
```csharp
    // audioS.clip = Music;
    // audioS.Play();
    // kakusi.SetActive(false);
```
Delete these three lines.

- [ ] **Step 4: Wait for compilation**

Read `mcp__UnityMCP__editor/state` until `is_compiling=false`.

- [ ] **Step 5: Read console for errors**

```python
mcp__UnityMCP__read_console(action="get", types=["error"], count=10)
```

Expected: zero errors in `GameSceneManager.cs`.

- [ ] **Step 6: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Scripts/GameSceneManager.cs
git commit -m "refactor: update GameSceneManager to use Songs[] and remove dead code"
```

---

## Task 6: Update `NotesManager.cs` to use `Songs[]`

**Files:**
- Modify: `Assets/Scripts/NotesManager.cs`

- [ ] **Step 1: Replace `SongName[]` reference at line 42**

```csharp
        songName = GManager.SongName[GManager.songID];
```

With:
```csharp
        songName = GManager.Songs[GManager.songID].title;
```

- [ ] **Step 2: Wait for compilation, check console, commit**

```python
mcp__UnityMCP__read_console(action="get", types=["error"], count=10)
```

```bash
git add Assets/Scripts/NotesManager.cs
git commit -m "refactor: update NotesManager to use Songs[] array"
```

---

## Task 7: Update `SelectSceneManager.cs` to use `Songs[]`

**Files:**
- Modify: `Assets/Scripts/SelectSceneManager.cs`

- [ ] **Step 1: Replace all `GManager.SongName[sID]` / `Musician` / `SBPM` / `Shit` / `Slevel` / `SongURL` / `totalSong` references**

For lines 106, 130–134, 146, 154, 184, 196, 239, 254, 256, apply:

Line 106 — `int total = GManager.totalSong;` → `int total = GManager.Songs.Length - 1;`

Line 130: `songNames[i].text = GManager.SongName[sID];` → `songNames[i].text = GManager.Songs[sID].title;`

Line 131: `musicians[i].text = GManager.Musician[sID];` → `musicians[i].text = GManager.Songs[sID].musician;`

Line 132: `bpms[i].text = GManager.SBPM[sID].ToString();` → `bpms[i].text = GManager.Songs[sID].bpm.ToString();`

Line 133: `hits[i].text = GManager.Shit[sID].ToString();` → `hits[i].text = GManager.Songs[sID].totalHits.ToString();`

Line 134: `levels[i].text = GManager.Slevel[sID].ToString();` → `levels[i].text = GManager.Songs[sID].level.ToString();`

Line 146: `StartCoroutine(FetchThumbnail(GManager.SongURL[sID], i));` → `StartCoroutine(FetchThumbnail(GManager.Songs[sID].videoId, i));`

Line 154: `for (int i = 1; i <= GManager.totalSong; i++)` → `for (int i = 1; i <= GManager.Songs.Length - 1; i++)`

Line 184: `if (GManager.songID > GManager.totalSong)` → `if (GManager.songID > GManager.Songs.Length - 1)`

Line 196: `GManager.songID = GManager.totalSong;` → `GManager.songID = GManager.Songs.Length - 1;`

Line 239: `GManager.songID += GManager.totalSong;` → `GManager.songID += GManager.Songs.Length - 1;`

Line 254: `if (GManager.songID > GManager.totalSong)` → `if (GManager.songID > GManager.Songs.Length - 1)`

Line 256: `GManager.songID -= GManager.totalSong;` → `GManager.songID -= GManager.Songs.Length - 1;`

- [ ] **Step 2: Verify compilation, commit**

```python
mcp__UnityMCP__read_console(action="get", types=["error"], count=10)
```

```bash
git add Assets/Scripts/SelectSceneManager.cs
git commit -m "refactor: update SelectSceneManager to use Songs[] array"
```

---

## Task 8: Update `OptionSceneManager.cs` to use `Songs[]`

**Files:**
- Modify: `Assets/Scripts/OptionSceneManager.cs`

- [ ] **Step 1: Replace references**

Line 127: `jsonPath = "Assets/Resources/" + GManager.SongName[I] + ".json";` → `jsonPath = "Assets/Resources/" + GManager.Songs[I].title + ".json";`

Line 128: `jsonfileName = GManager.SongName[I].ToString();` → `jsonfileName = GManager.Songs[I].title;`

Line 142: same as 127.

Line 143: same as 128.

Line 169: `for (int i = 1; i <= GManager.totalSong; i++)` → `for (int i = 1; i <= GManager.Songs.Length - 1; i++)`

Line 181: same as 169.

- [ ] **Step 2: Verify compilation, commit**

```bash
git add Assets/Scripts/OptionSceneManager.cs
git commit -m "refactor: update OptionSceneManager to use Songs[] array"
```

---

## Task 9: Update `ResultSceneManager.cs` to use `Songs[]`

**Files:**
- Modify: `Assets/Scripts/ResultSceneManager.cs`

- [ ] **Step 1: Replace references**

Line 41: `SongName.text = GManager.SongName[GManager.songID];` → `SongName.text = GManager.Songs[GManager.songID].title;`

Line 42: `Musician.text = GManager.Musician[GManager.songID];` → `Musician.text = GManager.Songs[GManager.songID].musician;`

Line 43: `BPM.text = GManager.SBPM[GManager.songID].ToString();` → `BPM.text = GManager.Songs[GManager.songID].bpm.ToString();`

Line 46: `TotalHit.text = GManager.Shit[GManager.songID].ToString();` → `TotalHit.text = GManager.Songs[GManager.songID].totalHits.ToString();`

Line 47: `Songlevel.text = GManager.Slevel[GManager.songID].ToString();` → `Songlevel.text = GManager.Songs[GManager.songID].level.ToString();`

Line 80: `StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID], 0));` → `StartCoroutine(FetchThumbnail(GManager.Songs[GManager.songID].videoId, 0));`

- [ ] **Step 2: Verify compilation, commit**

```bash
git add Assets/Scripts/ResultSceneManager.cs
git commit -m "refactor: update ResultSceneManager to use Songs[] array"
```

---

## Task 10: Update `BGFlash.cs` to use `Songs[]`

**Files:**
- Modify: `Assets/Scripts/BGFlash.cs`

- [ ] **Step 1: Replace reference at line 22**

```csharp
        Beat = (60/(float)GManager.SBPM[GManager.songID]);
```

With:
```csharp
        Beat = (60/(float)GManager.Songs[GManager.songID].bpm);
```

- [ ] **Step 2: Verify compilation, commit**

```bash
git add Assets/Scripts/BGFlash.cs
git commit -m "refactor: update BGFlash to use Songs[] array"
```

---

## Task 11: Update `TipingSceneManager.cs` to use `Songs[]`

**Files:**
- Modify: `Assets/Scripts/TipingSceneManager.cs`

- [ ] **Step 1: Replace references**

Line 39: `Load(GManager.SongName[GManager.songID]);` → `Load(GManager.Songs[GManager.songID].title);`

Line 40: `furiganaText.text = GManager.SongName[GManager.songID];` → `furiganaText.text = GManager.Songs[GManager.songID].title;`

Line 45: `Music = (AudioClip)Resources.Load("Musics/" + GManager.SongName[GManager.songID]);` → `Music = (AudioClip)Resources.Load("Musics/" + GManager.Songs[GManager.songID].title);`

Line 61: `if (MusicCount >= GManager.SongLong[GManager.songID])` → `if (MusicCount >= GManager.Songs[GManager.songID].duration)`

Line 77: same as 61.

- [ ] **Step 2: Verify compilation, commit**

```bash
git add Assets/Scripts/TipingSceneManager.cs
git commit -m "refactor: update TipingSceneManager to use Songs[] array"
```

---

## Task 12: Update `MakeFileSceneManager.cs` to use `Songs[]` + new song addition procedure

**Files:**
- Modify: `Assets/Scripts/MakeFileSceneManager.cs`

- [ ] **Step 1: Read the current bottom-of-file comment block documenting song-add steps**

Read `Assets/Scripts/MakeFileSceneManager.cs` to identify the comment block describing the 5-step song addition procedure (referenced in `CLAUDE.md`).

- [ ] **Step 2: Update the song-add procedure comment to use `Songs[]`**

Find the comment block (likely `// 1. ... ` through `// 5. ...`). Rewrite step 3 from:

```
// 3. Update the parallel arrays in GManager: SongName, Musician, SongURL, SBPM, Slevel, Shit, SongLong, Hiscore (length), totalSong.
```

To:

```
// 3. Add a SongData entry to the Songs array literal in GManager.cs (BuildSongs() reads SongName/Musician/SongURL/etc. arrays — add to those too, OR refactor to a single inline literal).
```

For this task, keep the parallel arrays as the source for now (Step 3 in spec implementation order is the formal consolidation; here we just update the comment to reflect Songs[] as the canonical API).

- [ ] **Step 3: Verify any code in this file that references parallel arrays**

If `MakeFileSceneManager.cs` references `SongName[]`, `Musician[]`, etc. directly (other than the comment block), update those to `Songs[].title`, `Songs[].musician`, etc.

- [ ] **Step 4: Verify compilation, commit**

```bash
git add Assets/Scripts/MakeFileSceneManager.cs
git commit -m "refactor: update MakeFileSceneManager to use Songs[] and document new procedure"
```

---

## Task 13: Verify compilation + tests pass after Phase 1

- [ ] **Step 1: Wait for compilation**

Read `mcp__UnityMCP__editor/state` until `is_compiling=false`.

- [ ] **Step 2: Check console**

```python
mcp__UnityMCP__read_console(action="get", types=["error"], count=20)
```

Expected: zero errors.

- [ ] **Step 3: Run all EditMode tests**

```python
mcp__UnityMCP__run_tests(mode="EditMode", include_failed_tests=True)
```

Expected: all tests pass (including `SongDataTests` and `JudgeLogicTests`).

- [ ] **Step 4: (Optional) Remove parallel arrays if no consumer remains**

After Tasks 4–12, search for any remaining `GManager.SongName[`, `GManager.Musician[`, `GManager.SongURL[`, `GManager.SBPM[`, `GManager.Slevel[`, `GManager.Shit[`, `GManager.SongLong[` references:

```bash
cd C:\Users\rebui\Desktop\FlashBeat
grep -rn "GManager\.SongName\[\|GManager\.Musician\[\|GManager\.SongURL\[\|GManager\.SBPM\[\|GManager\.Slevel\[\|GManager\.Shit\[\|GManager\.SongLong\[" Assets/Scripts Assets/Tests
```

If only `GManager.cs` itself contains them (i.e., the array declarations), and `Songs[]` is now the canonical source via `BuildSongs()`, then remove the parallel arrays from `GManager.cs` and inline the data into `Songs[]`:

In `Assets/Scripts/GManager.cs`, replace the entire `Songs` declaration block (parallel arrays + `BuildSongs`) with:

```csharp
    public static readonly SongData[] Songs = new SongData[]
    {
        new SongData(0,  "noSong", "no", "VIjqWffacio", 100, 10, 1000, 10000.0f),
        new SongData(1,  "ロストアンブレラ", "稲葉曇", "DeKLpgzh-qQ", 274, 8, 268, 89.0f),
        new SongData(2,  "まにまに", "r-906", "9O2VyUM5MlQ", 174, 13, 383, 48f),
        new SongData(3,  "Who", "Azari", "8JXiXt0D6tw", 128, 5, 139, 68.0f),
        new SongData(4,  "ツイッターランド", "STEAKA", "e_qQEU_uGjw", 142, 15, 130, 44.0f),
        new SongData(5,  "終焉逃避行", "柊マグネタイト", "yVi3mhLr0uU", 147, 14, 296, 103.0f),
        new SongData(6,  "エウタナシア", "ど～ぱみん", "xqYOI7OD9aE", 127, 18, 243, 90.0f),
        new SongData(7,  "Chartreuse", "t+pazolite", "5BlSQpejMTw", 180, 19, 349, 80.0f),
        new SongData(8,  "Psyched", "t+pazolite", "3mufQ1Tt844", 150, 19, 257, 87.0f),
        new SongData(9,  "Dogbite", "t+pazolite", "3s4y8B6Je-4", 195, 19, 603, 121f),
        new SongData(10, "チュートリアル", "LeaF", "NAeuRhLaqaQ", 95, 0, 53, 69f),
        new SongData(11, "狂喜蘭舞", "LeaF", "s-0HVBCEMZk", 176, 17, 360, 79f),
        new SongData(12, "Poison", "LeaF", "3C5zNU2JCdc", 120, 10, 82, 55f),
        new SongData(13, "セルフィー", "たぴぼ!!", "bUM5erw1vRs", 195, 16, 254, 80f),
        new SongData(14, "4th smile", "LeaF", "zKbc-kVdtcI", 140, 5, 105, 69f),
        new SongData(15, "Calamity", "LeaF", "n-2GnXKvIOU", 200, 7, 111, 52f),
        new SongData(16, "パノプティコン", "r-906", "_-Vd0ZGB-lo", 174, 10, 316, 123f),
        new SongData(17, "GURU", "ジン", "smYLMgfCD5o", 138, 17, 295, 85f),
        new SongData(18, "阿吽のビーツ", "羽生まゐご", "SiqjnFhLq2U", 206, 25, 262, 67f),
        new SongData(19, "混沌ブギ", "jon-YAKITORY", "1Swg-aBO9eY", 95, 15, 292, 86f),
        new SongData(20, "春嵐", "john", "pUH9vCsvq08", 140, 30, 315, 60f),
        new SongData(21, "ビビビビ", "フロクロ", "sWOvhZBS9IA", 175, 15, 184, 74f),
        new SongData(22, "灰Φ倶楽部", "煮ル果実", "_qj9ftYCNyw", 143, 20, 275, 75f),
        new SongData(23, "オーバーライド", "吉田夜世", "LLjfal8jCYI", 102, 21, 250, 68f),
        new SongData(24, "イガク", "原口沙輔", "F38EuG2dAyM", 170, 10, 196, 81f),
        new SongData(25, "テレキャスタービーボーイ", "すりぃ", "i-DZukWFR64", 182, 10, 163, 69f),
        new SongData(26, "一龠", "ァネイロ", "iWzUxFQQAKY", 176, 24, 193, 77f),
    };
```

Also delete the `SongName`, `Musician`, `SongURL`, `SBPM`, `Slevel`, `Shit`, `SongLong` static array fields.

- [ ] **Step 5: Update test that referenced `SongURL`**

In `Assets/Tests/Editor/SongDataTests.cs`, the test `TestVideoIdMigrationMatchesPriorSongUrl` references `GManager.SongURL[i]`. After removal, this test must change to:

```csharp
    [Test]
    public void TestVideoIdMigrationMatchesPriorSongUrl()
    {
        // Sanity: videoId is non-null and non-empty for every real song.
        for (int i = 1; i < GManager.Songs.Length; i++)
        {
            Assert.IsFalse(string.IsNullOrEmpty(GManager.Songs[i].videoId), $"Songs[{i}].videoId must be set.");
            Assert.AreEqual(11, GManager.Songs[i].videoId.Length, $"Songs[{i}].videoId should be 11-char YouTube ID.");
        }
    }
```

- [ ] **Step 6: Wait for compilation, run tests, commit**

```python
mcp__UnityMCP__read_console(action="get", types=["error"], count=10)
mcp__UnityMCP__run_tests(mode="EditMode")
```

```bash
git add Assets/Scripts/GManager.cs Assets/Tests/Editor/SongDataTests.cs
git commit -m "refactor: inline Songs[] data and remove parallel arrays"
```

---

# Phase 2: YouTube Path Verification

The previous session already wired up InvidiousVideoPlayer components. This phase verifies each piece is correct. If any piece is missing, fix it.

## Task 14: Verify `InvidiousVideoPlayer` is on each child GameObject in GameScene

**Files:**
- Verify: `Assets/Scenes/GameScene.unity`

- [ ] **Step 1: Read editor state to confirm scene is loaded**

```python
mcp__UnityMCP__manage_scene(action="get_active")
```

Expected: path is `Assets/Scenes/GameScene.unity`. If not, load it:

```python
mcp__UnityMCP__manage_scene(action="load", path="Assets/Scenes/GameScene.unity")
```

- [ ] **Step 2: List child GameObjects named "0" through "26"**

```python
mcp__UnityMCP__find_gameobjects(search_term="[0-9]", search_method="by_name", page_size=50)
```

Verify: at least 27 results (0 through 26).

- [ ] **Step 3: For each numeric child, verify InvidiousVideoPlayer component is present**

Pick one child (e.g., ID `1`):

```python
mcp__UnityMCP__find_gameobjects(search_term="1", search_method="by_name")
```

Read its components via `mcp__UnityMCP__scene/gameobject/{id}/components`.

Verify: `InvidiousVideoPlayer` is in the component list AND `VideoId` matches `GManager.Songs[1].videoId`.

If any child is missing the component or has wrong VideoId, fix it:

```python
mcp__UnityMCP__manage_components(
    action="set_property",
    target=<child_id>,
    component_type="InvidiousVideoPlayer",
    property="VideoId",
    value="DeKLpgzh-qQ"  # Songs[1].videoId
)
```

- [ ] **Step 4: Spot-check 3 more children (e.g., IDs 5, 15, 26)**

Repeat Step 3 for IDs 5, 15, 26. Verify VideoId matches.

- [ ] **Step 5: No commit needed if no fixes; commit fixes if any**

If you modified components:

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Scenes/GameScene.unity
git commit -m "fix: ensure InvidiousVideoPlayer components on GameScene children 0-26"
```

---

## Task 15: Verify `InvidiousInstance` GameObject exists

**Files:**
- Verify: `Assets/Scenes/GameScene.unity`

- [ ] **Step 1: Search for InvidiousInstance**

```python
mcp__UnityMCP__find_gameobjects(search_term="InvidiousInstance", search_method="by_name")
```

Expected: exactly 1 result.

- [ ] **Step 2: If missing, create it**

```python
mcp__UnityMCP__manage_gameobject(
    action="create",
    name="InvidiousInstance",
    components_to_add=["YoutubePlayer.Components.InvidiousInstance"]
)
```

- [ ] **Step 3: Commit if added**

```bash
git add Assets/Scenes/GameScene.unity
git commit -m "fix: ensure InvidiousInstance GameObject exists in GameScene"
```

---

## Task 16: Verify YoutubePlayer prefabs have `InvidiousVideoPlayer` configured

**Files:**
- Verify: `Assets/Prefab/YoutubePlayer.prefab`, `Assets/Prefab/YoutubePlayer 1.prefab`

- [ ] **Step 1: Read prefab contents**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
head -10 "Assets/Prefab/YoutubePlayer.prefab"
```

Verify: `m_Script: {fileID: 11500000, guid: 6f51438de7493496c87c7a2918e755e0, type: 3}` is present (InvidiousVideoPlayer script GUID).

- [ ] **Step 2: Verify VideoPlayer.m_PlayOnAwake = 0 and m_Url is empty**

```bash
grep -A 5 "m_PlayOnAwake\|m_Url:" "Assets/Prefab/YoutubePlayer.prefab"
```

Expected: `m_PlayOnAwake: 0`, `m_Url:` empty.

- [ ] **Step 3: Repeat for YoutubePlayer 1.prefab**

- [ ] **Step 4: No commit if verified; commit fixes if any**

```bash
git add "Assets/Prefab/YoutubePlayer.prefab" "Assets/Prefab/YoutubePlayer 1.prefab"
git commit -m "fix: ensure YoutubePlayer prefabs configured for InvidiousVideoPlayer"
```

---

## Task 17: Verify `MusicManager` `YPlayer` path is the primary playback

**Files:**
- Verify: `Assets/Scripts/MusicManager.cs`

- [ ] **Step 1: Confirm MusicManager has the YPlayer + InvidiousVideoPlayer path**

Read `Assets/Scripts/MusicManager.cs`. Confirm:
- `[SerializeField] GameObject[] YPlayer;` exists.
- `Start()` activates `YPlayer[GManager.songID]` and calls `PrepareVideoCoroutine`.
- `Update()` calls `PlayVideoCoroutine` on Space press.
- `PlayVideoCoroutine` uses `InvidiousVideoPlayer.PlayVideoAsync()` if present, else falls back to `VPlayer[songIdx].Play()`.

- [ ] **Step 2: Confirm `FlashBeat.asmdef` references YoutubePlayer**

```bash
cat Assets/Scripts/FlashBeat.asmdef
```

Expected: `"references": ["...","YoutubePlayer"]` (or similar reference name).

- [ ] **Step 3: Confirm `manifest.json` has `com.ibicha.youtube-player`**

```bash
grep "youtube-player" Packages/manifest.json
```

Expected: matches `"com.ibicha.youtube-player": "..."`.

- [ ] **Step 4: No code changes expected; no commit**

If any piece is missing, fix it. Otherwise, mark complete.

---

# Phase 3: Code Name Changes

## Task 18: Rename `Light.cs` → `LaneFlash.cs`

**Files:**
- Rename: `Assets/Scripts/Light.cs` → `Assets/Scripts/LaneFlash.cs`
- Rename class: `Light` → `LaneFlash`
- Rename field: `Lanesnum` → `laneNumber` (and `alfa` → `alpha` if present)
- Add: `FormerlySerializedAs` attributes for Inspector preservation
- Verify: any scene reference to `Light` MonoBehaviour

- [ ] **Step 1: Find scene/prefab references to the `Light` MonoBehaviour**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
grep -rln "m_Script.*Light" Assets/Scenes Assets/Prefab 2>/dev/null | head -20
```

Note the file IDs of any `Light` MonoBehaviour references.

- [ ] **Step 2: Read current `Light.cs`**

Read `Assets/Scripts/Light.cs` to see class fields.

- [ ] **Step 3: Create `LaneFlash.cs` with renamed class and fields**

Create `Assets/Scripts/LaneFlash.cs` with content:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class LaneFlash : MonoBehaviour
{
    [FormerlySerializedAs("Lanesnum")]
    public int laneNumber;

    [FormerlySerializedAs("alfa")]
    public float alpha;

    // Preserve other fields exactly; rename as appropriate per existing code.
    // Add FormerlySerializedAs("oldName") above each renamed field.
}
```

(Mirror all fields from `Light.cs`. Add `[FormerlySerializedAs("oldName")]` above each renamed field to preserve Inspector values.)

- [ ] **Step 4: Delete old `Light.cs`**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git rm Assets/Scripts/Light.cs Assets/Scripts/Light.cs.meta 2>/dev/null
```

- [ ] **Step 5: Update scene/prefab references from `Light` script GUID to `LaneFlash` script GUID**

Open `Assets/Scenes/GameScene.unity` (and any prefab) and find every entry referencing the `Light` MonoBehaviour script. Replace its `m_Script: {fileID: 11500000, guid: <old-guid>, type: 3}` line with the new `LaneFlash` script GUID.

To find the new GUID:

```bash
cat Assets/Scripts/LaneFlash.cs.meta | grep guid
```

Update each scene/prefab reference. The `[FormerlySerializedAs]` attributes preserve field values, so only the script GUID needs updating.

- [ ] **Step 6: Wait for compilation, check console**

```python
mcp__UnityMCP__read_console(action="get", types=["error"], count=20)
```

Expected: zero errors. (Any missing-reference warnings should disappear once `LaneFlash` script is loaded.)

- [ ] **Step 7: Run EditMode tests**

```python
mcp__UnityMCP__run_tests(mode="EditMode")
```

Expected: all pass.

- [ ] **Step 8: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Scripts/LaneFlash.cs Assets/Scripts/Light.cs.meta Assets/Scenes/GameScene.unity
git commit -m "refactor: rename Light MonoBehaviour to LaneFlash to avoid UnityEngine.Light confusion

- Rename class Light -> LaneFlash
- Rename Lanesnum -> laneNumber, alfa -> alpha (FormerlySerializedAs preserves Inspector values)
- Update scene/prefab script GUID references"
```

---

## Task 19: Rename `TipingSceneManager.cs` → `TypingSceneManager.cs`

**Files:**
- Rename: `Assets/Scripts/TipingSceneManager.cs` → `Assets/Scripts/TypingSceneManager.cs`
- Rename class: `TipingSceneManager` → `TypingSceneManager`

- [ ] **Step 1: Find references to the class name**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
grep -rln "TipingSceneManager" Assets
```

Note files that reference it.

- [ ] **Step 2: Read current file**

Read `Assets/Scripts/TipingSceneManager.cs`.

- [ ] **Step 3: Create `TypingSceneManager.cs`**

Create `Assets/Scripts/TypingSceneManager.cs` with the same content but `public class TypingSceneManager : MonoBehaviour` instead of `TipingSceneManager`.

- [ ] **Step 4: Delete old `TipingSceneManager.cs`**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git rm Assets/Scripts/TipingSceneManager.cs Assets/Scripts/TipingSceneManager.cs.meta
```

- [ ] **Step 5: Update references in any other file that mentions `TipingSceneManager`**

For each file from Step 1 (other than the deleted one), replace `TipingSceneManager` with `TypingSceneManager`.

- [ ] **Step 6: Wait for compilation, check console, run tests**

- [ ] **Step 7: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Scripts/TypingSceneManager.cs Assets/Scripts/TipingSceneManager.cs.meta <other-touched-files>
git commit -m "refactor: rename TipingSceneManager to TypingSceneManager (fix typo)"
```

---

## Task 20: Rename `TipingScene].unity` → `TypingScene.unity`

**Files:**
- Rename: `Assets/Scenes/TipingScene].unity` → `Assets/Scenes/TypingScene.unity`
- Rename: `Assets/Scenes/TipingScene].unity.meta` → `Assets/Scenes/TypingScene.unity.meta`
- Verify: Build Settings still reference it correctly

- [ ] **Step 1: Find the file**

```bash
ls "Assets/Scenes/" | grep -i tip
```

- [ ] **Step 2: Use AssetDatabase to rename (preserves GUID)**

Via Unity Editor menu: select `TipingScene].unity` in Project window, press F2, rename to `TypingScene.unity`.

Or via MCP:

```python
mcp__UnityMCP__manage_asset(action="rename", path="Assets/Scenes/TipingScene].unity", destination="Assets/Scenes/TypingScene.unity")
```

- [ ] **Step 3: Verify Build Settings reference the renamed file**

```python
mcp__UnityMCP__manage_build(action="get_build_settings")
```

If `TipingScene].unity` still appears, remove it and add `TypingScene.unity`:

```python
mcp__UnityMCP__manage_build(action="scenes", scenes=["Assets/Scenes/TypingScene.unity"])
```

- [ ] **Step 4: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Scenes/TypingScene.unity Assets/Scenes/TypingScene.unity.meta ProjectSettings/EditorBuildSettings.asset
git commit -m "refactor: rename TipingScene].unity to TypingScene.unity (fix typo)"
```

---

# Phase 4: BuildScript + Dead Code

## Task 21: Fix `BuildScript.cs` `OpeningScene.unity` reference

**Files:**
- Modify: `Assets/Editor/BuildScript.cs`

- [ ] **Step 1: Locate the bad reference**

```bash
grep -n "OpeningScene\|Opening.unity" Assets/Editor/BuildScript.cs
```

- [ ] **Step 2: Replace `OpeningScene.unity` with `Opening.unity`**

Edit `Assets/Editor/BuildScript.cs` to replace any `"Assets/Scenes/OpeningScene.unity"` string with `"Assets/Scenes/Opening.unity"`.

- [ ] **Step 3: Verify no other broken scene paths**

Read full `BuildScript.cs`, check all `Assets/Scenes/*.unity` references against actual files in `Assets/Scenes/`. Fix any that don't match.

- [ ] **Step 4: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Editor/BuildScript.cs
git commit -m "fix: correct OpeningScene.unity -> Opening.unity path in BuildScript"
```

---

## Task 22: Remove dead commented-out code in `GameSceneManager.cs`

**Files:**
- Modify: `Assets/Scripts/GameSceneManager.cs`

- [ ] **Step 1: Find commented-out code lines**

```bash
grep -n "^[[:space:]]*//" Assets/Scripts/GameSceneManager.cs | head -30
```

Identify lines that look like commented-out code (e.g., `// audioS.Play();`) vs documentation comments (e.g., `// Load resources for ...`).

- [ ] **Step 2: Delete dead commented-out code**

Remove only the lines that are clearly unreachable commented-out code. Keep explanatory comments.

- [ ] **Step 3: Verify compilation, commit**

```bash
git add Assets/Scripts/GameSceneManager.cs
git commit -m "chore: remove dead commented-out code in GameSceneManager"
```

---

# Phase 5: Asset Organization

## Task 23: Move `Assets/Material/` → `Assets/Art/Materials/`

**Files:**
- Move: `Assets/Material/` (whole directory) → `Assets/Art/Materials/`

- [ ] **Step 1: Create new directory**

Via MCP:

```python
mcp__UnityMCP__manage_asset(action="create_folder", path="Assets/Art")
mcp__UnityMCP__manage_asset(action="create_folder", path="Assets/Art/Materials")
```

- [ ] **Step 2: Move each material asset**

For each `.mat` file in `Assets/Material/`:

```python
mcp__UnityMCP__manage_asset(action="move", path="Assets/Material/ゲーム背景.mat", destination="Assets/Art/Materials/ゲーム背景.mat")
```

Repeat for all 8 materials (per Material renames table in spec):
- `ゲーム背景.mat`, `ノーツ.mat`, `パーティクル.mat`, `ライト.mat`, `レーン.mat`, `判定線.mat`, `背景.mat`, `黒半透明.mat`

- [ ] **Step 3: Rename materials to English**

After moving (or in one combined step), rename:

```python
mcp__UnityMCP__manage_asset(action="rename", path="Assets/Art/Materials/ゲーム背景.mat", destination="Assets/Art/Materials/GameBackground.mat")
```

Repeat for all 8 per the Material renames table.

- [ ] **Step 4: Verify scene/prefab references update automatically**

Unity tracks references by GUID. AssetDatabase move + rename preserves GUID, so scene/prefab references should still resolve.

Verify by opening `GameScene.unity` and checking materials are not pink (missing). Read console:

```python
mcp__UnityMCP__read_console(action="get", types=["warning","error"], count=20)
```

- [ ] **Step 5: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Art Assets/Material Assets/Material.meta
git commit -m "refactor: move Materials to Assets/Art/Materials with English filenames"
```

---

## Task 24: Move `Assets/Prefab/` → `Assets/Art/Prefabs/`

**Files:**
- Move: `Assets/Prefab/` (whole directory) → `Assets/Art/Prefabs/`

- [ ] **Step 1: Create directory**

```python
mcp__UnityMCP__manage_asset(action="create_folder", path="Assets/Art/Prefabs")
```

- [ ] **Step 2: Move + rename each prefab**

For each of the 7 prefabs (per Prefab renames table in spec), do `move` then `rename`:

- `スクリーン.prefab` → `Screen.prefab`
- `ノーツ.prefab` → `Note.prefab`
- `Image (1).prefab` → `ScreenVideo.prefab`
- `Image (1) 1.prefab` → `FullscreenOverlay.prefab`
- `BackGround.prefab` → `Background.prefab`
- `Particle System.prefab` → `ParticleSystem.prefab`
- `Directional Light.prefab` → `DirectionalLight.prefab`

(`Canvas`, `Bad`, `Great`, `Perfect`, `Miss`, `Hiscore`, `SongInfoPanel`, `YoutubePlayer` remain in place / move to `Art/Prefabs/` without rename.)

- [ ] **Step 3: Verify scene references**

```python
mcp__UnityMCP__read_console(action="get", types=["warning","error"], count=20)
```

- [ ] **Step 4: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Art Assets/Prefab
git commit -m "refactor: move Prefabs to Assets/Art/Prefabs with English filenames"
```

---

## Task 25: Move font assets → `Assets/Fonts/Japanese/`

**Files:**
- Move:
  - `Assets/NotoSansJP-Medium.ttf`
  - `Assets/NotoSansJP-Medium SDF.asset`
  - `Assets/YuGothB.ttc`
  - `Assets/YuGothB SDF.asset`
  - `Assets/japanese_full.txt`
- Destination: `Assets/Fonts/Japanese/`

- [ ] **Step 1: Create directory**

```python
mcp__UnityMCP__manage_asset(action="create_folder", path="Assets/Fonts")
mcp__UnityMCP__manage_asset(action="create_folder", path="Assets/Fonts/Japanese")
```

- [ ] **Step 2: Move each font asset**

```python
mcp__UnityMCP__manage_asset(action="move", path="Assets/NotoSansJP-Medium.ttf", destination="Assets/Fonts/Japanese/NotoSansJP-Medium.ttf")
mcp__UnityMCP__manage_asset(action="move", path="Assets/NotoSansJP-Medium SDF.asset", destination="Assets/Fonts/Japanese/NotoSansJP-Medium SDF.asset")
mcp__UnityMCP__manage_asset(action="move", path="Assets/YuGothB.ttc", destination="Assets/Fonts/Japanese/YuGothB.ttc")
mcp__UnityMCP__manage_asset(action="move", path="Assets/YuGothB SDF.asset", destination="Assets/Fonts/Japanese/YuGothB SDF.asset")
mcp__UnityMCP__manage_asset(action="move", path="Assets/japanese_full.txt", destination="Assets/Fonts/Japanese/japanese_full.txt")
```

- [ ] **Step 3: Verify TextMesh Pro references resolve**

Open `GameScene.unity`. Confirm TextMesh Pro text is still rendering correctly (visual check or screenshot).

- [ ] **Step 4: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Fonts Assets/NotoSansJP-Medium.ttf Assets/NotoSansJP-Medium SDF.asset Assets/YuGothB.ttc "Assets/YuGothB SDF.asset" Assets/japanese_full.txt
git commit -m "refactor: move Japanese font assets to Assets/Fonts/Japanese/"
```

---

# Phase 6: Scene Cleanup

## Task 26: Rename Hierarchy objects in GameScene

**Files:**
- Modify: `Assets/Scenes/GameScene.unity`

- [ ] **Step 1: Open GameScene in Unity Editor (or load via MCP)**

```python
mcp__UnityMCP__manage_scene(action="load", path="Assets/Scenes/GameScene.unity")
```

- [ ] **Step 2: Rename each Japanese-named object**

For each row in the Hierarchy renames table:

```python
mcp__UnityMCP__manage_gameobject(action="modify", target="ライト", new_name="LaneLights")
mcp__UnityMCP__manage_gameobject(action="modify", target="ライト1", new_name="LaneLight1")
# ... repeat for ライト2-8
mcp__UnityMCP__manage_gameobject(action="modify", target="スコアゲージ", new_name="ScoreGauge")
mcp__UnityMCP__manage_gameobject(action="modify", target="判定線", new_name="JudgeLine")
mcp__UnityMCP__manage_gameobject(action="modify", target="レーン", new_name="Lane")
mcp__UnityMCP__manage_gameobject(action="modify", target="問題", new_name="QuestionText")
mcp__UnityMCP__manage_gameobject(action="modify", target="ふりがな", new_name="FuriganaText")
mcp__UnityMCP__manage_gameobject(action="modify", target="ローマ字", new_name="RomajiText")
mcp__UnityMCP__manage_gameobject(action="modify", target="スクリーン", new_name="Screen")
mcp__UnityMCP__manage_gameobject(action="modify", target="サムネイル", new_name="Thumbnail")
```

Note: `target=` uses the *current* name. Renames are sequential — re-read hierarchy if needed.

- [ ] **Step 3: Save scene**

```python
mcp__UnityMCP__manage_scene(action="save")
```

- [ ] **Step 4: Verify no missing references**

```python
mcp__UnityMCP__read_console(action="get", types=["warning","error"], count=20)
```

- [ ] **Step 5: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Scenes/GameScene.unity
git commit -m "refactor: English-ify Hierarchy names in GameScene (LaneLights, JudgeLine, etc.)"
```

---

## Task 27: Move `tttt.unity` → `Scenes/Legacy/`

**Files:**
- Move: `Assets/Scenes/tttt.unity` → `Assets/Scenes/Legacy/tttt.unity`

- [ ] **Step 1: Confirm `tttt.unity` is not in Build Settings**

```python
mcp__UnityMCP__manage_build(action="get_build_settings")
```

If listed, remove it (move to `Legacy/` keeps it out of builds even if Build Settings is wrong, but explicit removal is safer):

```python
mcp__UnityMCP__manage_build(action="scenes", scenes=[<scenes-without-tttt>])
```

- [ ] **Step 2: Search for references to `tttt.unity` in scripts/scenes**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
grep -rln "tttt\.unity\|tttt'" Assets
```

If any references exist, update them (or note they remain valid since the file is at a new location).

- [ ] **Step 3: Create Legacy directory and move**

```python
mcp__UnityMCP__manage_asset(action="create_folder", path="Assets/Scenes/Legacy")
mcp__UnityMCP__manage_asset(action="move", path="Assets/Scenes/tttt.unity", destination="Assets/Scenes/Legacy/tttt.unity")
```

- [ ] **Step 4: Commit**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
git add Assets/Scenes
git commit -m "refactor: move tttt.unity to Scenes/Legacy/ (out of build path)"
```

---

# Phase 7: Verification

## Task 28: Run EditMode tests

- [ ] **Step 1: Wait for compilation**

Read `mcp__UnityMCP__editor/state` until `is_compiling=false`.

- [ ] **Step 2: Run all EditMode tests**

```python
mcp__UnityMCP__run_tests(mode="EditMode", include_failed_tests=True)
```

Expected: 100% pass. Investigate and fix any failures.

- [ ] **Step 3: No commit**

If tests fail, fix code and commit. Otherwise mark complete.

---

## Task 29: Scene validation

- [ ] **Step 1: Validate each scene in Build Settings**

For each scene in Build Settings:

```python
mcp__UnityMCP__manage_scene(action="validate", path="<scene_path>", auto_repair=False)
```

Expected: no errors. Note any warnings.

- [ ] **Step 2: If validation finds missing references, fix them**

Common fixes:
- Script GUID mismatch (re-link in Inspector)
- Missing prefab/material (re-import or re-link)
- Missing script class (verify class still exists)

- [ ] **Step 3: Commit fixes if any**

```bash
git add <fixed-files>
git commit -m "fix: resolve scene validation issues"
```

---

## Task 30: Windows build

- [ ] **Step 1: Trigger Windows build via batch mode (or Editor menu)**

```bash
cd C:\Users\rebui\Desktop\FlashBeat
Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneWindows64 -logFile build.log
```

Or via Editor menu `Build → Build Windows 64`.

- [ ] **Step 2: Verify build artifact exists**

```bash
ls -la Builds/Windows/FlashBeat.exe 2>/dev/null
```

Expected: file exists.

- [ ] **Step 3: No commit needed for build artifacts**

`Builds/` is gitignored. Only commit if config files changed.

---

## Task 31: End-to-end playtest with multiple song IDs

**Files:**
- Manual verification

- [ ] **Step 1: Open GameScene in Editor**

- [ ] **Step 2: For each of song IDs 1, 5, 15, 26:**

  a. Set `GManager.songID` to the ID (via Inspector on `GManager` instance, or temporarily edit and run).

  b. Press Play.

  c. Wait for `MusicManager` to load (count > 8f window).

  d. Press Space.

  e. Verify:
     - YouTube streaming starts (InvidiousVideoPlayer.PlayVideoAsync succeeds) — or local mp4 plays if YouTube fails.
     - Audio plays (`AudioSource` from `Resources/Musics/<songName>`).
     - Notes appear and respond to lane keys.
     - At `step_time >= duration - 1`, scene fades to `ResultScene`.

  f. Verify `ResultScene` shows correct title, musician, BPM, totalHits, level for the song.

- [ ] **Step 3: Document any failures**

If YouTube streaming fails for all songs, note Invidious instance availability issue. Local mp4 should still play as fallback.

- [ ] **Step 4: No commit (manual test)**

If issues require code fixes, create a new task and address them.

---

## Completion Criteria Checklist

- [ ] `GManager.Songs[]` is the single source of truth for song data.
- [ ] All 27 song IDs (0–26) match prior data (title, videoId, bpm, level, totalHits, duration).
- [ ] Hiscore save data still loads correctly.
- [ ] Zero Unity compilation errors.
- [ ] All EditMode tests pass.
- [ ] Build Settings scenes all reference valid paths.
- [ ] Windows build succeeds.
- [ ] `YPlayer[songID].GetComponent<InvidiousVideoPlayer>().PlayVideoAsync()` is invoked on Space press for at least 4 different song IDs.
- [ ] `VPlayer[songID].Play()` fallback works when YouTube streaming is unavailable.
- [ ] YouTube player components, prefabs, and `com.ibicha.youtube-player` package reference remain intact.
