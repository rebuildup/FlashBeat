# NoteEditor YouTube 取り込み 実装設計 (サブプロジェクト 2 / 3)

> **スコープ**: NoteEditor で編集した譜面に YouTube VideoId を埋め込み、MakeFileScene で YouTube 配線 (GameScene) と GManager 並列配列を全自動化する。
>
> **サブプロジェクト構成** (ユーザー選択):
> 1. 譜面の FlashBeat 形式エクスポート (NoteEditor → `<song>.json`) — **既存タスクで実装済み** (`EditDataSerializer` が既に FlashBeat 互換 JSON を出力)
> 2. **YouTube 取り込み (本ドキュメント)** ← 今ここ
> 3. 歌詞入力 UI (`<song>_text.json` を NoteEditor 内で編集する UI)

**Goal**: NoteEditor から YouTube を取り込み、MakeFileScene を 1 回実行するだけで GameScene と GManager に新曲を登録できるようにする。

**Architecture**: NoteEditor 側で VideoId を `MusicDTO.EditData` に保持して JSON に埋め込み、MakeFileScene 側で `PrefabUtility.InstantiatePrefab` (テンプレ YoutubePlayer の複製) とテキスト置換 (GManager.cs) で GameScene / GManager を更新する。YouTube 動画は `InvidiousVideoPlayer` (UPM `com.ibicha.youtube-player`) で引き続きストリーミング再生。

**Tech Stack**: Unity 6000.5.6f1 / UniRx (ReactiveProperty) / `UnityEditor.SceneManagement` / `UnityEditor.PrefabUtility` / `System.Text.RegularExpressions`.

---

## 1. 背景 / 動機

### 1-1. 現状の課題 (`MakeFileSceneManager.cs` のコメント参照)

新曲追加は手動 5 ステップ:

```
① Assets/Game/Resources/<song>.json 配置
② MakeFileScene 実行 → <song>_text.json 作成 (歌詞を手で埋める)
③ GManager.cs の SongName / Musician / SongURL / SBPM / Slevel / Shit / SongLong / Hiscore を末尾に Append
④ GameScene 内の YoutubePlayer を複製、名前を SongID 数値に変更
⑤ 複製した InvidiousVideoPlayer.VideoId に 11 文字 VideoId を設定
```

ステップ ④⑤ が手動で、`MusicManager.YPlayer[]` への配線も別途手動。**VideoId が譜面 JSON と一緒に管理されていない** ため、譜面編集時に VideoId が取り残される事故が起きやすい。

### 1-2. 解決方針

- 譜面 JSON に `videoId` フィールドを追加し、NoteEditor と FlashBeat の両方で読み書きできるようにする
- MakeFileScene で YouTube 配線と GManager 更新を全自動化する
- 既存譜面 JSON (videoId 無し) はそのまま読み込める (後方互換)

---

## 2. データモデル拡張

### 2-1. `MusicDTO.EditData` に `videoId` 追加

**ファイル**: `Assets/NoteEditor/Scripts/DTO/MusicDTO.cs`

```csharp
[System.Serializable]
public class EditData
{
    public string name;
    public int maxBlock;
    public int BPM;
    public int offset;
    public string videoId;          // ← 追加 (optional, default "")
    public List<Note> notes;
}
```

- `JsonUtility.FromJson` は未知 / 欠落フィールドを無視するため、**既存 JSON はそのまま読める**
- `JsonUtility.ToJson` は空文字も書き出すため、シリアライズ往復で VideoId が永続化される

### 2-2. `EditData.VideoId` ReactiveProperty

**ファイル**: `Assets/NoteEditor/Scripts/Model/EditData.cs`

```csharp
ReactiveProperty<string> videoId_ = new ReactiveProperty<string>("");
public static ReactiveProperty<string> VideoId { get { return Instance.videoId_; } }
```

### 2-3. `EditDataSerializer` 更新

**ファイル**: `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs`

```csharp
// Serialize() 末尾に追加:
dto.videoId = EditData.VideoId.Value;

// Deserialize() 末尾に追加 (videoId 欠落 JSON 対応):
EditData.VideoId.Value = string.IsNullOrEmpty(editData.videoId) ? "" : editData.videoId;
```

### 2-4. `GManager.SongData` には変更なし

