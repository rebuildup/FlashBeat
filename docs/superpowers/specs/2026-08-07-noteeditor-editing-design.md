# NoteEditor GUI 編集機能 設計

**日付**: 2026-08-07
**スコープ**: NoteEditor 統合シーンの譜面 GUI 編集機能の有効化、歌詞データの統合、音声ファイル依存の除去
**ステータス**: Draft

## 背景

FlashBeat に統合された NoteEditor (`Assets/NoteEditor/`) は譜面作成ツールとして配置されているが、現状ユーザーが GUI から譜面を編集できない:

1. NoteEditor の `MusicLoader.Load(fileName)` は `<WorkSpacePath>/Musics/<fileName>.wav` を `WWW` で読み込み、`Audio.Source.clip` をセットしてから `Audio.OnLoad.OnNext(Unit.Default)` を発火する設計。FlashBeat は YouTube 再生のみでローカル `.wav` を持たないため、NoteEditor を起動しても譜面リストは空、ロードもできない。
2. 保存先は `<WorkSpacePath>/Notes/<fileName>.json` で、`Assets/Game/Resources/` ではないため、YouTubeImportDialog 経由でしか FlashBeat 本体に反映できない。
3. 歌詞データ (`*_text.json`) は別ファイルで管理されており、NoteEditor 内では編集できない。

ユーザー要求:
- 既存曲の GUI 編集 (`Assets/Game/Resources/*.json` を一覧から選択 → ロード → 編集 → 上書き保存)
- 新規譜面の作成
- 音声ファイル依存の完全除去 (著作権的理由)
- `_text.json` を notes JSON に統合
- 歌詞編集 UI を NoteEditor シーン内に追加

## 目標

- NoteEditor を起動すると `Assets/Game/Resources/*.json` が一覧表示され、選択すると編集可能になる
- `Ctrl+S` で `Assets/Game/Resources/<曲名>.json` に直接上書き保存される
- 歌詞データは notes JSON 内の `lyrics` フィールドに統合される
- NoteEditor シーン内に歌詞編集タブが追加され、タイムラインから Mondai を追加・編集・削除できる
- 既存の `*_text.json` を notes JSON にマイグレーションするツールを提供する
- FlashBeat 本体は新フォーマットで歌詞を読み込む (旧 `_text.json` へのフォールバックも残す)
- ローカル音声ファイル (`.wav`) のロード処理を完全に削除する

## 非目標

- YouTube 音声の NoteEditor 内プレビュー再生
- 譜面の Git diff / マージ支援
- MIDI インポート
- 譜面のリアルタイム共同編集

## アーキテクチャ

```
NoteEditor.unity シーン (起動時)
  ├─ FlashBeatSongLoader (新)        ← Resources/*.json を一覧 + ロード
  │    ├─ Audio.OnLoad.OnNext(Unit.Default) を擬似発火
  │    └─ 古い MusicSelector/MusicLoader は no-op 化
  ├─ NoteCanvas + 既存 Presenter 群   ← InputNotesByKeyboard / EditNotes 等
  ├─ LyricsTabPresenter (新)          ← 歌詞編集タブ (タイムライン連動)
  └─ SavePresenter (改修)              ← Resources/<曲>.json 直保存

Editor (Tools/NoteEditor/* メニュー)
  ├─ Open NoteEditor                    ← 既存
  ├─ Migrate Legacy Text JSON (新)      ← *_text.json → notes.json マージ + 旧ファイル削除
  └─ Import YouTube URL...              ← 既存
```

### データフロー

1. NoteEditor シーン起動 → `FlashBeatSongLoader.Start()` が `Assets/Game/Resources/*.json` を列挙
2. 曲クリック → JSON テキスト読み込み → `EditDataSerializer.Deserialize(json)` → `Audio.OnLoad.OnNext(Unit.Default)` 擬似発火
3. 既存 Presenter 群 (`InputNotesByKeyboardPresenter`, `EditNotesPresenter`, `SavePresenter`) は `Audio.OnLoad` 待ち → 起動 → 編集可能
4. `Ctrl+S` or Save ボタン → `SavePresenter.Save()` → `Assets/Game/Resources/<曲>.json` 上書き
5. 歌詞タブ → `EditData.Lyrics.*` を読み書き → 同じシリアライザで保存

## データモデル

### JSON 形式 (歌詞マージ後)

```json
{
  "name": "4th smile",
  "maxBlock": 8,
  "BPM": 140,
  "offset": 5,
  "videoId": "DeKLpgzh-qQ",
  "notes": [{"type":1,"num":32,"block":4,"LPB":4}, ...],
  "lyrics": {
    "startTime": [12.5, 13.0],
    "furigana":  ["漢字", "かんじ"],
    "mondai":    ["今日", "漢字"],
    "romaji":    ["kyou", "kanji"],
    "endTime":   [13.0, 13.5]
  }
}
```

