# NoteEditor YouTube 取り込み 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal**: NoteEditor で YouTube VideoId を譜面 JSON に保存し、MakeFileScene で YouTube 配線と GManager 並列配列更新を全自動化する (spec: `docs/superpowers/specs/2026-08-07-noteeditor-youtube-import-design.md`)。

**Architecture**: 譜面 JSON に `videoId` フィールド追加 (NoteEditor DTO + Serializer 拡張) → MakeFileScene で `PrefabUtility.InstantiatePrefab` でテンプレ YoutubePlayer を複製し `MusicManager.YPlayer[]` に append → `GManager.cs` の並列配列を正規表現テキスト置換で末尾 Append。

**Tech Stack**: Unity 6000.5.6f1 / UniRx / `UnityEditor.SceneManagement` / `UnityEditor.PrefabUtility` / `UnityEditor.EditorWindow` / NUnit (EditMode tests).

---

## File Structure

**新規ファイル (作成)**:
| パス | asmdef | 役割 |
|---|---|---|
| `Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeVideoIdParser.cs` | `NoteEditor` | URL / VideoId パース (静的・テスト容易) |
| `Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeImportDialog.cs` | `NoteEditor` | モーダル EditorWindow |
| `Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeImportButtonPresenter.cs` | `NoteEditor` | Toolstrip Button 用 MonoBehaviour |
| `Assets/Editor/GManagerParallelArrayUpdater.cs` | `FlashBeat.Editor` | GManager.cs テキスト編集 |
| `Assets/Editor/SongMetaDialog.cs` | `FlashBeat.Editor` | メタ情報入力 EditorWindow |
| `Assets/Editor/YouTubeSceneWiring.cs` | `FlashBeat.Editor` | PrefabUtility + SerializedObject |
| `Assets/Editor/NoteEditorToolbarButtonInstaller.cs` | `FlashBeat.Editor` | NoteEditor.unity にボタン追加 (一回実行) |
| `Assets/Tests/Editor/YouTubeImportTests.cs` | `FlashBeat.Tests` | EditMode テスト |

**既存ファイル (修正)**:
| パス | 変更内容 |
|---|---|
| `Assets/NoteEditor/Scripts/DTO/MusicDTO.cs` | `EditData` に `videoId` フィールド追加 |
| `Assets/NoteEditor/Scripts/Model/EditData.cs` | `VideoId` ReactiveProperty 追加 |
| `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs` | `Serialize/Deserialize` で videoId 永続化 |
| `Assets/Game/Scripts/UI/MakeFileSceneManager.cs` | `#if UNITY_EDITOR` 内でオーケストレータに書き換え |

---

## Task 1: MusicDTO.EditData に videoId フィールド追加

**Files:**
- Modify: `Assets/NoteEditor/Scripts/DTO/MusicDTO.cs:8-15`

- [ ] **Step 1: videoId フィールドを追加**

`Assets/NoteEditor/Scripts/DTO/MusicDTO.cs` を以下に書き換え:

```csharp
using System.Collections.Generic;

namespace NoteEditor.DTO
{
    public class MusicDTO
    {
        [System.Serializable]
        public class EditData
        {
            public string name;
            public int maxBlock;
            public int BPM;
            public int offset;
            public string videoId;
            public List<Note> notes;
        }

        [System.Serializable]
        public class Note
        {
            public int LPB;
            public int num;
            public int block;
            public int type;
            public List<Note> notes;
        }
    }
}
```

- [ ] **Step 2: ビルド確認**

Unity Editor で Console エラー 0 を確認 (`mcp__UnityMCP__read_console types=["error"] count=5`)。

- [ ] **Step 3: Commit**

```bash
git add Assets/NoteEditor/Scripts/DTO/MusicDTO.cs
git commit -m "feat(noteeditor): add videoId field to MusicDTO.EditData"
```

---

## Task 2: EditData.VideoId ReactiveProperty 追加

**Files:**
- Modify: `Assets/NoteEditor/Scripts/Model/EditData.cs:10-22`

- [ ] **Step 1: VideoId ReactiveProperty を追加**

`Assets/NoteEditor/Scripts/Model/EditData.cs` を以下に書き換え:

```csharp
using NoteEditor.Notes;
using NoteEditor.Utility;
using System.Collections.Generic;
using UniRx;

namespace NoteEditor.Model
{
    public class EditData : SingletonMonoBehaviour<EditData>
    {
        ReactiveProperty<string> name_ = new ReactiveProperty<string>();
        ReactiveProperty<int> maxBlock_ = new ReactiveProperty<int>(5);
        ReactiveProperty<int> LPB_ = new ReactiveProperty<int>(4);
        ReactiveProperty<int> BPM_ = new ReactiveProperty<int>(120);
        ReactiveProperty<int> offsetSamples_ = new ReactiveProperty<int>(0);
        ReactiveProperty<string> videoId_ = new ReactiveProperty<string>("");
        Dictionary<NotePosition, NoteObject> notes_ = new Dictionary<NotePosition, NoteObject>();

        public static ReactiveProperty<string> Name { get { return Instance.name_; } }
        public static ReactiveProperty<int> MaxBlock { get { return Instance.maxBlock_; } }
        public static ReactiveProperty<int> LPB { get { return Instance.LPB_; } }
        public static ReactiveProperty<int> BPM { get { return Instance.BPM_; } }
        public static ReactiveProperty<int> OffsetSamples { get { return Instance.offsetSamples_; } }
        public static ReactiveProperty<string> VideoId { get { return Instance.videoId_; } }
        public static Dictionary<NotePosition, NoteObject> Notes { get { return Instance.notes_; } }
    }
}
```