`GManager.cs` の `SongData` には既に `videoId` フィールドが存在する (確認済み)。`SongURL[]` 配列の値は **生 VideoId (11 文字)** が格納されている (例: `"VIjqWffacio"`) ので、譜面 JSON の `videoId` と直接対応する。**SongURL 配列への VideoId 追加は「並列配列更新」の一部として行う**。

---

## 3. NoteEditor UI: YouTube 取り込みダイアログ

### 3-1. ツールバーボタン + メニュー項目

- **配置**: `Assets/NoteEditor/Scripts/Presenter/YouTube/` ディレクトリを新設し、配下に `YouTubeImportDialog.cs` を配置 (NoteEditor asmdef = Editor-only)
- **メニュー項目**: `[MenuItem("Tools/NoteEditor/Import YouTube URL...")]` でダイアログを直接起動可能
- **ツールバー追加方式**: 一回限りのエディタスクリプト `Assets/Editor/NoteEditorToolbarButtonInstaller.cs` を実行すると、`NoteEditor.unity` の Toolstrip GameObject に新 Button GameObject が追加され、クリックで `YouTubeImportDialog` が開く。installer は冪等 (既にボタンがあれば何もしない)。このスクリプトは手動 / メニュー `Tools/NoteEditor/Install YouTube Import Button` で 1 回だけ実行

### 3-2. `YouTubeImportDialog` (EditorWindow)

- 単一テキストフィールド: "YouTube URL または VideoId"
- OK / Cancel ボタン
- OK 押下時:
  1. 入力値を `YouTubeVideoIdParser.TryParse(input, out string videoId)` に渡す
  2. パース失敗時は `EditorUtility.DisplayDialog` でエラー表示 + ダイアログ継続 (OK 無効化でも可)
  3. パース成功時は `EditData.VideoId.Value = videoId;` で即反映
- ダイアログは Modal (`ShowModalUtility()`)

### 3-3. `YouTubeVideoIdParser` (共有ロジック)

**ファイル**: `Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeVideoIdParser.cs` (NoteEditor 側) と `Assets/Editor/YouTubeVideoIdParser.cs` (Editor 側) の **2 ファイル用意はせず**、NoteEditor 側 1 ファイルに集約。MakeFileScene 側からは `internal` 可視化 + `InternalsVisibleTo` で参照。

受理フォーマット:
- `https://www.youtube.com/watch?v=<11chars>`
- `https://youtu.be/<11chars>`
- 生 VideoId (`[A-Za-z0-9_-]{11}`)

正規表現:
```csharp
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
```

---

## 4. MakeFileScene 自動化

### 4-1. 全体フロー

現在の `MakeFileSceneManager` は歌詞を直書きするスタブ。これを **汎用の新曲登録オーケストレータ** に置き換える:

```
[MakeFileSceneManager.Start()]
  ↓
1. <song>.json をダイアログで指定 (または直前の Inspector 値)
  ↓
2. JSON 読み込み → EditData (BPM / maxBlock / videoId)
  ↓
3. 「楽曲情報入力」モーダル表示
   - タイトル (必須, デフォルト = <song>)
   - アーティスト (任意, デフォルト "")
   - BPM (必須, デフォルト = JSON.BPM)
   - レベル (任意, デフォルト 1)
   - ノーツ数 (任意, デフォルト 0)
   - 尺 (任意, デフォルト 0 秒)
  ↓
4. <song>_text.json を空テンプレートで書き出す (既存挙動)
  ↓
5. videoId が JSON にあれば → YouTubeSceneWiring.WireYouTubeToGameScene()
  ↓
6. GManagerParallelArrayUpdater.AppendSongEntry(meta)
  ↓
7. AssetDatabase.Refresh() + Debug.Log で完了通知
```

### 4-2. 新規ファイル