`UnityEngine.JsonUtility` は未知フィールドを無視するので、旧形式 (lyrics フィールド無し) も問題なく読める。

### EditData (C# モデル)

```csharp
namespace NoteEditor.Model
{
    public class EditData : SingletonMonoBehaviour<EditData>
    {
        // 既存フィールドはそのまま
        ReactiveProperty<string> name_ = new ReactiveProperty<string>();
        ReactiveProperty<int> maxBlock_ = new ReactiveProperty<int>(5);
        ReactiveProperty<int> LPB_ = new ReactiveProperty<int>(4);
        ReactiveProperty<int> BPM_ = new ReactiveProperty<int>(120);
        ReactiveProperty<int> offsetSamples_ = new ReactiveProperty<int>(0);
        ReactiveProperty<string> videoId_ = new ReactiveProperty<string>("");
        ReactiveProperty<bool> isDirty_ = new ReactiveProperty<bool>(false);
        Dictionary<NotePosition, NoteObject> notes_ = new Dictionary<NotePosition, NoteObject>();

        public static class Lyrics
        {
            public static ReactiveProperty<float[]> StartTime = new(new float[0]);
            public static ReactiveProperty<string[]> Furigana = new(new string[0]);
            public static ReactiveProperty<string[]> Mondai = new(new string[0]);
            public static ReactiveProperty<string[]> Romaji = new(new string[0]);
            public static ReactiveProperty<float[]> EndTime = new(new float[0]);
        }

        public static ReactiveProperty<string> Name { get { return Instance.name_; } }
        public static ReactiveProperty<int> MaxBlock { get { return Instance.maxBlock_; } }
        public static ReactiveProperty<int> LPB { get { return Instance.LPB_; } }
        public static ReactiveProperty<int> BPM { get { return Instance.BPM_; } }
        public static ReactiveProperty<int> OffsetSamples { get { return Instance.offsetSamples_; } }
        public static ReactiveProperty<string> VideoId { get { return Instance.videoId_; } }
        public static ReactiveProperty<bool> IsDirty { get { return Instance.isDirty_; } }
        public static Dictionary<NotePosition, NoteObject> Notes { get { return Instance.notes_; } }
    }
}
```

### MusicDTO (JSON シリアライズ用)

```csharp
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
            public LyricsDTO lyrics;  // null 許容 (旧ファイルとの互換)
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

        [System.Serializable]
        public class LyricsDTO
        {
            public float[] startTime;
            public string[] furigana;
            public string[] mondai;
            public string[] romaji;
            public float[] endTime;
        }
    }
}
```

## コンポーネント詳細

### 1. `FlashBeatSongLoader.cs` (新規)

`MusicLoader`/`MusicSelector` の代替。シーンに 1 つ配置。

```csharp
namespace NoteEditor.Presenter.FlashBeatSong
{
    public class FlashBeatSongLoader : MonoBehaviour
    {
        const string ResourcesDir = "Assets/Game/Resources";

        [SerializeField] Transform fileItemContainer = default;
        [SerializeField] GameObject fileItemPrefab = default;
        [SerializeField] Text emptyMessageText = default;
        [SerializeField] Button refreshButton = default;

        void Start()
        {
            if (refreshButton != null) refreshButton.onClick.AddListener(RefreshList);
            RefreshList();
        }

        public void RefreshList()
        {
            foreach (Transform child in fileItemContainer) Destroy(child.gameObject);

            if (!Directory.Exists(ResourcesDir))
            {
                Directory.CreateDirectory(ResourcesDir);
                Debug.Log($"[FlashBeatSongLoader] Created {ResourcesDir}");
            }

            var jsonPaths = Directory.GetFiles(ResourcesDir, "*.json")
                .Where(p => !Path.GetFileNameWithoutExtension(p).EndsWith("_text"))
                .Where(p => Path.GetFileNameWithoutExtension(p) != "E2EProbeSong")
                .OrderBy(p => p)
                .ToList();

            foreach (var path in jsonPaths)
            {
                var item = Instantiate(fileItemPrefab, fileItemContainer);
                item.GetComponent<FileListItem>().SetInfo(new FileItemInfo(false, path));
                var btn = item.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(() => OnFileSelected(path));
            }

            if (emptyMessageText != null) emptyMessageText.gameObject.SetActive(jsonPaths.Count == 0);
        }

        public void OnFileSelected(string jsonPath)
        {
            try
            {
                var json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                EditDataSerializer.Deserialize(json);
                EditData.Name.Value = Path.GetFileNameWithoutExtension(jsonPath);
                EditData.IsDirty.Value = false;
                ProvideSyntheticAudioClip();
                Audio.OnLoad.OnNext(Unit.Default);
                Debug.Log($"[FlashBeatSongLoader] Loaded: {jsonPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FlashBeatSongLoader] Failed to load {jsonPath}: {ex.Message}");
            }
        }

        // 44100Hz / 1秒 / 無音の合成 AudioClip を Audio.Source.clip にセット。
        // PlaybackPositionPresenter 等、Audio.OnLoad 後に Audio.Source.clip を
        // null-guard せず参照する Presenter が NPE しないために必要。
        static void ProvideSyntheticAudioClip()
        {
            const int sampleRate = 44100;
            const float lengthSeconds = 1f;
            int sampleCount = Mathf.CeilToInt(sampleRate * lengthSeconds);
            var silent = new float[sampleCount];
            var clip = AudioClip.Create("__FlashBeatSynthetic", sampleCount, 1, sampleRate, false);
            clip.SetData(silent, 0);
            Audio.Source.clip = clip;
        }
    }
}
```