- [ ] **Step 2: ビルド確認**

`mcp__UnityMCP__read_console types=["error"] count=5` で 0 エラー。

- [ ] **Step 3: Commit**

```bash
git add Assets/NoteEditor/Scripts/Model/EditData.cs
git commit -m "feat(noteeditor): add VideoId ReactiveProperty to EditData"
```

---

## Task 3: EditDataSerializer videoId 永続化 + EditMode テスト (TDD)

**Files:**
- Modify: `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs`
- Create: `Assets/Tests/Editor/YouTubeImportTests.cs`

- [ ] **Step 1: テストファイル作成 (失敗する状態)**

`Assets/Tests/Editor/YouTubeImportTests.cs` を新規作成:

```csharp
using NUnit.Framework;
using NoteEditor.DTO;
using NoteEditor.Model;
using UnityEngine;

public class YouTubeImportTests
{
    [Test]
    public void EditDataSerializer_RoundtripsVideoId()
    {
        EditData.VideoId.Value = "dQw4w9WgXcQ";
        var json = EditDataSerializer.Serialize();
        EditDataSerializer.Deserialize(json);
        Assert.AreEqual("dQw4w9WgXcQ", EditData.VideoId.Value);
    }

    [Test]
    public void EditDataSerializer_HandlesMissingVideoId()
    {
        const string legacyJson = "{\"name\":\"test\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[]}";
        Assert.DoesNotThrow(() => EditDataSerializer.Deserialize(legacyJson));
        Assert.AreEqual("", EditData.VideoId.Value);
    }

    [Test]
    public void EditDataSerializer_HandlesEmptyVideoId()
    {
        const string emptyVideoIdJson = "{\"name\":\"test\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"videoId\":\"\",\"notes\":[]}";
        EditDataSerializer.Deserialize(emptyVideoIdJson);
        Assert.AreEqual("", EditData.VideoId.Value);
    }
}
```

- [ ] **Step 2: テスト実行 → 失敗確認**

`mcp__UnityMCP__run_tests mode="EditMode" test_names=["YouTubeImportTests"]` を実行。
期待: `EditDataSerializer_RoundtripsVideoId` と `EditDataSerializer_HandlesMissingVideoId` が FAIL (videoId が DTO にまだ含まれていないため)。

- [ ] **Step 3: EditDataSerializer.Serialize に videoId 追加**

`Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs` の `Serialize()` メソッド、`dto.notes = new List<MusicDTO.Note>();` の直前に 1 行追加:

```csharp
            dto.videoId = EditData.VideoId.Value;
```

- [ ] **Step 4: EditDataSerializer.Deserialize に videoId 追加**

同ファイルの `Deserialize()` メソッド、`EditData.OffsetSamples.Value = editData.offset;` の直後に 2 行追加:

```csharp
            EditData.VideoId.Value = string.IsNullOrEmpty(editData.videoId) ? "" : editData.videoId;
```

- [ ] **Step 5: テスト再実行 → パス確認**

`mcp__UnityMCP__run_tests mode="EditMode" test_names=["YouTubeImportTests"]` を実行。
期待: 3 テスト全て PASS。

- [ ] **Step 6: Commit**

```bash
git add Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs Assets/Tests/Editor/YouTubeImportTests.cs
git commit -m "feat(noteeditor): roundtrip videoId through EditDataSerializer"
```

---

## Task 4: YouTubeVideoIdParser + 5 パターン EditMode テスト (TDD)

**Files:**
- Create: `Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeVideoIdParser.cs`
- Modify: `Assets/Tests/Editor/YouTubeImportTests.cs` (テスト追加)

- [ ] **Step 1: ディレクトリ作成 + テスト追加**

`Assets/NoteEditor/Scripts/Presenter/YouTube/` ディレクトリを新規作成 (`.meta` は Unity が自動生成)。

`Assets/Tests/Editor/YouTubeImportTests.cs` の末尾に以下を追加 (クラスは既存のものに追加):

