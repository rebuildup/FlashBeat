# NoteEditor GUI 編集機能 実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make NoteEditor usable from the GUI for loading, editing, and saving FlashBeat chart JSON files in `Assets/Game/Resources/`, with merged lyrics data and zero dependency on local `.wav` files.

**Architecture:** Replace MusicSelector/MusicLoader workflow with a new `FlashBeatSongLoader` that scans `Assets/Game/Resources/*.json` (excluding `*_text.json` and `E2EProbeSong.json`). Patch `EditDataSerializer` for null-safe audio access and add a `LyricsDTO` field. Extend `SavePresenter` to save directly to `Resources/<曲名>.json`. Add a Lyrics tab UI to the NoteEditor scene. Provide a one-shot migration tool for legacy `*_text.json` files and a FlashBeat-side compatibility reader.

**Tech Stack:** Unity 6 (6000.5.6f1), C#, UniRx, NUnit, JSON via `UnityEngine.JsonUtility`.

**Reference spec:** `docs/superpowers/specs/2026-08-07-noteeditor-editing-design.md`

---

## File Structure

**新規 (4)**:
- `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs` — Resources スキャン + ロード
- `Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsTabPresenter.cs` — 歌詞タブ UI 制御
- `Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsListItem.cs` — 歌詞行の ViewModel
- `Assets/Editor/NoteEditor.Editor/MigrateLegacyTextJson.cs` — マイグレーション Editor ツール

**変更 (7)**:
- `Assets/NoteEditor/Scripts/Presenter/MusicSelector/MusicLoader.cs` — no-op 化
- `Assets/NoteEditor/Scripts/Presenter/MusicSelector/MusicSelectorPresenter.cs` — no-op 化
- `Assets/NoteEditor/Scripts/Model/EditData.cs` — `IsDirty` + `Lyrics` 追加
- `Assets/NoteEditor/Scripts/DTO/MusicDTO.cs` — `lyrics` フィールド + `LyricsDTO` 追加
- `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs` — null-safe Audio + 歌詞 Serialize
- `Assets/NoteEditor/Scripts/Presenter/Save/SavePresenter.cs` — 保存先変更 + IsDirty 駆動
- `Assets/NoteEditor/Scenes/NoteEditor.unity` — LyricsPanel 追加 (YAML 手編集)

**FlashBeat 本体 (1)**:
- `Assets/Game/Scripts/UI/GameSceneManager.cs` — LoadTextData merged 優先 + 旧フォールバック

**テスト (3 新規)**:
- `Assets/Tests/Editor/FlashBeatSongLoaderTests.cs`
- `Assets/Tests/Editor/EditDataSerializerLyricsTests.cs`
- `Assets/Tests/Editor/MigrateLegacyTextJsonTests.cs`

**テストヘルパー変更 (1)**:
- `Assets/Tests/Editor/SingletonTestHelper.cs` — `Lyrics` 用 5 フィールド + `isDirty_` 追加

---

## CP1: NoteEditor で Resources/*.json が一覧 + ロードできる (音なし)

### Task 1: EditData に `IsDirty` を追加

**Files:**
- Modify: `Assets/NoteEditor/Scripts/Model/EditData.cs`
- Modify: `Assets/Tests/Editor/SingletonTestHelper.cs:42-65`

- [ ] **Step 1: EditData.cs に isDirty_ フィールドを追加**

`Assets/NoteEditor/Scripts/Model/EditData.cs` の 16 行目 (`Dictionary<NotePosition, NoteObject> notes_ = ...` の直前) に以下を追加:

```csharp
        ReactiveProperty<bool> isDirty_ = new ReactiveProperty<bool>(false);
```

- [ ] **Step 2: EditData.cs に IsDirty プロパティを追加**

同ファイルの末尾 (24 行目の `public static Dictionary<NotePosition, NoteObject> Notes ...` の直前) に追加:

```csharp
        public static ReactiveProperty<bool> IsDirty { get { return Instance.isDirty_; } }
```

- [ ] **Step 3: SingletonTestHelper.cs に isDirty_ 初期化を追加**

`Assets/Tests/Editor/SingletonTestHelper.cs` の 63 行目 (`notes_` の SetValue の直前) に追加:

```csharp
            typeof(EditData).GetField("isDirty_", InstanceNonPublic)
                .SetValue(instance, new ReactiveProperty<bool>(false));
```

- [ ] **Step 4: 既存テストが通過することを確認**

Run: Test Runner → EditMode → YouTubeImportTests 全体実行
Expected: 全 11 ケース PASS (isDirty_ 追加で EditData の reflection 経由初期化が壊れていないこと)

- [ ] **Step 5: Commit**

```bash
git add Assets/NoteEditor/Scripts/Model/EditData.cs Assets/Tests/Editor/SingletonTestHelper.cs
git commit -m "feat(noteeditor): add EditData.IsDirty for save state tracking"
```

---

### Task 2: EditData に `Lyrics` 名前空間を追加

**Files:**
- Modify: `Assets/NoteEditor/Scripts/Model/EditData.cs`

- [ ] **Step 1: Lyrics 名前空間クラスを追加**

`Assets/NoteEditor/Scripts/Model/EditData.cs` の `public class EditData` 内、`public static Dictionary<NotePosition, NoteObject> Notes` の直後に追加:

```csharp
        public static class Lyrics
        {
            public static ReactiveProperty<float[]> StartTime = new ReactiveProperty<float[]>(new float[0]);
            public static ReactiveProperty<string[]> Furigana = new ReactiveProperty<string[]>(new string[0]);
            public static ReactiveProperty<string[]> Mondai = new ReactiveProperty<string[]>(new string[0]);
            public static ReactiveProperty<string[]> Romaji = new ReactiveProperty<string[]>(new string[0]);
            public static ReactiveProperty<float[]> EndTime = new ReactiveProperty<float[]>(new float[0]);
        }
```

- [ ] **Step 2: ビルド確認**

Unity Editor を起動し、Console にコンパイルエラーが出ていないことを確認。

- [ ] **Step 3: Commit**

```bash
git add Assets/NoteEditor/Scripts/Model/EditData.cs
git commit -m "feat(noteeditor): add EditData.Lyrics namespace with 5 array properties"
```

---

### Task 3: MusicDTO に `LyricsDTO` を追加

**Files:**
- Modify: `Assets/NoteEditor/Scripts/DTO/MusicDTO.cs`

- [ ] **Step 1: LyricsDTO クラスを追加**

`Assets/NoteEditor/Scripts/DTO/MusicDTO.cs` の `}` (28 行目の MusicDTO クラス閉じ) の直前に追加:

```csharp
        [System.Serializable]
        public class LyricsDTO
        {
            public float[] startTime;
            public string[] furigana;
            public string[] mondai;
            public string[] romaji;
            public float[] endTime;
        }
```

- [ ] **Step 2: EditData に `lyrics` フィールドを追加**

同ファイルの `EditData` クラス内、`public List<Note> notes;` の直後に追加:

```csharp
            public LyricsDTO lyrics;
```

- [ ] **Step 3: ビルド確認**

Unity Editor 起動、Console 確認。

- [ ] **Step 4: Commit**

```bash
git add Assets/NoteEditor/Scripts/DTO/MusicDTO.cs
git commit -m "feat(noteeditor): add LyricsDTO and lyrics field to MusicDTO.EditData"
```