注意: NoteEditor asmdef は `includePlatforms: []` (全プラットフォーム) のため `UnityEditor.EditorUtility.DisplayDialog` は使えない。エラーは `Debug.LogError` のみ。

### 2. `MusicLoader.cs` / `MusicSelectorPresenter.cs` (no-op 化)

両ファイルとも `Awake()` の `ResetEditor()` は残す (EditData 初期化のため)。それ以外のメソッドは no-op 化する。`MusicSelectorPresenter` のスキャンタイマー (300ms 間隔) も停止。

理由: `SettingsWindowPresenter` 等が `EditData.MaxBlock` 等の ReactiveProperty にバインドしているため、`Awake` の `ResetEditor` で ReactiveProperty が初期化される必要がある。

### 3. `EditDataSerializer.cs` (歌詞 + null-guard)

- `Audio.Source.clip.frequency` NPE を null-guard (共通ヘルパー `EditData.AudioFrequency()` を使用)
- `EditNotesPresenter.AudioFrequency()` (現状 private static) を `EditData.AudioFrequency()` (public static) に昇格して共有化
- `lyrics` フィールドの Serialize/Deserialize
- `EditData.Lyrics.*` への双方向変換

### 4. `SavePresenter.cs` (保存先変更)

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
            return;
        }
        var path = Path.Combine(ResourcesDir, songName + ".json");
        if (!Directory.Exists(ResourcesDir)) Directory.CreateDirectory(ResourcesDir);
        var json = EditDataSerializer.Serialize();
        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
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

`mustBeSaved` のロジックは `EditData.IsDirty` を直接参照するよう書き換え (`Audio.OnLoad.DelayFrame(1)` の `SkipUntil` 削除)。

### 5. `LyricsTabPresenter.cs` + NoteEditor シーン UI

```
NoteEditor/Canvas/
  ├─ NoteCanvasPanel      (既存)
  ├─ LyricsPanel          (新)
  │    ├─ Toolbar
  │    │    ├─ [+ 歌詞追加] ボタン
  │    │    ├─ [タブ切替] NoteCanvas ⇔ Lyrics
  │    │    └─ [保存]
  │    ├─ TimelineStrip        ← クリックで時刻指定
  │    └─ LyricsListScrollView
  │         ├─ Item prefab: Time | Furigana | Mondai | Romaji | 削除
```

- Tab 切替は `GameObject.SetActive` でシンプル化
- タイムライン座標は BPM/LPB/offset から秒数換算 (`Audio.Source.clip.frequency` の代わりに 44100 固定)
- クリック → 新規行追加 (デフォルトはクリック位置の時刻 + 2 秒間)
- 各行は `InputField` で編集、Enter / フォーカス解除で `EditData.Lyrics.*` に書き込み

### 6. `MigrateLegacyTextJson.cs` (Editor ツール)