```csharp
    [Test]
    public void VideoIdParser_ExtractsFromFullUrl()
    {
        Assert.IsTrue(YouTubeVideoIdParser.TryParse("https://www.youtube.com/watch?v=dQw4w9WgXcQ", out var id));
        Assert.AreEqual("dQw4w9WgXcQ", id);
    }

    [Test]
    public void VideoIdParser_ExtractsFromShortUrl()
    {
        Assert.IsTrue(YouTubeVideoIdParser.TryParse("https://youtu.be/dQw4w9WgXcQ", out var id));
        Assert.AreEqual("dQw4w9WgXcQ", id);
    }

    [Test]
    public void VideoIdParser_AcceptsRawId()
    {
        Assert.IsTrue(YouTubeVideoIdParser.TryParse("dQw4w9WgXcQ", out var id));
        Assert.AreEqual("dQw4w9WgXcQ", id);
    }

    [Test]
    public void VideoIdParser_RejectsEmpty()
    {
        Assert.IsFalse(YouTubeVideoIdParser.TryParse("", out var id));
        Assert.IsNull(id);
    }

    [Test]
    public void VideoIdParser_RejectsInvalid()
    {
        Assert.IsFalse(YouTubeVideoIdParser.TryParse("not a url", out var id));
        Assert.IsNull(id);
    }

    [Test]
    public void VideoIdParser_AcceptsUnderscoreAndHyphen()
    {
        Assert.IsTrue(YouTubeVideoIdParser.TryParse("_-abc-DEF_12", out var id));
        Assert.AreEqual("_-abc-DEF_1", id);
    }
```

- [ ] **Step 2: テスト実行 → 失敗確認**

`mcp__UnityMCP__run_tests mode="EditMode" test_names=["YouTubeImportTests.VideoIdParser_ExtractsFromFullUrl","YouTubeImportTests.VideoIdParser_RejectsEmpty"]` を実行。
期待: `YouTubeVideoIdParser` 型が見つからずコンパイルエラーまたはテスト FAIL。

- [ ] **Step 3: YouTubeVideoIdParser 実装**

`Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeVideoIdParser.cs` を新規作成:

```csharp
using System.Text.RegularExpressions;

namespace NoteEditor.Presenter.YouTube
{
    public static class YouTubeVideoIdParser
    {
        private static readonly Regex Pattern = new Regex(
            @"(?:v=|youtu\.be/|^)([A-Za-z0-9_-]{11})",
            RegexOptions.Compiled
        );

        public static bool TryParse(string input, out string videoId)
        {
            videoId = null;
            if (string.IsNullOrWhiteSpace(input)) return false;
            var match = Pattern.Match(input.Trim());
            if (!match.Success) return false;
            videoId = match.Groups[1].Value;
            return true;
        }
    }
}
```

- [ ] **Step 4: テスト再実行 → 全 9 テスト PASS 確認**

`mcp__UnityMCP__run_tests mode="EditMode" test_names=["YouTubeImportTests"]` を実行。
期待: 全テスト PASS。

- [ ] **Step 5: Commit**

```bash
git add Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeVideoIdParser.cs Assets/Tests/Editor/YouTubeImportTests.cs
git commit -m "feat(noteeditor): add YouTubeVideoIdParser with 6 EditMode tests"
```

---

## Task 5: YouTubeImportDialog EditorWindow + メニュー項目

**Files:**
- Create: `Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeImportDialog.cs`

- [ ] **Step 1: ダイアログ実装**

`Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeImportDialog.cs` を新規作成:

```csharp
using NoteEditor.Model;
using UnityEditor;
using UnityEngine;

namespace NoteEditor.Presenter.YouTube
{
    public class YouTubeImportDialog : EditorWindow
    {
        private string _input = "";
        private string _errorMessage = "";

        [MenuItem("Tools/NoteEditor/Import YouTube URL...")]
        public static void Open()
        {
            var window = GetWindow<YouTubeImportDialog>(true, "YouTube VideoId 取り込み", true);
            window.minSize = new Vector2(420, 120);
            window.maxSize = new Vector2(420, 120);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("YouTube URL または VideoId (11文字) を入力:", EditorStyles.boldLabel);
            _input = EditorGUILayout.TextField(_input);

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
            }

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_input)))
                {
                    if (GUILayout.Button("Import"))
                    {
                        if (YouTubeVideoIdParser.TryParse(_input, out var videoId))
                        {
                            EditData.VideoId.Value = videoId;
                            Debug.Log($"[YouTubeImport] VideoId set: {videoId}");
                            Close();
                        }
                        else
                        {
                            _errorMessage = "VideoId を抽出できませんでした。形式を確認してください。";
                        }
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 2: ビルド確認**

`mcp__UnityMCP__read_console types=["error"] count=5` で 0 エラー。

- [ ] **Step 3: 手動動作確認 (Editor で実行)**

Unity Editor で `Tools/NoteEditor/Import YouTube URL...` メニューが開くことを確認 (目視のみ)。

- [ ] **Step 4: Commit**

```bash
git add Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeImportDialog.cs
git commit -m "feat(noteeditor): add YouTubeImportDialog EditorWindow with menu item"
```

---

## Task 6: GManagerParallelArrayUpdater + EditMode テスト (TDD)

**Files:**
- Create: `Assets/Editor/GManagerParallelArrayUpdater.cs`
- Modify: `Assets/Tests/Editor/YouTubeImportTests.cs`

- [ ] **Step 1: テスト追加**

`Assets/Tests/Editor/YouTubeImportTests.cs` の末尾に以下を追加:

```csharp
    [Test]
    public void GManagerUpdater_AppendsEntryToSongName()
    {
        const string input =
            "public static readonly string[] SongName = { \"noSong\", \"AAA\", \"BBB\" };\n" +
            "public static readonly string[] Musician = { \"no\", \"x\", \"y\" };\n" +
            "public static readonly string[] SongURL = { \"v1\", \"v2\", \"v3\" };\n" +
            "public static readonly int[] SBPM = { 100, 120, 130 };\n" +
            "public static readonly int[] Slevel = { 10, 8, 9 };\n" +
            "public static readonly int[] Shit = { 100, 200, 300 };\n" +
            "public static readonly float[] SongLong = { 10000.0f, 80.0f, 90.0f };\n" +
            "public static readonly int[] Hiscore = new int[42];\n";
        var meta = new GManagerParallelArrayUpdater.SongMeta
        {
            Title = "NewSong",
            Musician = "artist",
            VideoId = "abcdefghijk",
            Bpm = 150,
            Level = 5,
            TotalHits = 250,
            Duration = 100.0f
        };

        var output = GManagerParallelArrayUpdater.ApplyEdits(input, meta);

        StringAssert.Contains("\"NewSong\"", output);
        StringAssert.Contains("\"artist\"", output);
        StringAssert.Contains("\"abcdefghijk\"", output);
        StringAssert.Contains("150", output);
        StringAssert.Contains("new int[43]", output);
    }

    [Test]
    public void GManagerUpdater_HandlesMultiLineArrays()
    {
        const string input =
            "public static readonly string[] SongName = {\n" +
            "    \"noSong\",\n" +
            "    \"AAA\"\n" +
            "};\n";
        var meta = new GManagerParallelArrayUpdater.SongMeta { Title = "Multi" };

        var output = GManagerParallelArrayUpdater.ApplyEdits(input, meta);
        StringAssert.Contains("\"Multi\"", output);
    }