| ファイル | asmdef | 役割 |
|---|---|---|
| `Assets/Editor/YouTubeSceneWiring.cs` | `FlashBeat.Editor` | PrefabUtility で YoutubePlayer 複製 + VideoId 設定 + MusicManager.YPlayer 配線 |
| `Assets/Editor/GManagerParallelArrayUpdater.cs` | `FlashBeat.Editor` | GManager.cs のテキスト編集で並列配列にエントリ追加 |
| `Assets/Editor/SongMetaDialog.cs` | `FlashBeat.Editor` | メタ情報入力 EditorWindow |
| `Assets/Editor/NoteEditorToolbarButtonInstaller.cs` | `FlashBeat.Editor` | NoteEditor.unity Toolstrip に YouTube 取り込みボタン追加 (一回実行) |
| `Assets/Game/Scripts/UI/MakeFileSceneManager.cs` (既存書換) | `FlashBeat` (#if UNITY_EDITOR ブロック) | 上記 2 つを順番に呼ぶオーケストレータ |
| `Assets/Tests/Editor/YouTubeImportTests.cs` | `FlashBeat.Tests` | EditMode テスト (VideoIdParser / Serializer / Updater) |

### 4-3. `YouTubeSceneWiring.WireYouTubeToGameScene(string songName, string videoId)`

**署名**:
```csharp
public static bool WireYouTubeToGameScene(string songName, string videoId);
```

**処理ステップ**:

1. `EditorSceneManager.OpenScene("Assets/Game/Scenes/GameScene.unity", OpenSceneMode.Single)` で GameScene をロード
2. 既存の `MusicManager` コンポーネントを `Object.FindObjectsOfType<MusicManager>()` で取得
3. `MusicManager.YPlayer` 配列の `[0]` をテンプレ `YoutubePlayer` GameObject として取得
   - テンプレが null の場合: `Debug.LogError("MusicManager.YPlayer[0] is null. Set a template YoutubePlayer in GameScene.")` + return false
4. 同名 `YoutubePlayer_<songName>` が既にシーンに存在するか確認 (冪等性)
   - 存在すればスキップ + 警告ログ + return true
5. `PrefabUtility.InstantiatePrefab(template)` で複製 (Prefab リンク維持)
6. 複製 GameObject の `name = "YoutubePlayer_<songName>"`
7. `InvidiousVideoPlayer` コンポーネントを取得 → `SerializedObject` で `VideoId` フィールドを設定
8. `MusicManager.YPlayer` 配列に append (SerializedObject の `FindProperty("YPlayer").arraySize++` + 末尾代入)
9. `MusicManager.VPlayer` 配列も同様に `null` を append (新曲は YouTube のみでローカル動画なしの場合)
10. `EditorSceneManager.MarkSceneDirty(loadedScene)` + `EditorSceneManager.SaveScene(loadedScene)`
11. return true

**`InvidiousVideoPlayer.VideoId` フィールドの確認**: Unity MCP `unity_reflect get_type` で `YoutubePlayer.Components.InvidiousVideoPlayer` の fields に `VideoId` が存在することを確認済み。

### 4-4. `GManagerParallelArrayUpdater.AppendSongEntry(SongMeta meta)`

**署名**:
```csharp
public class SongMeta
{
    public string Title;
    public string Musician;
    public string VideoId;      // → SongURL に格納
    public int Bpm;
    public int Level;
    public int TotalHits;       // → Shit に格納
    public float Duration;      // → SongLong に格納
}

public static bool AppendSongEntry(SongMeta meta);
```

**処理ステップ**:

1. `Assets/Game/Scripts/Songs/GManager.cs` をテキスト読み込み
2. バックアップ作成: `Assets/Game/Scripts/Songs/GManager.cs.bak`
3. 各配列の初期化を正規表現でマッチ:
   - `SongName = \{ "noSong", "ロストアンブレラ", ... \};`
   - `Musician = \{ "no", "稲葉曇", ... \};`
   - `SongURL = \{ "VIjqWffacio", ... \};` (1 行 / 複数行両対応)
   - `SBPM = \{ 100, 274, ... \};`
   - `Slevel = \{ 10, 8, ... \};`
   - `Shit = \{ 1000, 268, ... \};`
   - `SongLong = \{ 10000.0f, 89.0f, ... \};`
4. 末尾要素の直前に新エントリを挿入 (例: `, "新曲名"` を最後の `"}` 直前に挿入)
5. `Hiscore` の配列長を確認:
   - `new int[42];` → 新エントリ用に `+1` 拡張して `new int[43];` に書き換え
6. 書き戻す前に文字列フォーマットが崩れていないか簡易チェック (元の行数 ± 2 以内なら OK)
7. ファイル書き出し → バックアップ削除
8. 例外発生時はバックアップから復元 + return false

**注意**: `totalSong` は computed property (`Songs.Length - 1`) なので変更不要。`Hiscore` だけ手動拡張が必要。

### 4-5. MakeFileSceneManager オーケストレータ

**ファイル**: `Assets/Game/Scripts/UI/MakeFileSceneManager.cs` (書換)

新 `Start()`:
```csharp
void Start()
{
    var jsonPath = EditorUtility.OpenFilePanel("Select song JSON", "Assets/Game/Resources", "json");
    if (string.IsNullOrEmpty(jsonPath)) return;

    var json = File.ReadAllText(jsonPath);
    var chart = JsonUtility.FromJson<MusicDTO.EditData>(json);
    var songName = Path.GetFileNameWithoutExtension(jsonPath);

    // メタ入力ダイアログ (EditorUtility.DisplayDialog は単純入力不可なので
    // 別途 EditorWindow を表示する)
    var meta = SongMetaDialog.ShowAndCollect(chart, songName);
    if (meta == null) return;

    // _text.json 書き出し (歌詞は空テンプレ)
    WriteTextJsonStub(songName);

    // YouTube 配線
    if (!string.IsNullOrEmpty(chart.videoId))
    {
        if (!YouTubeSceneWiring.WireYouTubeToGameScene(songName, chart.videoId))
        {
            Debug.LogError($"[MakeFile] YouTube wiring failed for {songName}");
        }
    }

    // GManager 更新
    meta.VideoId = chart.videoId ?? "";
    if (!GManagerParallelArrayUpdater.AppendSongEntry(meta))
    {
        Debug.LogError($"[MakeFile] GManager update failed for {songName}");
    }

    AssetDatabase.Refresh();
    Debug.Log($"[MakeFile] Done: {songName}");
}
```

**`SongMetaDialog`** は EditorWindow の小ダイアログ。`Assets/Editor/SongMetaDialog.cs` に配置。

**asmdef 境界の取り扱い**: `MakeFileSceneManager.cs` は `FlashBeat` (runtime) asmdef に所属するため、`UnityEditor.*` API を直接呼ぶとビルドエラーになる。Start() 本体は `#if UNITY_EDITOR` で囲み、ランタイムビルドでは空関数として残す。`YouTubeSceneWiring` / `GManagerParallelArrayUpdater` / `SongMetaDialog` は `FlashBeat.Editor` asmdef 内に閉じる。

---

## 5. 既存挙動への影響

| 影響範囲 | 内容 |
|---|---|
| **既存譜面 JSON** | videoId 無しでも読める (JsonUtility 互換性) |
| **既存 MakeFileScene の歌詞出力** | 新コードでも `_text.json` 空テンプレ出力は維持 |
| **GManager の Songs 配列** | インデックス 0 の "noSong" プレースホルダ + 末尾 Append 規約を踏襲 |
| **GameScene** | YoutubePlayer が 1 つ増える (テンプレ + 新曲) |

---

## 6. テスト戦略

`Assets/Tests/Editor/YouTubeImportTests.cs` (新規):

| テスト名 | 検証 |
|---|---|
| `VideoIdParser_ExtractsFromFullUrl` | `https://www.youtube.com/watch?v=dQw4w9WgXcQ` → `dQw4w9WgXcQ` |
| `VideoIdParser_ExtractsFromShortUrl` | `https://youtu.be/dQw4w9WgXcQ` → `dQw4w9WgXcQ` |
| `VideoIdParser_AcceptsRawId` | `dQw4w9WgXcQ` → `dQw4w9WgXcQ` |
| `VideoIdParser_RejectsEmpty` | `""` → false |
| `VideoIdParser_RejectsInvalid` | `"not a url"` → false |
| `EditDataSerializer_RoundtripsVideoId` | Serialize → Deserialize で同一 VideoId |
| `EditDataSerializer_HandlesMissingVideoId` | videoId 欠落 JSON を読んでも例外なし / 空文字 |
| `GManagerUpdater_AppendsToAllArrays` | ダミー GManager.cs 文字列に対して 7 配列全て + Hiscore 拡張 |

Editor 専用テスト (シーン操作が必要な `YouTubeSceneWiring`) は EditMode テストではフル検証できないため、**最小実装 + 手動検証** でカバー。本ドキュメント § 8 検証手順参照。

---

## 7. 影響範囲サマリ

**変更するファイル**:
- `Assets/NoteEditor/Scripts/DTO/MusicDTO.cs` (videoId フィールド追加)
- `Assets/NoteEditor/Scripts/Model/EditData.cs` (VideoId ReactiveProperty)
- `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs` (Serialize/Deserialize 更新)
- `Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeImportDialog.cs` (新規)
- `Assets/NoteEditor/Scripts/Presenter/YouTube/YouTubeVideoIdParser.cs` (新規、`internal` 公開)
- `Assets/Editor/YouTubeSceneWiring.cs` (新規)
- `Assets/Editor/GManagerParallelArrayUpdater.cs` (新規)
- `Assets/Editor/SongMetaDialog.cs` (新規)
- `Assets/Editor/NoteEditorToolbarButtonInstaller.cs` (新規)
- `Assets/Game/Scripts/UI/MakeFileSceneManager.cs` (大幅書換)
- `Assets/Tests/Editor/YouTubeImportTests.cs` (新規)

**変更しないファイル** (スコープ外):
- `Assets/Game/Scripts/Songs/GManager.cs` (Updater がテキスト編集で触るのみ、ソース構造は変更しない)
- `Assets/Game/Scripts/Gameplay/MusicManager.cs` (YPlayer 配列は SerializedObject 経由で append のみ)
- `Assets/Game/Scenes/GameScene.unity` (Updater / Wiring 実行時にだけロード・保存)
- `Assets/Game/Scenes/MakeFileScene.unity` (Manager クラス差し替えのみ)

---

## 8. 検証手順

実装完了後、以下の手動検証を行う:

### 8-1. NoteEditor 側
1. NoteEditor シーンを開く → 譜面をロード
2. メニュー `Tools/NoteEditor/Import YouTube URL...` → `https://www.youtube.com/watch?v=dQw4w9WgXcQ` 貼付 → OK
3. `EditDataSerializer.Serialize()` で書き出された JSON に `"videoId":"dQw4w9WgXcQ"` が含まれることを確認
4. JSON を再読込 → `EditData.VideoId.Value` が一致

### 8-2. MakeFile 自動化
1. MakeFileScene を開く
2. テスト用 `<song>.json` (videoId 含む) を選択
3. メタ情報ダイアログで title / musician / bpm / level 入力 → 実行
4. 以下を確認:
   - `Assets/Game/Resources/<song>_text.json` が空テンプレで書き出されている
   - GameScene に `YoutubePlayer_<song>` GameObject が新規作成されている
   - その `InvidiousVideoPlayer.VideoId` が `chart.videoId` と一致
   - `MusicManager.YPlayer[]` に新規エントリが append されている
   - `GManager.cs` の 7 配列末尾に新エントリが追加されている
   - `GManager.Hiscore` の長さが `+1` されている
5. 再度 MakeFile を実行 → 重複作成されない (冪等性)

### 8-3. 後方互換
1. 既存の `Assets/Game/Resources/ロストアンブレラ.json` (videoId 無し) を NoteEditor でロード
2. → 例外が出ず、`EditData.VideoId.Value` が `""` になることを確認
3. JSON を再保存 → `videoId` フィールドが追加されるが空文字で問題なし

---

## 9. リスクと緩和策

| リスク | 緩和策 |
|---|---|
| `GManagerParallelArrayUpdater` の正規表現が配列フォーマット (1 行 / 複数行 / 末尾カンマ) の揺れで壊れる | 書換前後で行数を比較し、想定外の増加があればロールバック。バックアップ復元可能 |
| `PrefabUtility.InstantiatePrefab` がテンプレ非 Prefab だと失敗 | テンプレの参照元を Prefab 化済みかチェック、失敗時は `Object.Instantiate` にフォールバック |
| 既存 GameScene を上書き保存すると差分が大きくなり Git の merge conflict 源になる | `YPlayer_<song>` GameObject のみ追加する最小差分を目指 |
| NoteEditor DTO に videoId を入れたことで、NoteEditor アップストリーム (189256ef6...) との同期が面倒になる | DTO 変更を IMPORT.md に明記し、将来の rebase で `MusicDTO.cs` のみコンフリクト覚悟 |
| ダイアログ Modal が MakeFileScene (Play Mode) 中だとブロックする | MakeFileScene は EditMode で起動する設計のため問題なし。`[ExecuteInEditMode]` で運用 |

---

## 10. スコープ外 (将来サブプロジェクト)

- **歌詞入力 UI** (サブプロジェクト 3): `<song>_text.json` の StartTime / Mondai を NoteEditor 内で編集する UI
- **譜面エクスポート検証** (サブプロジェクト 1 の追加検証): `EditDataSerializer` 既存実装の単体テスト追加
- **新曲のインゲームプレビュー**: MakeFileScene を経由せずプレイテストする仕組み