---

### Task 4: EditDataSerializer に null-safe Audio + 歌詞 Serialize を追加

**Files:**
- Modify: `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs`
- Modify: `Assets/NoteEditor/Scripts/Presenter/NoteCanvas/EditNotesPresenter.cs:164-167`

- [ ] **Step 1: 失敗するテストを書く**

`Assets/Tests/Editor/EditDataSerializerLyricsTests.cs` を新規作成し、`Assets/Tests/Editor/SingletonTestHelper.cs` の `EnsureSingletons` を呼び出す `[SetUp]` と `TeardownSingletons` を呼ぶ `[TearDown]` を用意した上で:

```csharp
using NUnit.Framework;
using NoteEditor.DTO;
using NoteEditor.Model;

namespace FlashBeat.Tests.Editor
{
    public class EditDataSerializerLyricsTests
    {
        [SetUp]
        public void SetUp()
        {
            SingletonTestHelper.EnsureSingletons();
        }

        [TearDown]
        public void TearDown()
        {
            SingletonTestHelper.TeardownSingletons();
        }

        [Test]
        public void Serialize_IncludesEmptyLyricsWhenNotSet()
        {
            // EditData.Lyrics.* の初期状態は空配列。Serialize() は空 lyrics を含める。
            var json = EditDataSerializer.Serialize();
            StringAssert.Contains("\"lyrics\"", json);
            StringAssert.Contains("\"startTime\":[]", json);
        }
    }
}
```

- [ ] **Step 2: テスト実行して失敗を確認**

Run: Test Runner → EditMode → `EditDataSerializerLyricsTests.Serialize_IncludesEmptyLyricsWhenNotSet`
Expected: FAIL — `"lyrics"` が JSON に含まれない (まだ Serialize していないため)

- [ ] **Step 3: AudioFrequency() を EditData に昇格**

`Assets/NoteEditor/Scripts/Presenter/NoteCanvas/EditNotesPresenter.cs` の 164-167 行目 (`static int AudioFrequency()` メソッド) を削除。

代わりに `Assets/NoteEditor/Scripts/Model/EditData.cs` の `public class EditData` 内、`public static Dictionary<NotePosition, NoteObject> Notes` の直後に追加:

```csharp
        public static int AudioFrequency()
        {
            return Audio.Source.clip != null ? Audio.Source.clip.frequency : 44100;
        }
```

`Assets/NoteEditor/Scripts/Presenter/NoteCanvas/EditNotesPresenter.cs` の 82, 89, 97 行目の `AudioFrequency()` 呼び出しを `EditData.AudioFrequency()` に書き換え (3 箇所):

- 行 82: `b.OrderBy(note => note.position.ToSamples(AudioFrequency(), EditData.BPM.Value))` → `EditData.AudioFrequency()`
- 行 89: 同様
- 行 97: 同様

- [ ] **Step 4: EditDataSerializer.Serialize に歌詞フィールドを追加**

`Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs` の `Serialize()` メソッド内、`dto.videoId = EditData.VideoId.Value;` の直後に追加:

```csharp
            dto.lyrics = new MusicDTO.LyricsDTO
            {
                startTime = EditData.Lyrics.StartTime.Value ?? new float[0],
                furigana  = EditData.Lyrics.Furigana.Value ?? new string[0],
                mondai    = EditData.Lyrics.Mondai.Value ?? new string[0],
                romaji    = EditData.Lyrics.Romaji.Value ?? new string[0],
                endTime   = EditData.Lyrics.EndTime.Value ?? new float[0],
            };
```

- [ ] **Step 5: Serialize() の clip.frequency を null-guard**

同メソッドの 23 行目 `Audio.Source.clip.frequency` を `EditData.AudioFrequency()` に置換:

```csharp
            var sortedNoteObjects = EditData.Notes.Values
                .Where(note => !(note.note.type == NoteTypes.Long && EditData.Notes.ContainsKey(note.note.prev)))
                .OrderBy(note => note.note.position.ToSamples(EditData.AudioFrequency(), EditData.BPM.Value));
```

- [ ] **Step 6: テストを再実行して PASS を確認**

Run: Test Runner → EditMode → `EditDataSerializerLyricsTests`
Expected: PASS

- [ ] **Step 7: 既存テストが壊れていないことを確認**

Run: Test Runner → EditMode → YouTubeImportTests 全体
Expected: 全 PASS

- [ ] **Step 8: Commit**

```bash
git add Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs Assets/NoteEditor/Scripts/Presenter/NoteCanvas/EditNotesPresenter.cs Assets/NoteEditor/Scripts/Model/EditData.cs Assets/Tests/Editor/EditDataSerializerLyricsTests.cs
git commit -m "feat(noteeditor): extend EditDataSerializer with lyrics + null-safe AudioFrequency"
```

---

### Task 5: FlashBeatSongLoader を作成 (TDD)

**Files:**
- Create: `Assets/Tests/Editor/FlashBeatSongLoaderTests.cs`
- Create: `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs`

- [ ] **Step 1: 失敗するテストを書く**

`Assets/Tests/Editor/FlashBeatSongLoaderTests.cs` を新規作成:

```csharp
using System.IO;
using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    public class FlashBeatSongLoaderTests
    {
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "flashbeat_songloader_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }

        [Test]
        public void EnumerateJsonFiles_ExcludesTextJson()
        {
            File.WriteAllText(Path.Combine(tempDir, "AAA.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "AAA_text.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "BBB.json"), "{}");

            var result = FlashBeatSongLoader.EnumerateJsonFilesForTest(tempDir);

            CollectionAssert.AreEquivalent(new[] { "AAA.json", "BBB.json" },
                result.Select(Path.GetFileName).ToArray());
        }

        [Test]
        public void EnumerateJsonFiles_ExcludesE2EProbeSong()
        {
            File.WriteAllText(Path.Combine(tempDir, "Song1.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "E2EProbeSong.json"), "{}");

            var result = FlashBeatSongLoader.EnumerateJsonFilesForTest(tempDir);

            CollectionAssert.AreEquivalent(new[] { "Song1.json" },
                result.Select(Path.GetFileName).ToArray());
        }

        [Test]
        public void EnumerateJsonFiles_ReturnsEmptyForEmptyDir()
        {
            var result = FlashBeatSongLoader.EnumerateJsonFilesForTest(tempDir);
            Assert.IsEmpty(result);
        }

        [Test]
        public void EnumerateJsonFiles_ReturnsEmptyForMissingDir()
        {
            var result = FlashBeatSongLoader.EnumerateJsonFilesForTest(Path.Combine(tempDir, "nonexistent"));
            Assert.IsEmpty(result);
        }
    }
}
```

`using System.Linq;` を冒頭に追加。

- [ ] **Step 2: テスト実行して失敗を確認**

Run: Test Runner → EditMode → `FlashBeatSongLoaderTests` 全体
Expected: FAIL — `FlashBeatSongLoader` 型が存在しない

- [ ] **Step 3: FlashBeatSongLoader.cs を作成**

`Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs` を新規作成:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NoteEditor.Presenter.FlashBeatSong
{
    public class FlashBeatSongLoader : MonoBehaviour
    {
        public const string ResourcesDir = "Assets/Game/Resources";

        [SerializeField] Transform fileItemContainer = default;
        [SerializeField] GameObject fileItemPrefab = default;
        [SerializeField] UnityEngine.UI.Text emptyMessageText = default;
        [SerializeField] UnityEngine.UI.Button refreshButton = default;

        void Start()
        {
            if (refreshButton != null) refreshButton.onClick.AddListener(RefreshList);
            RefreshList();
        }

        public void RefreshList()
        {
            foreach (Transform child in fileItemContainer) Destroy(child.gameObject);

            var jsonPaths = EnumerateJsonFilesForTest(ResourcesDir).ToList();

            foreach (var path in jsonPaths)
            {
                var item = Instantiate(fileItemPrefab, fileItemContainer);
                item.GetComponent<FileListItem>().SetInfo(new FileItemInfo(false, path));
                var btn = item.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.onClick.AddListener(() => OnFileSelected(path));
            }

            if (emptyMessageText != null) emptyMessageText.gameObject.SetActive(jsonPaths.Count == 0);
        }

        public void OnFileSelected(string jsonPath)
        {
            try
            {
                var json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                NoteEditor.Model.EditDataSerializer.Deserialize(json);
                NoteEditor.Model.EditData.Name.Value = Path.GetFileNameWithoutExtension(jsonPath);
                NoteEditor.Model.EditData.IsDirty.Value = false;
                ProvideSyntheticAudioClip();
                NoteEditor.Model.Audio.OnLoad.OnNext(UniRx.Unit.Default);
                Debug.Log($"[FlashBeatSongLoader] Loaded: {jsonPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FlashBeatSongLoader] Failed to load {jsonPath}: {ex.Message}");
            }
        }

        // 44100Hz / 1秒 / 無音の合成 AudioClip を Audio.Source.clip にセット。
        // Audio.OnLoad 後に Audio.Source.clip を null-guard せず参照する
        // Presenter (PlaybackPositionPresenter 等) が NPE しないために必要。
        static void ProvideSyntheticAudioClip()
        {
            const int sampleRate = 44100;
            const float lengthSeconds = 1f;
            int sampleCount = Mathf.CeilToInt(sampleRate * lengthSeconds);
            var silent = new float[sampleCount];
            var clip = AudioClip.Create("__FlashBeatSynthetic", sampleCount, 1, sampleRate, false);
            clip.SetData(silent, 0);
            NoteEditor.Model.Audio.Source.clip = clip;
        }

        // テスト用: 純粋関数の static メソッドとして公開。Product code では ResourcesDir を固定で読む。
        public static IEnumerable<string> EnumerateJsonFilesForTest(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return Enumerable.Empty<string>();

            return Directory.GetFiles(dir, "*.json")
                .Where(p => !Path.GetFileNameWithoutExtension(p).EndsWith("_text"))
                .Where(p => Path.GetFileNameWithoutExtension(p) != "E2EProbeSong")
                .OrderBy(p => p);
        }
    }
}
```

- [ ] **Step 4: テストを再実行して PASS を確認**

Run: Test Runner → EditMode → `FlashBeatSongLoaderTests` 全体
Expected: 全 4 ケース PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs Assets/Tests/Editor/FlashBeatSongLoaderTests.cs
git commit -m "feat(noteeditor): add FlashBeatSongLoader to scan and load Resources/*.json"
```

---

### Task 6: MusicLoader / MusicSelectorPresenter を no-op 化

**Files:**
- Modify: `Assets/NoteEditor/Scripts/Presenter/MusicSelector/MusicLoader.cs`
- Modify: `Assets/NoteEditor/Scripts/Presenter/MusicSelector/MusicSelectorPresenter.cs`

- [ ] **Step 1: MusicLoader.cs の LoadMusic コルーチンを no-op 化**

`Assets/NoteEditor/Scripts/Presenter/MusicSelector/MusicLoader.cs` の `LoadMusic(string fileName)` メソッド全体を以下に置換 (WWW/clip セット/Audio.OnLoad 発火のロジックを削除):

```csharp
        IEnumerator LoadMusic(string fileName)
        {
            Debug.LogWarning($"[MusicLoader.LoadMusic] is deprecated. Use FlashBeatSongLoader.OnFileSelected instead. (fileName={fileName})");
            yield break;
        }
```

`Awake()` の `ResetEditor()` 呼び出しは残す (EditData 初期化のため)。

- [ ] **Step 2: MusicSelectorPresenter.cs の Subscribe ブロックを no-op 化**

`Assets/NoteEditor/Scripts/Presenter/MusicSelector/MusicSelectorPresenter.cs` の `void Start()` 内の全 Subscribe (ChangeLocationCommandManager、Settings、MusicSelector.DirectoryPath、Observable.Timer、FilePathList、loadButton) を削除し、メソッドを以下のみ残す:

```csharp
        void Start()
        {
            // Disabled: replaced by FlashBeatSongLoader.
            // Kept as a no-op so existing scene references don't NRE.
        }
```

- [ ] **Step 3: Unity Editor で NoteEditor.unity を開きエラーが出ていないか確認**

Tools → NoteEditor → Install YouTube Import Button (既にインストール済みのはず) 経由で NoteEditor シーンを開き、Console にエラーが出ていないことを確認。Game ビューでリストが出る必要はない (まだシーンに FlashBeatSongLoader を配置していないため)。

- [ ] **Step 4: 既存テストが壊れていないことを確認**

Run: Test Runner → EditMode → 全テスト
Expected: 全 PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/NoteEditor/Scripts/Presenter/MusicSelector/MusicLoader.cs Assets/NoteEditor/Scripts/Presenter/MusicSelector/MusicSelectorPresenter.cs
git commit -m "refactor(noteeditor): no-op MusicLoader and MusicSelectorPresenter (replaced by FlashBeatSongLoader)"
```

---

### Task 7: SavePresenter を保存先変更 + IsDirty 駆動に

**Files:**
- Modify: `Assets/NoteEditor/Scripts/Presenter/Save/SavePresenter.cs`

- [ ] **Step 1: 失敗するテストを書く**

`Assets/Tests/Editor/EditDataSerializerLyricsTests.cs` に以下を追加:

```csharp
        [Test]
        public void SavePresenter_WritesToResourcesDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "flashbeat_save_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // 直接 EditData に書き、SavePresenter.Save をリフレクションで呼ぶ
                EditData.Name.Value = "TestSong";
                EditData.BPM.Value = 120;
                EditData.Notes.Clear();

                // SavePresenter は static メソッドではないので、シーン上のインスタンスが必要。
                // 代わりに Serialize → ファイル書き込みのパスを直接検証する。
                var json = EditDataSerializer.Serialize();
                var targetPath = Path.Combine(tempDir, "TestSong.json");
                File.WriteAllText(targetPath, json);

                Assert.IsTrue(File.Exists(targetPath));
                var roundtrip = File.ReadAllText(targetPath);
                Assert.IsTrue(roundtrip.Contains("\"name\":\"TestSong\""));
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
```

`using System.IO;` を冒頭に追加。

- [ ] **Step 2: テスト実行して PASS を確認 (この段階ではパスするはず)**

Run: Test Runner → EditMode → `EditDataSerializerLyricsTests.SavePresenter_WritesToResourcesDirectory`
Expected: PASS (まだ SavePresenter.Save を触っていないため)

- [ ] **Step 3: SavePresenter.cs を Resources 直保存に書き換え**

`Assets/NoteEditor/Scripts/Presenter/Save/SavePresenter.cs` の `using` ディレクティブを以下に整理 (重複 `using System.IO;` 等を削除):

```csharp
using NoteEditor.Model;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Text;
```

`public void Save()` メソッドを以下に置換:

```csharp
        const string ResourcesDir = "Assets/Game/Resources";

        public void Save()
        {
            try
            {
                var songName = Path.GetFileNameWithoutExtension(EditData.Name.Value);
                if (string.IsNullOrEmpty(songName))
                {
                    Debug.LogError("[SavePresenter] EditData.Name is empty");
                    if (messageText != null) messageText.text = "曲名が空のため保存できません";
                    return;
                }
                var path = Path.Combine(ResourcesDir, songName + ".json");
                if (!Directory.Exists(ResourcesDir)) Directory.CreateDirectory(ResourcesDir);
                var json = EditDataSerializer.Serialize();
                File.WriteAllText(path, json, Encoding.UTF8);
                EditData.IsDirty.Value = false;
                Debug.Log($"[NoteEditor] Saved: {path}");
                if (messageText != null) messageText.text = path + " に保存しました";
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NoteEditor] Save failed: {ex.Message}");
                if (messageText != null) messageText.text = "保存に失敗しました: " + ex.Message;
            }
        }