```csharp
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

            int migrated = 0, skipped = 0;
            foreach (var textPath in Directory.GetFiles(dir, "*_text.json"))
            {
                var stem = Path.GetFileNameWithoutExtension(textPath).Replace("_text", "");
                var chartPath = Path.Combine(dir, stem + ".json");
                if (!File.Exists(chartPath))
                {
                    Debug.LogWarning($"[Migrate] Skipped (no chart): {textPath}");
                    skipped++;
                    continue;
                }

                var chart = UnityEngine.JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(chartPath));
                if (chart.lyrics != null && chart.lyrics.mondai != null && chart.lyrics.mondai.Length > 0)
                {
                    Debug.Log($"[Migrate] Already has lyrics, skip: {stem}");
                    skipped++;
                    continue;
                }

                var text = UnityEngine.JsonUtility.FromJson<TextData>(File.ReadAllText(textPath));
                chart.lyrics = new MusicDTO.LyricsDTO {
                    startTime = text.StartTime ?? new float[0],
                    furigana  = text.Furigana ?? new string[0],
                    mondai    = text.Mondai ?? new string[0],
                    romaji    = text.romaji ?? new string[0],
                    endTime   = text.EndTime ?? new float[0],
                };

                File.WriteAllText(chartPath, UnityEngine.JsonUtility.ToJson(chart, prettyPrint: true));
                File.Delete(textPath);
                File.Delete(textPath + ".meta");
                migrated++;
                Debug.Log($"[Migrate] Merged + deleted: {stem}");
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Migrate] Done: migrated={migrated}, skipped={skipped}");
        }
    }
}
```

### 7. `GameSceneManager.cs` (互換読み込み)

`LoadTextData(string songName)` を merged JSON 優先 + 旧 `_text.json` フォールバックに変更。

```csharp
void LoadTextData(string songName)
{
    var mergedPath = "Assets/Game/Resources/" + songName + ".json";
    if (File.Exists(mergedPath))
    {
        var chart = UnityEngine.JsonUtility.FromJson<MusicDTO.EditData>(File.ReadAllText(mergedPath));
        if (chart.lyrics != null)
        {
            // TextData に詰めて Text へ
            Text = new TextData {
                StartTime = chart.lyrics.startTime ?? new float[0],
                Furigana  = chart.lyrics.furigana ?? new string[0],
                Mondai    = chart.lyrics.mondai ?? new string[0],
                romaji    = chart.lyrics.romaji ?? new string[0],
                EndTime   = chart.lyrics.endTime ?? new float[0],
            };
            return;
        }
    }
    // フォールバック: 旧 *_text.json
    var legacyPath = "Assets/Game/Resources/" + songName + "_text.json";
    if (File.Exists(legacyPath))
    {
        Text = UnityEngine.JsonUtility.FromJson<TextData>(File.ReadAllText(legacyPath));
        return;
    }
    Debug.LogError($"[GameSceneManager] No text data for {songName}");
}
```

## エラーハンドリング

| 失敗ケース | 挙動 |
|---|---|
| `Assets/Game/Resources/` ディレクトリが存在しない | `FlashBeatSongLoader.Start` で自動作成 + 空リスト表示 + ログ |
| Resources 直下に JSON が無い | リスト空 + 「曲がありません。YouTubeImportDialog から追加してください」テキスト表示 |
| JSON パース失敗 (壊れたファイル) | `Debug.LogError` のみ (NoteEditor asmdef は全プラットフォームのため `EditorUtility` 使用不可) |
| JSON に `notes` フィールドが無い / 空 | `EditData.Notes` が空のまま起動 → ノートゼロから作成可能 |
| 歌詞フィールドが null | `EditData.Lyrics.*.Value = 空配列` で初期化、UI は空リスト表示 |
| 保存先 (`Assets/Game/Resources/`) が書き込み不可 | `Debug.LogError` + `messageText` にエラー表示 + `IsDirty=true` 維持 |
| `E2EProbeSong.json` のようなテスト用 JSON | リストから自動除外 |
| `*_text.json` 単体で残っている (マイグレーション漏れ) | マイグレーションスクリプトを再実行可能、冪等 |

## テスト戦略

### EditMode ユニットテスト (Assets/Tests/Editor/)

1. **`FlashBeatSongLoaderTests`** (新規) — temp dir を模して
   - `*_text.json` 除外
   - `E2EProbeSong.json` 除外
   - 複数 JSON の列挙順序
   - 壊れた JSON を含むディレクトリでの挙動

2. **`EditDataSerializerLyricsTests`** (新規) — 新規
   - Serialize: `LyricsDTO` を含む完全な JSON 出力
   - Deserialize: `lyrics` フィールド欠落時 (`null`) に空配列で初期化
   - 旧 JSON (lyrics フィールド無し) の後方互換読み込み
   - 歌詞マージ → 再シリアライズでロスなし

3. **`MigrateLegacyTextJsonTests`** (新規)
   - マイグレーション対象 `_text.json` + `chart.json` の組に対して正しくマージ
   - `_text.json` が単独 (chart 無し) で残った場合はスキップ + 警告
   - 2 度実行しても冪等

### 手動検証 (Unity Editor)