```

- [ ] **Step 2: テスト実行 → 失敗確認**

`mcp__UnityMCP__run_tests mode="EditMode" test_names=["YouTubeImportTests.GManagerUpdater_AppendsEntryToSongName","YouTubeImportTests.GManagerUpdater_HandlesMultiLineArrays"]` を実行。
期待: `GManagerParallelArrayUpdater` 型未定義で FAIL / コンパイルエラー。

- [ ] **Step 3: GManagerParallelArrayUpdater 実装**

`Assets/Editor/GManagerParallelArrayUpdater.cs` を新規作成:

```csharp
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class GManagerParallelArrayUpdater
{
    public class SongMeta
    {
        public string Title;
        public string Musician = "";
        public string VideoId = "";
        public int Bpm = 120;
        public int Level = 1;
        public int TotalHits = 0;
        public float Duration = 0f;
    }

    private const string GManagerPath = "Assets/Game/Scripts/Songs/GManager.cs";

    public static bool AppendSongEntry(SongMeta meta)
    {
        if (!File.Exists(GManagerPath))
        {
            Debug.LogError($"[GManagerUpdater] GManager.cs not found at {GManagerPath}");
            return false;
        }

        var original = File.ReadAllText(GManagerPath);
        var updated = ApplyEdits(original, meta);
        if (updated == original) return false;

        var backupPath = GManagerPath + ".bak";
        File.Copy(GManagerPath, backupPath, overwrite: true);
        try
        {
            File.WriteAllText(GManagerPath, updated);
            File.Delete(backupPath);
            AssetDatabase.Refresh();
            Debug.Log($"[GManagerUpdater] Appended '{meta.Title}' to GManager parallel arrays.");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GManagerUpdater] Failed to write GManager.cs: {e.Message}. Restoring backup.");
            File.Copy(backupPath, GManagerPath, overwrite: true);
            File.Delete(backupPath);
            return false;
        }
    }

    public static string ApplyEdits(string source, SongMeta meta)
    {
        source = InsertIntoStringArray(source, "SongName", meta.Title);
        source = InsertIntoStringArray(source, "Musician", meta.Musician ?? "");
        source = InsertIntoStringArray(source, "SongURL", meta.VideoId ?? "");
        source = InsertIntoIntArray(source, "SBPM", meta.Bpm);
        source = InsertIntoIntArray(source, "Slevel", meta.Level);
        source = InsertIntoIntArray(source, "Shit", meta.TotalHits);
        source = InsertIntoFloatArray(source, "SongLong", meta.Duration);
        source = ExtendHiscore(source);
        return source;
    }

    private static string InsertIntoStringArray(string source, string fieldName, string value)
    {
        var pattern = $@"public static readonly string\[\] {fieldName} = \{{([^{{}}]*?)\}};";
        var match = Regex.Match(source, pattern, RegexOptions.Singleline);
        if (!match.Success) return source;

        var body = match.Groups[1].Value.TrimEnd().TrimEnd(',');
        var escaped = value.Replace("\"", "\\\"");
        var newBody = body + ", \"" + escaped + "\"";
        return source.Substring(0, match.Groups[0].Index) +
               $"public static readonly string[] {fieldName} = {{{newBody}}};" +
               source.Substring(match.Groups[0].Index + match.Groups[0].Length);
    }

    private static string InsertIntoIntArray(string source, string fieldName, int value)
    {
        var pattern = $@"public static readonly int\[\] {fieldName} = \{{([^{{}}]*?)\}};";
        var match = Regex.Match(source, pattern, RegexOptions.Singleline);
        if (!match.Success) return source;

        var body = match.Groups[1].Value.TrimEnd().TrimEnd(',');
        var newBody = body + ", " + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return source.Substring(0, match.Groups[0].Index) +
               $"public static readonly int[] {fieldName} = {{{newBody}}};" +
               source.Substring(match.Groups[0].Index + match.Groups[0].Length);
    }

    private static string InsertIntoFloatArray(string source, string fieldName, float value)
    {
        var pattern = $@"public static readonly float\[\] {fieldName} = \{{([^{{}}]*?)\}};";
        var match = Regex.Match(source, pattern, RegexOptions.Singleline);
        if (!match.Success) return source;

        var body = match.Groups[1].Value.TrimEnd().TrimEnd(',');
        var f = value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "f";
        var newBody = body + ", " + f;
        return source.Substring(0, match.Groups[0].Index) +
               $"public static readonly float[] {fieldName} = {{{newBody}}};" +
               source.Substring(match.Groups[0].Index + match.Groups[0].Length);
    }

    private static string ExtendHiscore(string source)
    {
        var pattern = @"public static readonly int\[\] Hiscore = new int\[(\d+)\];";
        var match = Regex.Match(source, pattern);
        if (!match.Success) return source;
        var current = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var replacement = $"public static readonly int[] Hiscore = new int[{current + 1}];";
        return source.Substring(0, match.Index) + replacement + source.Substring(match.Index + match.Length);
    }
}
```

- [ ] **Step 4: テスト再実行 → 全 11 テスト PASS 確認**

`mcp__UnityMCP__run_tests mode="EditMode" test_names=["YouTubeImportTests"]` を実行。
期待: 全テスト PASS。

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/GManagerParallelArrayUpdater.cs Assets/Tests/Editor/YouTubeImportTests.cs
git commit -m "feat(editor): add GManagerParallelArrayUpdater with text-edit logic"
```

---

## Task 7: SongMetaDialog EditorWindow

**Files:**
- Create: `Assets/Editor/SongMetaDialog.cs`

- [ ] **Step 1: SongMetaDialog 実装**

`Assets/Editor/SongMetaDialog.cs` を新規作成:

```csharp
using UnityEditor;
using UnityEngine;

public class SongMetaDialog : EditorWindow
{
    private GManagerParallelArrayUpdater.SongMeta _meta;
    private bool _accepted;

    public static GManagerParallelArrayUpdater.SongMeta ShowAndCollect(string defaultTitle, int defaultBpm)
    {
        var window = CreateInstance<SongMetaDialog>();
        window.titleContent = new GUIContent("楽曲情報入力");
        window._meta = new GManagerParallelArrayUpdater.SongMeta
        {
            Title = defaultTitle,
            Bpm = defaultBpm,
            Musician = "",
            VideoId = "",
            Level = 1,
            TotalHits = 0,
            Duration = 0f
        };
        window._accepted = false;
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 380, 260);
        window.ShowModalUtility();
        return window._accepted ? window._meta : null;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("新曲のメタデータを入力:", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _meta.Title = EditorGUILayout.TextField("タイトル (必須)", _meta.Title);
        _meta.Musician = EditorGUILayout.TextField("アーティスト (任意)", _meta.Musician);
        _meta.VideoId = EditorGUILayout.TextField("VideoId (任意)", _meta.VideoId);
        _meta.Bpm = EditorGUILayout.IntField("BPM", _meta.Bpm);
        _meta.Level = EditorGUILayout.IntField("レベル", _meta.Level);
        _meta.TotalHits = EditorGUILayout.IntField("総ノーツ数", _meta.TotalHits);
        _meta.Duration = EditorGUILayout.FloatField("尺 (秒)", _meta.Duration);

        EditorGUILayout.Space();
        GUILayout.FlexibleSpace();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Cancel"))
            {
                _accepted = false;
                Close();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_meta.Title)))
            {
                if (GUILayout.Button("OK"))
                {
                    _accepted = true;
                    Close();
                }
            }
        }
    }
}
```