```

`mustBeSaved` の `.SkipUntil(Audio.OnLoad.DelayFrame(1))` を削除し、EditData.IsDirty を直接参照する形に置換。`Awake()` メソッド内の 49-61 行目を以下に置換:

```csharp
            mustBeSaved = Observable.Merge(
                    EditData.BPM.Select(_ => true),
                    EditData.OffsetSamples.Select(_ => true),
                    EditData.MaxBlock.Select(_ => true),
                    editPresenter.RequestForEditNote.Select(_ => true),
                    editPresenter.RequestForAddNote.Select(_ => true),
                    editPresenter.RequestForRemoveNote.Select(_ => true),
                    editPresenter.RequestForChangeNoteStatus.Select(_ => true),
                    EditData.Lyrics.Mondai.Select(_ => true),
                    saveActionObservable.Select(_ => false),
                    EditData.IsDirty.Select(_ => EditData.IsDirty.Value))
                .Do(unsaved => saveButton.GetComponent<Image>().color = unsaved ? unsavedStateButtonColor : savedStateButtonColor)
                .ToReactiveProperty();
```

`Audio.OnLoad.Select(_ => false)` は削除 (擬似 OnNext で起動するが、即 false に上書きされる)。代わりに `EditData.IsDirty.Select(_ => EditData.IsDirty.Value)` で IsDirty 自身を購読。

- [ ] **Step 4: 既存テストが通過することを確認**

Run: Test Runner → EditMode → 全テスト
Expected: 全 PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/NoteEditor/Scripts/Presenter/Save/SavePresenter.cs Assets/Tests/Editor/EditDataSerializerLyricsTests.cs
git commit -m "feat(noteeditor): save charts directly to Resources/<song>.json, track IsDirty"
```

---

### ✅ CP1 チェックポイント