- 既存の `Assets/Game/Resources/*.json` 7 ファイルがリストに出る
- 1 ファイル選択 → ノートが表示される
- ノート追加 / 削除 → `Ctrl+S` → ファイル上書き確認 (`git diff` でバイト差分チェック)
- 歌詞タブ → 1 行追加 → 保存 → JSON に `lyrics` キーが含まれることを確認
- マイグレーション実行 → `*_text.json` 削除 + chart に lyrics マージ確認
- FlashBeat 本体起動 → GameScene で歌詞表示確認

## 実装順序

### CP1: NoteEditor で Resources/*.json が一覧 + ロードできる (音なし)

- `FlashBeatSongLoader.cs` 新規
- `MusicLoader.cs` / `MusicSelectorPresenter.cs` no-op 化
- `EditDataSerializer.cs` の `Audio.Source.clip.frequency` NPE を null-guard 化
- `Audio.OnLoad` 擬似発火

✅ チェック: NoteEditor 起動 → 既存曲クリック → ノート表示 + 編集 + 上書き保存

### CP2: 歌詞を JSON に統合 + NoteEditor で歌詞編集 UI

- `EditData.cs` に `Lyrics` 名前空間追加 + `IsDirty`
- `MusicDTO.EditData` に `lyrics` フィールド + `LyricsDTO` クラス
- `EditDataSerializer.cs` で歌詞 Serialize/Deserialize
- `LyricsTabPresenter.cs` 新規 + NoteEditor シーンに LyricsPanel 追加
- `SavePresenter.cs` を `IsDirty` 駆動に書き換え

✅ チェック: 歌詞タブで 1 行追加 → 保存 → JSON に `lyrics` 配列が含まれる

### CP3: マイグレーション + FlashBeat 側互換読み込み

- `MigrateLegacyTextJson.cs` Editor ツール
- `GameSceneManager.cs` の `LoadTextData` を merged 優先 / `_text.json` フォールバック

✅ チェック: マイグレーション実行 → `_text.json` 削除 + chart に lyrics マージ確認、FlashBeat 本体起動で歌詞表示確認

各 CP で Unity Editor を起動し、Console にエラーが出ていないことを確認してから次に進む。

## 影響を受けるファイル

**新規 (4)**:
- `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs`
- `Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsTabPresenter.cs`
- `Assets/NoteEditor/Scripts/Presenter/Lyrics/LyricsListItem.cs`
- `Assets/Editor/NoteEditor.Editor/MigrateLegacyTextJson.cs`

**変更 (7)**:
- `Assets/NoteEditor/Scripts/Presenter/MusicSelector/MusicLoader.cs`
- `Assets/NoteEditor/Scripts/Presenter/MusicSelector/MusicSelectorPresenter.cs`
- `Assets/NoteEditor/Scripts/Model/EditData.cs`
- `Assets/NoteEditor/Scripts/DTO/MusicDTO.cs`
- `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs`
- `Assets/NoteEditor/Scripts/Presenter/Save/SavePresenter.cs`
- `Assets/NoteEditor/Scenes/NoteEditor.unity`

**FlashBeat 本体 (1)**:
- `Assets/Game/Scripts/UI/GameSceneManager.cs`

**テスト (3 新規)**:
- `Assets/Tests/Editor/FlashBeatSongLoaderTests.cs`
- `Assets/Tests/Editor/EditDataSerializerLyricsTests.cs`
- `Assets/Tests/Editor/MigrateLegacyTextJsonTests.cs`

## リスク・懸念

- `Audio.OnLoad.OnNext` 擬似発火時に `Audio.Source.clip` が null だと `PlaybackPositionPresenter` 等が NPE → **対応済み**: `FlashBeatSongLoader.ProvideSyntheticAudioClip()` で 44100Hz 1秒無音の合成 AudioClip を必ずセット
- NoteEditor シーンへの UI 追加 (LyricsPanel) は YAML 直接編集になるため、Unity 起動後に Inspector 確認推奨
- `GameSceneManager.cs` で `MusicDTO.EditData` 型を参照するため、`MusicDTO` を runtime asmdef から参照可能か確認必要 → NoteEditor asmdef (`includePlatforms: []`) は FlashBeat 本体から参照不可。**対応**: DTO クラスを共通 asmdef (`Assets/Game/Scripts/DTO/MusicDTO.cs` など) に切り出すか、FlashBeat 用に最小 DTO クラスを別途定義する
- `LyricsTabPresenter` のタイムライン座標計算は `AudioFrequency()` ヘルパー (44100 固定) で計算。拍子・小節の可視化は NoteEditor の BPM/LPB 表示に追従