- [ ] **Step 2: ビルド確認**

`mcp__UnityMCP__read_console types=["error"] count=5` で 0 エラー。

- [ ] **Step 3: Commit**

```bash
git add Assets/Editor/SongMetaDialog.cs
git commit -m "feat(editor): add SongMetaDialog EditorWindow for new song metadata"
```

---

## Task 8: YouTubeSceneWiring (PrefabUtility による GameScene 配線)

**Files:**
- Create: `Assets/Editor/YouTubeSceneWiring.cs`

> **注**: このタスクは手動検証のみ。シーンを実際にロード・保存するため、EditMode テストでは安定して検証できない。

- [ ] **Step 1: YouTubeSceneWiring 実装**

`Assets/Editor/YouTubeSceneWiring.cs` を新規作成:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YoutubePlayer.Components;

public static class YouTubeSceneWiring
{
    private const string GameScenePath = "Assets/Game/Scenes/GameScene.unity";

    public static bool WireYouTubeToGameScene(string songName, string videoId)
    {
        var loadedScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        var musicManager = Object.FindObjectsOfType<MusicManager>();
        if (musicManager.Length == 0)
        {
            Debug.LogError("[YouTubeSceneWiring] MusicManager not found in GameScene.");
            return false;
        }
        var mm = musicManager[0];

        var template = mm.YPlayer != null && mm.YPlayer.Length > 0 ? mm.YPlayer[0] : null;
        if (template == null)
        {
            Debug.LogError("[YouTubeSceneWiring] MusicManager.YPlayer[0] is null. Set a template YoutubePlayer in GameScene.");
            return false;
        }

        var targetName = "YoutubePlayer_" + songName;
        var existing = GameObject.Find(targetName);
        if (existing != null)
        {
            Debug.LogWarning($"[YouTubeSceneWiring] '{targetName}' already exists in GameScene. Skipping.");
            return true;
        }

        var clone = (GameObject)PrefabUtility.InstantiatePrefab(template);
        clone.name = targetName;

        var ivp = clone.GetComponent<InvidiousVideoPlayer>();
        if (ivp != null)
        {
            var so = new SerializedObject(ivp);
            var prop = so.FindProperty("VideoId");
            if (prop != null)
            {
                prop.stringValue = videoId;
                so.ApplyModifiedProperties();
            }
        }

        var mmSO = new SerializedObject(mm);
        var yPlayerProp = mmSO.FindProperty("YPlayer");
        if (yPlayerProp != null && yPlayerProp.isArray)
        {
            yPlayerProp.arraySize++;
            yPlayerProp.GetArrayElementAtIndex(yPlayerProp.arraySize - 1).objectReferenceValue = clone;
            mmSO.ApplyModifiedProperties();
        }

        var vPlayerProp = mmSO.FindProperty("VPlayer");
        if (vPlayerProp != null && vPlayerProp.isArray)
        {
            vPlayerProp.arraySize++;
            vPlayerProp.GetArrayElementAtIndex(vPlayerProp.arraySize - 1).objectReferenceValue = null;
            mmSO.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(loadedScene);
        EditorSceneManager.SaveScene(loadedScene);
        Debug.Log($"[YouTubeSceneWiring] Wired '{targetName}' with VideoId '{videoId}'.");
        return true;
    }
}
```

- [ ] **Step 2: ビルド確認**

`mcp__UnityMCP__read_console types=["error"] count=5` で 0 エラー (PrefabUtility / SerializedObject / YoutubePlayer 名前空間すべて解決されること)。

- [ ] **Step 3: 手動動作確認 (GameScene がブランク状態なので、テスト用に既存 YoutubePlayer GameObject をテンプレとして残す必要あり)**

実機検証は Task 11 で行う。本ステップはコンパイル確認のみ。

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/YouTubeSceneWiring.cs
git commit -m "feat(editor): add YouTubeSceneWiring (PrefabUtility clone + VideoId + YPlayer append)"
```

---

## Task 9: MakeFileSceneManager オーケストレータ書換

**Files:**
- Modify: `Assets/Game/Scripts/UI/MakeFileSceneManager.cs`

> **重要**: ファイル全体を書き換える。既存テスト用ハードコードは新フローに置換される。歌詞 `_text.json` 出力は維持。

- [ ] **Step 1: MakeFileSceneManager を以下に書換**

`Assets/Game/Scripts/UI/MakeFileSceneManager.cs` を以下に置換:

```csharp
using System.IO;
using UnityEngine;

public class MakeFileSceneManager : MonoBehaviour
{
#if UNITY_EDITOR
    private const string ResourcesDir = "Assets/Game/Resources";

    void Start()
    {
        var jsonPath = UnityEditor.EditorUtility.OpenFilePanel("譜面 JSON を選択", ResourcesDir, "json");
        if (string.IsNullOrEmpty(jsonPath)) return;

        var json = File.ReadAllText(jsonPath);
        var chart = JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(json);
        var songName = Path.GetFileNameWithoutExtension(jsonPath);

        var meta = SongMetaDialog.ShowAndCollect(songName, chart.BPM);
        if (meta == null) return;

        WriteTextJsonStub(songName);

        if (!string.IsNullOrEmpty(chart.videoId))
        {
            if (!YouTubeSceneWiring.WireYouTubeToGameScene(songName, chart.videoId))
            {
                Debug.LogError($"[MakeFile] YouTube wiring failed for {songName}");
            }
        }

        meta.VideoId = chart.videoId ?? "";
        if (!GManagerParallelArrayUpdater.AppendSongEntry(meta))
        {
            Debug.LogError($"[MakeFile] GManager update failed for {songName}");
        }

        UnityEditor.AssetDatabase.Refresh();
        Debug.Log($"[MakeFile] Done: {songName}");
    }

    private void WriteTextJsonStub(string songName)
    {
        var data = new TextData
        {
            StartTime = new float[] { },
            Furigana = new string[] { },
            Mondai = new string[] { },
            romaji = new string[] { },
            EndTime = new float[] { }
        };
        var json = JsonUtility.ToJson(data);
        var path = Path.Combine(ResourcesDir, songName + "_text.json");
        File.WriteAllText(path, json);
        Debug.Log($"[MakeFile] Wrote stub: {path}");
    }
#else
    void Start() { }
#endif
}
```

- [ ] **Step 2: ビルド確認**

`mcp__UnityMCP__read_console types=["error"] count=5` で 0 エラー。
(注意: `TextData` 型が同 asmdef 内に存在することを確認 — 既存コードで使用されているため OK。)

- [ ] **Step 3: Commit**

```bash
git add Assets/Game/Scripts/UI/MakeFileSceneManager.cs
git commit -m "refactor(makefile): rewrite as orchestrator with #if UNITY_EDITOR guard"
```

---

## Task 10: YouTubeImportButtonPresenter (Toolstrip 用 MonoBehaviour)

**Files:**
- Create: `Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeImportButtonPresenter.cs`

- [ ] **Step 1: Presenter 実装**

`Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeImportButtonPresenter.cs` を新規作成:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace NoteEditor.Presenter.YouTube
{
    public class YouTubeImportButtonPresenter : MonoBehaviour
    {
        [SerializeField]
        Button importButton = default;

        void Awake()
        {
            if (importButton != null)
            {
                importButton.onClick.AddListener(YouTubeImportDialog.Open);
            }
        }
    }
}
```

- [ ] **Step 2: ビルド確認**

`mcp__UnityMCP__read_console types=["error"] count=5` で 0 エラー。

- [ ] **Step 3: Commit**

```bash
git add Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeImportButtonPresenter.cs
git commit -m "feat(noteeditor): add YouTubeImportButtonPresenter MonoBehaviour"
```

---

## Task 11: NoteEditorToolbarButtonInstaller (一回限りのシーン書換)

**Files:**
- Create: `Assets/Editor/NoteEditorToolbarButtonInstaller.cs`

- [ ] **Step 1: Installer 実装**

`Assets/Editor/NoteEditorToolbarButtonInstaller.cs` を新規作成:

```csharp
using NoteEditor.Presenter.YouTube;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class NoteEditorToolbarButtonInstaller
{
    private const string ScenePath = "Assets/NoteEditor/Scenes/NoteEditor.unity";
    private const string ButtonName = "YouTubeImportButton";

    [MenuItem("Tools/NoteEditor/Install YouTube Import Button")]
    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var existing = GameObject.Find(ButtonName);
        if (existing != null)
        {
            Debug.Log($"[Installer] '{ButtonName}' already exists. Skipping.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }

        var toolstripRoot = GameObject.Find("Toolstrip");
        if (toolstripRoot == null)
        {
            Debug.LogError("[Installer] 'Toolstrip' GameObject not found in NoteEditor.unity. " +
                           "Open the scene and identify the toolbar parent GameObject name.");
            return;
        }

        var go = new GameObject(ButtonName);
        go.transform.SetParent(toolstripRoot.transform, worldPositionStays: false);
        go.AddComponent<RectTransform>();
        go.AddComponent<CanvasRenderer>();
        var image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.6f, 0.9f, 1f);

        var button = go.AddComponent<Button>();
        var presenter = go.AddComponent<YouTubeImportButtonPresenter>();

        var soButton = new SerializedObject(button);
        var soPresenter = new SerializedObject(presenter);

        soButton.FindProperty("m_Colors.m_NormalColor").colorValue = new Color(1f, 1f, 1f, 1f);
        soButton.ApplyModifiedProperties();

        soPresenter.FindProperty("importButton").objectReferenceValue = button;
        soPresenter.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Installer] Installed '{ButtonName}' under Toolstrip. Button opens YouTubeImportDialog on click.");
    }
}
```

- [ ] **Step 2: ビルド確認**

`mcp__UnityMCP__read_console types=["error"] count=5` で 0 エラー。

- [ ] **Step 3: 手動インストーラ実行 (1 回のみ)**

Unity Editor でメニュー `Tools/NoteEditor/Install YouTube Import Button` を実行。
期待: Console に `[Installer] Installed 'YouTubeImportButton' under Toolstrip. ...` が出力される。

- [ ] **Step 4: NoteEditor.unity の変更を Commit**

```bash
git add Assets/NoteEditor/Scenes/NoteEditor.unity Assets/NoteEditor/Scenes/NoteEditor.unity.meta Assets/Editor/NoteEditorToolbarButtonInstaller.cs
git commit -m "feat(noteeditor): install YouTubeImportButton into NoteEditor toolbar"
```

- [ ] **Step 5: 冪等性確認**

再度 `Tools/NoteEditor/Install YouTube Import Button` を実行。
期待: `[Installer] 'YouTubeImportButton' already exists. Skipping.` が出る (重複作成されない)。

---

## Task 12: 最終統合検証 (全テスト + 手動フロー)

**Files:**
- (変更なし — 検証のみ)

- [ ] **Step 1: 全 EditMode テスト実行**

`mcp__UnityMCP__run_tests mode="EditMode"` を実行。
期待: 全 11 テスト PASS (3 from Task 3, 6 from Task 4, 2 from Task 6)。

- [ ] **Step 2: ビルドが clean か確認**

`mcp__UnityMCP__read_console types=["error","warning"] count=20` で warning 0 確認。

- [ ] **Step 3: NoteEditor 手動フロー (spec §8-1)**

1. NoteEditor シーンを開く
2. テスト用譜面 JSON (`{"name":"Test","maxBlock":8,"BPM":120,"offset":0,"videoId":"dQw4w9WgXcQ","notes":[]}`) を `Assets/Game/Resources/Test.json` に配置
3. NoteEditor で Test.json をロード
4. Toolbar の YouTube Import Button をクリック (または `Tools/NoteEditor/Import YouTube URL...`)
5. VideoId が EditData に反映されていることを確認
6. Save → Test.json に `"videoId":"dQw4w9WgXcQ"` が含まれることを確認

- [ ] **Step 4: MakeFile 手動フロー (spec §8-2)**

1. MakeFileScene を開く → Play
2. Test.json を選択
3. メタ情報入力ダイアログ → 入力 (例: title=Test, artist=tester, bpm=120, level=1)
4. 実行完了ログを確認
5. 確認項目:
   - `Assets/Game/Resources/Test_text.json` が空テンプレで作成された
   - GameScene に `YoutubePlayer_Test` GameObject が新規作成された
   - `InvidiousVideoPlayer.VideoId` が `dQw4w9WgXcQ` になっている
   - `MusicManager.YPlayer[]` に新エントリが append されている
   - `GManager.cs` の 7 配列末尾に新エントリが追加されている
   - `GManager.Hiscore` の長さが `+1` されている
6. 再実行 → 重複作成されない (冪等性)

- [ ] **Step 5: 後方互換確認 (spec §8-3)**

1. 既存の `Assets/Game/Resources/ロストアンブレラ.json` (videoId 無し) を NoteEditor でロード
2. → 例外が出ず `EditData.VideoId.Value` が `""` になることを確認
3. JSON を再保存 → `"videoId":""` が追加されている

- [ ] **Step 6: 問題があれば追加修正 → Commit**

検証で問題が見つかった場合、修正 → 個別コミット。
問題なければ本タスクは **コミットなし** で完了。

---

## Spec Coverage Check

| Spec セクション | カバー Task |
|---|---|
| §2-1 MusicDTO.videoId | Task 1 |
| §2-2 EditData.VideoId ReactiveProperty | Task 2 |
| §2-3 EditDataSerializer roundtrip | Task 3 |
| §2-4 GManager.SongData 変更なし | (タスクなし — 確認のみ) |
| §3-1 ツールバーボタン + メニュー項目 | Task 10, 11 |
| §3-2 YouTubeImportDialog | Task 5 |
| §3-3 YouTubeVideoIdParser | Task 4 |
| §4-1 MakeFileScene 全体フロー | Task 9 |
| §4-2 新規ファイル一覧 | Task 6, 7, 8, 11 |
| §4-3 YouTubeSceneWiring | Task 8 |
| §4-4 GManagerParallelArrayUpdater | Task 6 |
| §4-5 MakeFileSceneManager オーケストレータ | Task 9 |
| §6 テスト戦略 (11 EditMode テスト) | Task 3 (3), Task 4 (6), Task 6 (2) |
| §8 検証手順 | Task 12 |

**追加でカバー (spec で触れたが独立タスク化していない項目)**:
- asmdef 境界 (#if UNITY_EDITOR): Task 9 で対応

スコープ外項目 (spec §10) は意図的にタスク化していない:
- 歌詞入力 UI (サブプロジェクト 3)
- 新曲のインゲームプレビュー