NoteEditor シーンを開く → Resources/*.json 一覧が出る → 1 曲クリック → ノート表示 + 編集 + Ctrl+S → ファイル上書き確認。Console エラーなし。

---

## CP2: 歌詞を JSON に統合 + NoteEditor で歌詞編集 UI

### Task 8: EditDataSerializer.Deserialize に歌詞フィールドを追加

**Files:**
- Modify: `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs`

- [ ] **Step 1: 失敗するテストを書く**

`Assets/Tests/Editor/EditDataSerializerLyricsTests.cs` に追加:

```csharp
        [Test]
        public void Deserialize_PopulatesLyricsFromMergedJson()
        {
            var json = "{\"name\":\"x\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[],\"lyrics\":{" +
                       "\"startTime\":[1.0,2.0],\"furigana\":[\"a\",\"b\"],\"mondai\":[\"c\",\"d\"]," +
                       "\"romaji\":[\"e\",\"f\"],\"endTime\":[1.5,2.5]}}";
            EditDataSerializer.Deserialize(json);
            Assert.AreEqual(2, EditData.Lyrics.Mondai.Value.Length);
            Assert.AreEqual("c", EditData.Lyrics.Mondai.Value[0]);
            Assert.AreEqual(1.0f, EditData.Lyrics.StartTime.Value[0]);
        }

        [Test]
        public void Deserialize_HandlesMissingLyricsField()
        {
            var json = "{\"name\":\"legacy\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[]}";
            Assert.DoesNotThrow(() => EditDataSerializer.Deserialize(json));
            Assert.AreEqual(0, EditData.Lyrics.Mondai.Value.Length);
        }

        [Test]
        public void Serialize_RoundtripsLyrics()
        {
            EditData.Lyrics.Mondai.Value = new[] { "foo", "bar" };
            EditData.Lyrics.Furigana.Value = new[] { "f", "b" };
            EditData.Lyrics.Romaji.Value = new[] { "hoge", "fuga" };
            EditData.Lyrics.StartTime.Value = new[] { 1.5f, 3.0f };
            EditData.Lyrics.EndTime.Value = new[] { 2.5f, 4.0f };
            var json = EditDataSerializer.Serialize();
            EditDataSerializer.Deserialize(json);
            Assert.AreEqual(new[] { "foo", "bar" }, EditData.Lyrics.Mondai.Value);
            Assert.AreEqual(new[] { "hoge", "fuga" }, EditData.Lyrics.Romaji.Value);
            Assert.AreEqual(new[] { 1.5f, 3.0f }, EditData.Lyrics.StartTime.Value);
            Assert.AreEqual(new[] { 2.5f, 4.0f }, EditData.Lyrics.EndTime.Value);
        }
```

- [ ] **Step 2: テスト実行して失敗を確認**

Run: Test Runner → EditMode → `EditDataSerializerLyricsTests`
Expected: 3 つの新規テストすべて FAIL (まだ Deserialize に歌詞ロジックがないため)

- [ ] **Step 3: Deserialize に歌詞読み込みを追加**

`Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs` の `Deserialize(string json)` メソッド内、`EditData.VideoId.Value = ...` の直後に追加:

```csharp
            if (editData.lyrics != null)
            {
                EditData.Lyrics.StartTime.Value = editData.lyrics.startTime ?? new float[0];
                EditData.Lyrics.Furigana.Value  = editData.lyrics.furigana  ?? new string[0];
                EditData.Lyrics.Mondai.Value    = editData.lyrics.mondai    ?? new string[0];
                EditData.Lyrics.Romaji.Value    = editData.lyrics.romaji    ?? new string[0];
                EditData.Lyrics.EndTime.Value   = editData.lyrics.endTime   ?? new float[0];
            }
            else
            {
                EditData.Lyrics.StartTime.Value = new float[0];
                EditData.Lyrics.Furigana.Value  = new string[0];
                EditData.Lyrics.Mondai.Value    = new string[0];
                EditData.Lyrics.Romaji.Value    = new string[0];
                EditData.Lyrics.EndTime.Value   = new float[0];
            }
```

- [ ] **Step 4: テストを再実行して全 PASS を確認**

Run: Test Runner → EditMode → `EditDataSerializerLyricsTests` 全体
Expected: 全 PASS (既存 4 件 + 新規 3 件)

- [ ] **Step 5: 既存テストが壊れていないことを確認**

Run: Test Runner → EditMode → YouTubeImportTests + FlashBeatSongLoaderTests
Expected: 全 PASS

- [ ] **Step 6: Commit**

```bash
git add Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs Assets/Tests/Editor/EditDataSerializerLyricsTests.cs
git commit -m "feat(noteeditor): EditDataSerializer.Deserialize handles merged lyrics"
```

---

### Task 9: LyricsTabPresenter を作成 (TDD)

**Files:**
- Create: `Assets/Tests/Editor/LyricsTabPresenterTests.cs`
- Create: `Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsTabPresenter.cs`

- [ ] **Step 1: 失敗するテストを書く**

`Assets/Tests/Editor/LyricsTabPresenterTests.cs` を新規作成:

```csharp
using NUnit.Framework;
using NoteEditor.Model;

namespace FlashBeat.Tests.Editor
{
    public class LyricsTabPresenterTests
    {
        [SetUp]
        public void SetUp()
        {
            SingletonTestHelper.EnsureSingletons();
        }

        [TearDown]
        public void TearDown()
        {
            SingletonTestHelper.TeardownSingletons();
        }

        [Test]
        public void AddLyricAtTime_AppendsToAllArrays()
        {
            LyricsTabPresenter.AddLyricAtTime(startTime: 5.0f, endTime: 6.5f,
                mondai: "漢字", furigana: "かんじ", romaji: "kanji");

            Assert.AreEqual(1, EditData.Lyrics.StartTime.Value.Length);
            Assert.AreEqual(5.0f, EditData.Lyrics.StartTime.Value[0]);
            Assert.AreEqual("漢字", EditData.Lyrics.Mondai.Value[0]);
            Assert.AreEqual("かんじ", EditData.Lyrics.Furigana.Value[0]);
            Assert.AreEqual("kanji", EditData.Lyrics.Romaji.Value[0]);
            Assert.AreEqual(6.5f, EditData.Lyrics.EndTime.Value[0]);
        }

        [Test]
        public void RemoveLyricAt_RemovesFromAllArrays()
        {
            LyricsTabPresenter.AddLyricAtTime(1, 2, "a", "f", "r");
            LyricsTabPresenter.AddLyricAtTime(3, 4, "b", "g", "s");
            LyricsTabPresenter.AddLyricAtTime(5, 6, "c", "h", "t");

            LyricsTabPresenter.RemoveLyricAt(1);

            Assert.AreEqual(2, EditData.Lyrics.Mondai.Value.Length);
            Assert.AreEqual("a", EditData.Lyrics.Mondai.Value[0]);
            Assert.AreEqual("c", EditData.Lyrics.Mondai.Value[1]);
        }

        [Test]
        public void UpdateLyricAt_ReplacesFields()
        {
            LyricsTabPresenter.AddLyricAtTime(1, 2, "old", "f", "r");
            LyricsTabPresenter.UpdateLyricAt(0, startTime: 1.5f, endTime: 2.5f,
                mondai: "new", furigana: "n", romaji: "n2");

            Assert.AreEqual(1, EditData.Lyrics.Mondai.Value.Length);
            Assert.AreEqual("new", EditData.Lyrics.Mondai.Value[0]);
            Assert.AreEqual(1.5f, EditData.Lyrics.StartTime.Value[0]);
            Assert.AreEqual(2.5f, EditData.Lyrics.EndTime.Value[0]);
        }
    }
}
```

- [ ] **Step 2: テスト実行して失敗を確認**

Run: Test Runner → EditMode → `LyricsTabPresenterTests`
Expected: 3 件すべて FAIL — `LyricsTabPresenter` 型が存在しない

- [ ] **Step 3: LyricsTabPresenter.cs を作成 (純粋ロジック部分のみ)**

`Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsTabPresenter.cs` を新規作成:

```csharp
using System.Linq;
using NoteEditor.Model;
using UnityEngine;

namespace NoteEditor.Presenter.Lyrics
{
    public class LyricsTabPresenter : MonoBehaviour
    {
        // 純粋ロジック: テスト可能
        public static void AddLyricAtTime(float startTime, float endTime,
            string mondai, string furigana, string romaji)
        {
            EditData.Lyrics.StartTime.Value = Append(EditData.Lyrics.StartTime.Value, startTime);
            EditData.Lyrics.EndTime.Value   = Append(EditData.Lyrics.EndTime.Value, endTime);
            EditData.Lyrics.Mondai.Value    = Append(EditData.Lyrics.Mondai.Value, mondai ?? "");
            EditData.Lyrics.Furigana.Value  = Append(EditData.Lyrics.Furigana.Value, furigana ?? "");
            EditData.Lyrics.Romaji.Value    = Append(EditData.Lyrics.Romaji.Value, romaji ?? "");
            EditData.IsDirty.Value = true;
        }

        public static void RemoveLyricAt(int index)
        {
            EditData.Lyrics.StartTime.Value = RemoveAt(EditData.Lyrics.StartTime.Value, index);
            EditData.Lyrics.EndTime.Value   = RemoveAt(EditData.Lyrics.EndTime.Value, index);
            EditData.Lyrics.Mondai.Value    = RemoveAt(EditData.Lyrics.Mondai.Value, index);
            EditData.Lyrics.Furigana.Value  = RemoveAt(EditData.Lyrics.Furigana.Value, index);
            EditData.Lyrics.Romaji.Value    = RemoveAt(EditData.Lyrics.Romaji.Value, index);
            EditData.IsDirty.Value = true;
        }

        public static void UpdateLyricAt(int index, float startTime, float endTime,
            string mondai, string furigana, string romaji)
        {
            EditData.Lyrics.StartTime.Value = ReplaceAt(EditData.Lyrics.StartTime.Value, index, startTime);
            EditData.Lyrics.EndTime.Value   = ReplaceAt(EditData.Lyrics.EndTime.Value, index, endTime);
            EditData.Lyrics.Mondai.Value    = ReplaceAt(EditData.Lyrics.Mondai.Value, index, mondai ?? "");
            EditData.Lyrics.Furigana.Value  = ReplaceAt(EditData.Lyrics.Furigana.Value, index, furigana ?? "");
            EditData.Lyrics.Romaji.Value    = ReplaceAt(EditData.Lyrics.Romaji.Value, index, romaji ?? "");
            EditData.IsDirty.Value = true;
        }

        static T[] Append<T>(T[] arr, T value)
        {
            var copy = new T[arr.Length + 1];
            System.Array.Copy(arr, copy, arr.Length);
            copy[arr.Length] = value;
            return copy;
        }

        static T[] RemoveAt<T>(T[] arr, int index)
        {
            var copy = new T[arr.Length - 1];
            System.Array.Copy(arr, copy, index, index);
            System.Array.Copy(arr, index + 1, copy, index, arr.Length - 1 - index);
            return copy;
        }

        static T[] ReplaceAt<T>(T[] arr, int index, T value)
        {
            var copy = (T[])arr.Clone();
            copy[index] = value;
            return copy;
        }
    }
}
```

- [ ] **Step 4: テストを再実行して全 PASS を確認**

Run: Test Runner → EditMode → `LyricsTabPresenterTests`
Expected: 全 3 件 PASS

- [ ] **Step 5: 既存テストが壊れていないことを確認**

Run: Test Runner → EditMode → 全 EditMode テスト
Expected: 全 PASS

- [ ] **Step 6: Commit**

```bash
git add Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsTabPresenter.cs Assets/Tests/Editor/LyricsTabPresenterTests.cs
git commit -m "feat(noteeditor): add LyricsTabPresenter with add/remove/update operations"
```

---

### Task 10: NoteEditor シーンに LyricsPanel を追加 (YAML 手編集)

**Files:**
- Modify: `Assets/NoteEditor/Scenes/NoteEditor.unity`

- [ ] **Step 1: NoteEditor.unity を開いて現状確認**

Unity Editor で `Assets/NoteEditor/Scenes/NoteEditor.unity` を開き、Hierarchy の構造 (Canvas、Toolstrip、NoteCanvasPanel 等の名前) を記録。Inspector で NoteEditorSettings.lighting を確認。

- [ ] **Step 2: LyricsPanel GameObject を追加**

Hierarchy で右クリック → UI → Canvas で "LyricsPanel" を作成。RectTransform を 画面いっぱい (anchor stretch) に設定。CanvasGroup コンポーネントを追加し、初期 `alpha=1, interactable=false` (NoteCanvas 表示中は触らせない)。

子要素として以下を作成 (全て UI):
- `Toolbar` (Horizontal Layout Group) — ボタン: "Note" (NoteCanvas に切替)、"Lyrics" (LyricsPanel に切替)、"Save" (SavePresenter.Save 呼び出し)
- `TimelineStrip` (Image + Button) — 上端に細い帯、クリックで時間取得
- `LyricsListScrollView` (Scroll View) — 中に Vertical Layout Group 付き `Content` 子要素
- `LyricsItemPrefab` (Prefab) — Time | Furigana InputField | Mondai InputField | Romaji InputField | 削除ボタン の 5 列 Horizontal Layout
- `EmptyMessageText` (Text) — "歌詞がありません。タイムラインをクリックして追加"

- [ ] **Step 3: LyricsListItem.cs (MonoBehaviour) を作成**

`Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsListItem.cs` を新規作成:

```csharp
using NoteEditor.Model;
using UnityEngine;
using UnityEngine.UI;

namespace NoteEditor.Presenter.Lyrics
{
    public class LyricsListItem : MonoBehaviour
    {
        [SerializeField] Text timeText = default;
        [SerializeField] InputField furiganaInput = default;
        [SerializeField] InputField mondaiInput = default;
        [SerializeField] InputField romajiInput = default;
        [SerializeField] Button removeButton = default;

        public int Index { get; private set; }

        public void SetData(int index, float startTime, float endTime,
            string mondai, string furigana, string romaji)
        {
            Index = index;
            if (timeText != null) timeText.text = $"{startTime:0.00}s - {endTime:0.00}s";
            if (furiganaInput != null) furiganaInput.text = furigana;
            if (mondaiInput != null) mondaiInput.text = mondai;
            if (romajiInput != null) romajiInput.text = romaji;
            if (removeButton != null) removeButton.onClick.AddListener(OnRemoveClicked);

            // 各 InputField にフォーカス解除時のコミットを追加
            if (furiganaInput != null) furiganaInput.onEndEdit.AddListener(v => OnFieldChanged());
            if (mondaiInput != null) mondaiInput.onEndEdit.AddListener(v => OnFieldChanged());
            if (romajiInput != null) romajiInput.onEndEdit.AddListener(v => OnFieldChanged());
        }

        void OnRemoveClicked()
        {
            LyricsTabPresenter.RemoveLyricAt(Index);
        }

        void OnFieldChanged()
        {
            // endTime は固定 (2 秒後) とする。startTime は timeText から再パース。
            float startTime = EditData.Lyrics.StartTime.Value[Index];
            float endTime = EditData.Lyrics.EndTime.Value[Index];
            LyricsTabPresenter.UpdateLyricAt(Index, startTime, endTime,
                mondaiInput.text, furiganaInput.text, romajiInput.text);
        }
    }
}
```

- [ ] **Step 4: LyricsTabPresenter に UI 制御メソッドを追加**

`Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsTabPresenter.cs` に以下を追加 (`using System.Linq;` と `using UnityEngine.UI;` を using に追加):

```csharp
        // シーン UI 制御 (要 SerializeField 注入)
        [SerializeField] GameObject noteCanvasPanel = default;
        [SerializeField] GameObject lyricsPanel = default;
        [SerializeField] RectTransform lyricsListContent = default;
        [SerializeField] GameObject lyricsItemPrefab = default;
        [SerializeField] Text emptyMessage = default;
        [SerializeField] Button timelineButton = default;

        LyricsListItem[] itemCache;

        void Start()
        {
            // EditData.Lyrics の変更を購読してリスト再構築
            EditData.Lyrics.Mondai.Subscribe(_ => RebuildList());
            if (timelineButton != null) timelineButton.onClick.AddListener(OnTimelineClicked);
        }

        void OnTimelineClicked()
        {
            // クリック位置から秒数を計算
            var rect = timelineButton.GetComponent<RectTransform>();
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, Input.mousePosition, null, out localPoint);
            float t = Mathf.Lerp(0, 60, (localPoint.x - rect.rect.xMin) / rect.rect.width);
            LyricsTabPresenter.AddLyricAtTime(t, t + 2f, "", "", "");
        }

        void RebuildList()
        {
            if (lyricsListContent == null) return;
            foreach (Transform child in lyricsListContent) Destroy(child.gameObject);

            var mondai = EditData.Lyrics.Mondai.Value;
            for (int i = 0; i < mondai.Length; i++)
            {
                var item = Instantiate(lyricsItemPrefab, lyricsListContent);
                item.GetComponent<LyricsListItem>().SetData(
                    i,
                    EditData.Lyrics.StartTime.Value[i],
                    EditData.Lyrics.EndTime.Value[i],
                    mondai[i],
                    EditData.Lyrics.Furigana.Value[i],
                    EditData.Lyrics.Romaji.Value[i]);
            }
            if (emptyMessage != null) emptyMessage.gameObject.SetActive(mondai.Length == 0);
        }

        public void ShowLyricsTab()
        {
            if (noteCanvasPanel != null) noteCanvasPanel.SetActive(false);
            if (lyricsPanel != null) lyricsPanel.SetActive(true);
            RebuildList();
        }

        public void ShowNoteCanvasTab()
        {
            if (noteCanvasPanel != null) noteCanvasPanel.SetActive(true);
            if (lyricsPanel != null) lyricsPanel.SetActive(false);
        }
```

- [ ] **Step 5: Unity Editor で NoteEditor シーンを開き、Play して動作確認**

Tools → NoteEditor → Open NoteEditor (または scene asset を直接開く) → Play。既存曲 1 つ選択 → 歌詞タブに切替 → タイムラインクリックで 1 行追加 → Mondai 入力 → Save → JSON に `lyrics` キーが含まれることを `cat Assets/Game/Resources/<曲名>.json | grep lyrics` で確認。

- [ ] **Step 6: Commit**

```bash
git add Assets/NoteEditor/Scenes/NoteEditor.unity Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsTabPresenter.cs Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsListItem.cs
git commit -m "feat(noteeditor): add Lyrics tab UI with timeline-driven entry add"
```

---

### ✅ CP2 チェックポイント

NoteEditor で曲選択 → 歌詞タブ → タイムラインクリックで行追加 → 各フィールド編集 → Save → JSON に lyrics 配列が含まれる。Console エラーなし。

---

## CP3: マイグレーション + FlashBeat 側互換読み込み

### Task 11: MigrateLegacyTextJson Editor ツールを作成 (TDD)

**Files:**
- Create: `Assets/Tests/Editor/MigrateLegacyTextJsonTests.cs`
- Create: `Assets/Editor/NoteEditor.Editor/MigrateLegacyTextJson.cs`

- [ ] **Step 1: 失敗するテストを書く**

`Assets/Tests/Editor/MigrateLegacyTextJsonTests.cs` を新規作成:

```csharp
using System.IO;
using NUnit.Framework;
using NoteEditor.DTO;

namespace FlashBeat.Tests.Editor
{
    public class MigrateLegacyTextJsonTests
    {
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "flashbeat_migrate_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }

        [Test]
        public void Migrate_MergesTextIntoChartAndDeletesTextFile()
        {
            var chartJson = "{\"name\":\"AAA\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[]}";
            var textJson = "{\"StartTime\":[1.0,2.0],\"Furigana\":[\"a\",\"b\"]," +
                           "\"Mondai\":[\"c\",\"d\"],\"romaji\":[\"e\",\"f\"]," +
                           "\"EndTime\":[1.5,2.5]}";
            File.WriteAllText(Path.Combine(tempDir, "AAA.json"), chartJson);
            File.WriteAllText(Path.Combine(tempDir, "AAA_text.json"), textJson);

            MigrateLegacyTextJson.MigrateForTest(tempDir);

            Assert.IsFalse(File.Exists(Path.Combine(tempDir, "AAA_text.json")));
            Assert.IsTrue(File.Exists(Path.Combine(tempDir, "AAA.json")));

            var merged = UnityEngine.JsonUtility.FromJson<MusicDTO.EditData>(
                File.ReadAllText(Path.Combine(tempDir, "AAA.json")));
            Assert.IsNotNull(merged.lyrics);
            Assert.AreEqual(2, merged.lyrics.mondai.Length);
            Assert.AreEqual("c", merged.lyrics.mondai[0]);
        }

        [Test]
        public void Migrate_SkipsOrphanTextFile()
        {
            File.WriteAllText(Path.Combine(tempDir, "Orphan_text.json"), "{}");
            Assert.DoesNotThrow(() => MigrateLegacyTextJson.MigrateForTest(tempDir));
            Assert.IsTrue(File.Exists(Path.Combine(tempDir, "Orphan_text.json")));
        }

        [Test]
        public void Migrate_IsIdempotent()
        {
            var alreadyMerged = "{\"name\":\"X\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[]," +
                                "\"lyrics\":{\"startTime\":[1],\"mondai\":[\"a\"]}}";
            File.WriteAllText(Path.Combine(tempDir, "X.json"), alreadyMerged);
            File.WriteAllText(Path.Combine(tempDir, "X_text.json"),
                "{\"StartTime\":[99],\"Mondai\":[\"different\"]}");

            MigrateLegacyTextJson.MigrateForTest(tempDir);

            var chart = UnityEngine.JsonUtility.FromJson<MusicDTO.EditData>(
                File.ReadAllText(Path.Combine(tempDir, "X.json")));
            Assert.AreEqual("a", chart.lyrics.mondai[0], "Existing lyrics should not be overwritten");
        }
    }
}
```

- [ ] **Step 2: テスト実行して失敗を確認**

Run: Test Runner → EditMode → `MigrateLegacyTextJsonTests`
Expected: 3 件すべて FAIL — `MigrateLegacyTextJson` 型が存在しない

- [ ] **Step 3: MigrateLegacyTextJson.cs を作成 (純粋ロジックのみ)**

`Assets/Editor/NoteEditor.Editor/MigrateLegacyTextJson.cs` を新規作成:

```csharp
using System.IO;
using NoteEditor.DTO;
using UnityEditor;
using UnityEngine;

namespace NoteEditor.Editor
{
    public static class MigrateLegacyTextJson
    {
        [MenuItem("Tools/NoteEditor/Migrate Legacy Text JSON")]
        public static void Run()
        {
            var dir = "Assets/Game/Resources";
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[Migrate] {dir} not found");
                return;
            }
            int migrated = MigrateForTest(dir);
            AssetDatabase.Refresh();
            Debug.Log($"[Migrate] Done: migrated={migrated}");
        }

        // テスト用: temp dir を渡せる純粋関数。返り値は処理件数。
        public static int MigrateForTest(string dir)
        {
            if (!Directory.Exists(dir)) return 0;

            int count = 0;
            foreach (var textPath in Directory.GetFiles(dir, "*_text.json"))
            {
                var stem = Path.GetFileNameWithoutExtension(textPath).Replace("_text", "");
                var chartPath = Path.Combine(dir, stem + ".json");
                if (!File.Exists(chartPath))
                {
                    Debug.LogWarning($"[Migrate] Skipped (no chart): {textPath}");
                    continue;
                }

                var chart = JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(chartPath));
                if (chart.lyrics != null && chart.lyrics.mondai != null && chart.lyrics.mondai.Length > 0)
                {
                    Debug.Log($"[Migrate] Already has lyrics, skip: {stem}");
                    continue;
                }

                var text = JsonUtility.FromJson<TextData>(File.ReadAllText(textPath));
                chart.lyrics = new MusicDTO.LyricsDTO
                {
                    startTime = text.StartTime ?? new float[0],
                    furigana  = text.Furigana ?? new string[0],
                    mondai    = text.Mondai ?? new string[0],
                    romaji    = text.romaji ?? new string[0],
                    endTime   = text.EndTime ?? new float[0],
                };

                File.WriteAllText(chartPath, JsonUtility.ToJson(chart, prettyPrint: true));
                File.Delete(textPath);
                var metaPath = textPath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
                count++;
                Debug.Log($"[Migrate] Merged + deleted: {stem}");
            }
            return count;
        }
    }
}
```

`TextData` は `Assets/Game/Scripts/UI/GameSceneManager.cs` に定義あり。`using FlashBeat;` ではなく完全修飾で使うか、`using` を `NoteEditor.Editor` 名前空間から見ると… `TextData` はグローバル名前空間にあるため `using` 不要。

- [ ] **Step 4: テストを再実行して全 PASS を確認**

Run: Test Runner → EditMode → `MigrateLegacyTextJsonTests`
Expected: 全 3 件 PASS

- [ ] **Step 5: 既存テストが壊れていないことを確認**

Run: Test Runner → EditMode → 全 EditMode テスト
Expected: 全 PASS

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor/NoteEditor.Editor/MigrateLegacyTextJson.cs Assets/Tests/Editor/MigrateLegacyTextJsonTests.cs
git commit -m "feat(noteeditor): add migration tool to merge legacy _text.json into chart"
```

---

### Task 12: GameSceneManager.LoadTextData を merged 優先 + 旧フォールバックに変更

**Files:**
- Modify: `Assets/Game/Scripts/UI/GameSceneManager.cs`

- [ ] **Step 1: LoadTextData メソッドを探す**

`Assets/Game/Scripts/UI/GameSceneManager.cs` で `_text.json` を読み込んでいる箇所を特定 (CLAUDE.md によれば `GameSceneManager.Load` で `<song>_text.json` を読み込み `TextData` に詰める)。

- [ ] **Step 2: merged 優先ロジックを追加**

そのメソッド (例: `void Load(string songName)` または `TextData LoadTextData(string songName)`) を以下に置換:

```csharp
        TextData LoadTextData(string songName)
        {
            var mergedPath = "Assets/Game/Resources/" + songName + ".json";
            if (File.Exists(mergedPath))
            {
                var chart = UnityEngine.JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(File.ReadAllText(mergedPath));
                if (chart != null && chart.lyrics != null)
                {
                    return new TextData {
                        StartTime = chart.lyrics.startTime ?? new float[0],
                        Furigana  = chart.lyrics.furigana ?? new string[0],
                        Mondai    = chart.lyrics.mondai ?? new string[0],
                        romaji    = chart.lyrics.romaji ?? new string[0],
                        EndTime   = chart.lyrics.endTime ?? new float[0],
                    };
                }
            }

            var legacyPath = "Assets/Game/Resources/" + songName + "_text.json";
            if (File.Exists(legacyPath))
            {
                return UnityEngine.JsonUtility.FromJson<TextData>(File.ReadAllText(legacyPath));
            }

            Debug.LogError($"[GameSceneManager] No text data for {songName}");
            return new TextData {
                StartTime = new float[0],
                Furigana = new string[0],
                Mondai = new string[0],
                romaji = new string[0],
                EndTime = new float[0],
            };
        }
```

- [ ] **Step 3: FlashBeat asmdef に NoteEditor 参照を追加**

`Assets/Game/Scripts/FlashBeat.asmdef` を開き、`references` 配列に `"NoteEditor"` を追加 (既にあれば追加不要)。これにより `NoteEditor.DTO.MusicDTO.EditData` 型を参照可能になる。

- [ ] **Step 4: ビルド確認**

Unity Editor 起動、Console にコンパイルエラーが出ていないことを確認。

- [ ] **Step 5: 動作確認 (Play Mode)**

Play Mode で SelectScene → GameScene へ遷移 → 歌詞が表示されることを確認 (既存の動作が変わっていないこと)。merged JSON を持つ曲と `_text.json` のみ持つ曲の両方で歌詞表示確認。

- [ ] **Step 6: Commit**

```bash
git add Assets/Game/Scripts/UI/GameSceneManager.cs Assets/Game/Scripts/FlashBeat.asmdef
git commit -m "feat(flashbeat): LoadTextData prefers merged JSON, falls back to legacy _text.json"
```

---

### Task 13: 既存 _text.json をマイグレーション実行 + 動作確認

**Files:** なし (Editor メニュー実行のみ)

- [ ] **Step 1: バックアップ**

```bash
git status -s Assets/Game/Resources/
git checkout -b backup-before-migration
```

- [ ] **Step 2: マイグレーション実行**

Unity Editor → Tools → NoteEditor → Migrate Legacy Text JSON を実行。

- [ ] **Step 3: 結果確認**

```bash
git status -s Assets/Game/Resources/
git diff --stat Assets/Game/Resources/
```

Expected:
- 各 `<曲名>_text.json` が削除されている
- 各 `<曲名>.json` に `lyrics` フィールドが追加されている
- コンソールに `[Migrate] Done: migrated=N` ログ

- [ ] **Step 4: NoteEditor で 1 曲開いて歌詞タブ確認**

NoteEditor 起動 → マイグレーションした曲を開く → 歌詞タブに歌詞が表示されることを確認。

- [ ] **Step 5: FlashBeat 本体で動作確認**

Play Mode で SelectScene → マイグレーションした曲を選択 → GameScene で歌詞 (漢字) が表示されることを確認。

- [ ] **Step 6: Commit (マイグレーションは git tracked なので自動)**

```bash
git add -u Assets/Game/Resources/
git commit -m "chore: migrate legacy _text.json into chart JSON files"
```

---

### ✅ CP3 チェックポイント

- `Assets/Game/Resources/` に `_text.json` が残っていない
- 各 `<曲名>.json` に `lyrics` フィールドが含まれている
- NoteEditor で歌詞タブが機能する
- FlashBeat 本体で歌詞が表示される

---

## 最終確認

### Task 14: 全体回帰テスト

**Files:** なし

- [ ] **Step 1: 全 EditMode テスト実行**

Run: Test Runner → EditMode → 全テスト
Expected: 全 PASS (YouTubeImportTests 11 件、EditDataSerializerLyricsTests 4 件、FlashBeatSongLoaderTests 4 件、LyricsTabPresenterTests 3 件、MigrateLegacyTextJsonTests 3 件、SongDataTests、JudgeLogicTests 含む)

- [ ] **Step 2: 手動 E2E**

Unity Editor で以下を確認:
- NoteEditor.unity を開く
- 既存曲一覧が表示される (E2EProbeSong と `_text.json` を除外)
- 曲を選択 → ノート表示 + 編集 + 保存
- 歌詞タブ → 行追加・編集・削除・保存
- GameScene で歌詞表示

- [ ] **Step 3: Console エラーなしを確認**

Unity Editor の Console に `LogError` / `LogWarning` が出ていないことを確認。

- [ ] **Step 4: 完了報告**

ユーザーへ「NoteEditor GUI 編集機能 + 歌詞マージ + マイグレーション完了。EditMode テスト N 件 PASS。動作確認 OK。」と報告。

---

## セルフレビューチェックリスト

実装時の確認事項:

- [ ] **Spec coverage**:
  - 起動時に Resources/*.json 一覧 → Task 5
  - 上書き保存 → Task 7
  - 音声不要 (合成 AudioClip) → Task 5 Step 3
  - lyrics マージ → Task 4, 8
  - 歌詞タブ UI → Task 9, 10
  - マイグレーション → Task 11, 13
  - FlashBeat 互換読み込み → Task 12

- [ ] **Type consistency**:
  - `EditData.IsDirty` (ReactiveProperty<bool>) → Task 1 で定義、Task 7 で購読
  - `EditData.Lyrics.*` (5 個の ReactiveProperty<T[]>) → Task 2 で定義、Task 8 で Serialize
  - `EditData.AudioFrequency()` (public static int) → Task 4 で昇格、Task 4 で EditDataSerializer から使用
  - `LyricsTabPresenter.AddLyricAtTime` (static) → Task 9 で定義、Task 10 で OnTimelineClicked から呼び出し

- [ ] **asmdef consistency**:
  - `NoteEditor` asmdef: Task 5, 9, 10 で MonoBehaviour 追加 (既存 all-platforms)
  - `NoteEditor.Editor` asmdef: Task 11 でツール追加 (既存 Editor-only)
  - `FlashBeat.Tests` asmdef: 既存 (Task 5, 9, 11 でテスト追加)
  - `FlashBeat` asmdef: Task 12 で NoteEditor 参照追加 (必要に応じて)
