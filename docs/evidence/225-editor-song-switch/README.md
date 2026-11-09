# Issue #225 条件 7 「Unity Editor 実機での曲切替 → 保存」実測証跡

条件 6 (別曲の JSON を上書きしない / 未保存変更の警告) は PR #252 (`39ba1bd`) で main に merge 済み。
本ディレクトリは **条件 7** を **開発用の Mac 上の Unity Editor (batchmode)** で実測した証跡である。

> 展示 (Windows) PC には一切アクセスしていない。NoteEditor は Editor 専用ツール
> (`Assets/NoteEditor/Scenes/NoteEditor.unity` は build scenes list に無く `FlashBeat.exe` に
> bundle されない) なので、展示機と無関係に Mac 上の Editor で実測できる。

---

## 1. 実行環境

| 項目 | 値 |
|---|---|
| Unity | **6000.5.6f1** (`/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity`) |
| OS | macOS 27.2 (26B123), arm64 (Apple M5) |
| 作業ツリー | `/Users/rebui/work/FlashBeat.w225` (branch `w/w225-editor-evidence`, base `origin/main` = `9e3c63e`) |
| 実行モード | `-batchmode` (ネットワーク / 外部入力なし) |

## 2. 実行コマンド

```
cd /Users/rebui/work/FlashBeat.w225
/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit \
    -projectPath /Users/rebui/work/FlashBeat.w225 \
    -executeMethod NoteEditorSongSwitchProbe.Run \
    -logFile /tmp/w225probe2.log
```

終了コード: **0** (`[NoteEditorSongSwitchProbe] 4/4 cases passed` / `Exiting batchmode successfully now!`)

## 3. 実行したプローブ

`Assets/Editor/NoteEditorSongSwitchProbe.cs` (新規, `FlashBeat.Editor` asmdef 配下 = Editor 専用。
`FlashBeat.exe` には含まれない)。

- テスト用の譜面 JSON を `Assets/Game/Resources/` に一時的に 2 つ作る
  (`__probe_a.json` / `__probe_b.json`)。**既存の実曲 JSON は読み取るだけ**で書き換えていない。
- **本番経路をそのまま使う** (テスト側でロジックを作り直していない):
  - `FlashBeatSongLoader` (公開入口 `OnFileSelected` / 本体 `LoadSongFile`)
  - `SongSwitchCoordinator` (本番の `RequestSwitch` → `Complete`)
  - `SongSwitchPromptDialog` (本番の `ISongSwitchPrompt` 実装。`Choose()` は
    本クラスが「ボタン押下 / テストからの注入」用として公开している注入点)
  - `ChartFileWriter.SaveCurrentSong` (本番の保存先 = `Assets/Game/Resources/<編集中の曲名>.json`)
  - `EditDataSerializer` (本番の Deserialize / Serialize)
  - `EditNotesPresenter` + `NoteEditDirtyTracker` (本番の編集操作 → dirty 検知)
- 比較は **ファイル実測値** (byte size + SHA-256) で行う。
- プローブ用ファイルは `try/finally` で **成否にかかわらず必ず削除** する (`.meta` ごと)。

###  dirty を立てる手順 (本番経路)

1. 曲 A (`__probe_a.json`) を `FlashBeatSongLoader.LoadSongFile` で開く → `EditData.IsDirty = false`
2. `EditNotesPresenter.RequestForAddNote.OnNext(note)` でノーツ (LPB=4 / num=6 / block=0) を追加
   → 本番の `NoteEditDirtyTracker` が `EditData.IsDirty = true` を立てる
3. `EditNotesPresenter.AddNote(note)` で追加ノーツを譜面に入れる (num=6, 曲 A の既存 2 ノーツ → 計 3)

###  計測値 (SHA-256 / size)

| ファイル | size (bytes) | SHA-256 |
|---|---|---|
| `__probe_a.json` (変更前 / 元) | 266 | `bb4999244fb9f08dc4883562f3993f408220751476688670528e2b36cf07b201` |
| `__probe_a.json` (保存後) | 314 | `50fef7a507d24676a7b431ae979054de3c6b8caf7020cf101de288694d6cf814` |
| `__probe_b.json` (全ケース共通) | 220 | `5e3600479e084595395252f6cd5b02a4b8af69a7b01d8f361b80c0997c3b0c48` |

## 4. 結果

**4 / 4 ケース PASS** (機械可読な実測値: [`results.json`](results.json),
ログ抜粋: [`unity-log-excerpt.log`](unity-log-excerpt.log))

### ケース 0: `no-dirty-switch` — 未保存変更が無い状態での切替 (公開入口 `FlashBeatSongLoader.OnFileSelected`)

| 項目 | 実測値 |
|---|---|
| ダイアログ表示 | **なし** (`promptShown = false`) |
| 切替後の `EditData.Name` | `__probe_b` |
| 切替後のノーツ数 | 1 (曲 B のノーツのみ) |
| 曲 A のファイル | **変更なし** (266 bytes / `bb499924…` → 266 bytes / `bb499924…`) |
| 曲 B のファイル | **変更なし** (220 bytes / `5e360047…` → 220 bytes / `5e360047…`) |

### ケース 1: `cancel` — 「キャンセル」を選ぶ

| 項目 | 実測値 |
|---|---|
| `SongSwitchResult` | `AwaitingChoice` (ダイアログが出て選択待ちになった) |
| ダイアログ表示 | **あり**。実測メッセージ:<br>`曲「__probe_a」に未保存の変更があります。\n曲「__probe_b」に切り替えますか?\n「保存して切り替え」を選ぶと現在の曲を保存してから切り替えます。` |
| 切替後の `EditData.Name` | `__probe_a` (**切り替えられていない**) |
| 切替後のノーツ数 | 3 (曲 A の 2 + 追加 1 = **保持**) |
| `EditData.IsDirty` | **true** (未保存変更が残る) |
| 曲 A のファイル | **変更なし** (266 / `bb499924…` → 266 / `bb499924…`) |
| 曲 B のファイル | **変更なし** (220 / `5e360047…` → 220 / `5e360047…`) |

### ケース 2: `save` — 「保存して切り替え」を選ぶ (**本 Issue の核心**)

| 項目 | 実測値 |
|---|---|
| `SongSwitchResult` | `AwaitingChoice` → `Choose(SaveAndSwitch)` |
| 切替後の `EditData.Name` | `__probe_b` |
| 切替後のノーツ数 | 1 (曲 B のノーツのみ。曲 A のノーツは混入していない) |
| `EditData.IsDirty` | false (保存済み) |
| 曲 A のファイル | **変更された** (266 / `bb499924…` → **314** / `50fef7a5…`) ← 保存は曲 A にだけ起きる |
| 曲 B のファイル | **変更なし** (220 / `5e360047…` → 220 / `5e360047…`) ← **別曲の JSON は上書きされない** |

保存された `__probe_a.json` の実測内容 (改行なし, `results.json` の `aAfterContent`):

```json
{"name":"__probe_a","maxBlock":8,"BPM":120,"offset":0,"videoId":"probe-a-video",
 "notes":[{"LPB":4,"num":4,"block":0,"type":1,"notes":[]},
          {"LPB":4,"num":5,"block":0,"type":1,"notes":[]},
          {"LPB":4,"num":6,"block":0,"type":1,"notes":[]}],
 "lyrics":{"startTime":[],"furigana":[],"mondai":[],"romaji":[],"endTime":[]}}
```

- `name` = `__probe_a` (**曲 B ではない**)
- ノーツ 3 個 = 曲 A の既存 2 + 追加した num=6
- 曲 B のノーツ (`num=8`) は**含まれていない** (`savedAContainsSongBNote = false`)

### ケース 3: `discard` — 「保存せずに切り替え」を選ぶ

| 項目 | 実測値 |
|---|---|
| `SongSwitchResult` | `AwaitingChoice` → `Choose(DiscardAndSwitch)` |
| 切替後の `EditData.Name` | `__probe_b` |
| 切替後のノーツ数 | 1 (曲 B のみ) |
| 曲 A のファイル | **変更なし** (266 / `bb499924…` → 266 / `bb499924…`) |
| 曲 B のファイル | **変更なし** (220 / `5e360047…` → 220 / `5e360047…`) |

### まとめ (条件 7 の判定)

| 判定項目 | 結果 |
|---|---|
| 未保存変更があるとき警告ダイアログが出る | **PASS** (3 ケースすべて `promptShown = true`) |
| キャンセルで曲 A の内容とファイルが保持される | **PASS** |
| 保存で **曲 A のファイルだけ** が保存される | **PASS** (A: 266 → 314 bytes) |
| **別曲 (曲 B) の JSON が上書きされない** | **PASS** (B: SHA-256 / size 一致、4 ケースすべて) |
| 破棄でどのファイルも書き換わらない | **PASS** |

### 比較の感度 (false green でないことの根拠)

同じ 1 回の実行の中で、A ファイルの比較は「保存」ケースで **変化を検出**している
(266 → 314 bytes / SHA-256 変更)  반면 B ファイルの比較は 4 ケースすべてで
**バイト単位で一致**を返している。同一の比較関数が「変更あり」と「変更なし」を
正しく区別していることの対照になっている。

## 5.  後片付けの確認

- `results.json` の `cleanup` フィールド:
  - `deleted …/Assets/Game/Resources/__probe_a.json`
  - `deleted …/Assets/Game/Resources/__probe_b.json`
  - `.meta` は生成されなかった (バッチモードではアセット再インポートが走らないため)
  - `RESIDUAL FILE REMAINS` の記録は無い
- 実行後の `git status --short` (worktree `/Users/rebui/work/FlashBeat.w225`):

```
?? Assets/Editor/NoteEditorSongSwitchProbe.cs
?? Assets/Editor/NoteEditorSongSwitchProbe.cs.meta
?? docs/evidence/225-editor-song-switch/
```

  → `Assets/Game/Resources/` に未追跡ファイルは残っていない。
  既存の実曲 JSON (`4th smile.json` など) も **1 バイトも変更されていない**
  (`git status` に `Assets/Game/Resources/` の変更なし)。

## 6.  残った未検証 (この証跡でカバーできていないこと)

1. **画面上の IMGUI ダイアログの実描画とマウス操作**
   本プローブは本番の `SongSwitchPromptDialog` を実体として生成し、
   `Choose()` (このクラスが「テストからの注入」用に公開している入口) で選択した。
   ダイアログが **画面に描画されること** と、**人がボタンを押す** ことは
   実機 GUI 操作でのみ検証できるため、本次 (本プローブ) では未実施。
2. **NoteEditor シーン / 曲一覧 UI 経由での操作**
   `Assets/NoteEditor/Scenes/NoteEditor.unity` を開かず、ロジック層 (ローダー + coordinator +
   ダイアログ) だけを batchmode で動かした。したがって
   `FlashBeatSongLoader.RefreshList` の曲一覧 UI、prefabs (`fileItemPrefab`) の実表示、
   一覧クリック → `OnFileSelected` の UI 経路は未検証
   (ケース 0 で `OnFileSelected` を直接呼んで入口自体は通した)。
3. **Play Mode / ビルド済み Player での検証**
   なし。Editor のバッチモード実行のみ。
4. **dirty 検知の網羅範囲**
   単ノーツ追加 (num=6) のみ。ロングノーツ編集・歌詞編集・BPM/MaxBlock/Offset 変更
   による dirty 化は未検証。
5. **`ChartFileWriter.Save` のガード (編集中の曲名 ≠ 保存先) の直接実行**
   本プローブでは常に「編集中の曲名 = 保存先」なので、ガードは発動していない。
   ガードが発動しないまま通ることは観測のみで、**ガードが実際に発火して別曲への
   上書きを止める** ことは未検証 (EditMode テスト側の責務)。
6. **Unity の `Serialization depth limit 10 exceeded at 'Note.notes'` 警告**
   すべての譜面読み込み時に Unity `JsonUtility` が出す警告 (DTO の `notes` が
   自己参照的なリスト構造のため)。本プローブ由来の現象ではなく、実曲でも同じ警告が出る
   既知の挙動。読み込み自体は成功している (`[FlashBeatSongLoader] Loaded: …`)。
   本 Issue の対象ではないが記録しておく。
7. **実曲ファイルを使った検証**
   既存の実曲 JSON は読み取るだけで、書き換え・保存・上書き防止の対象にはしていない
   (プローブ専用 JSON でのみ実測)。

## 7.  関連

- Issue #225
- 実装: PR #252 (`39ba1bd Merge pull request #252 from rebuildup/w/w225`)
  - `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/SongSwitchCoordinator.cs`
  - `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/ISongSwitchPrompt.cs`
  - `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/SongSwitchDecision.cs`
  - `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/SongSwitchPromptDialog.cs`
  - `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs`
  - `Assets/NoteEditor/Scripts/Model/ChartFileWriter.cs`
  - `Assets/NoteEditor/Scripts/Presenter/NoteCanvas/NoteEditDirtyTracker.cs`
- EditMode テスト: `Assets/Tests/Editor/NoteEditorSongSwitchTests.cs` 等
  (本証跡は **テストではなく Editor 実機実行のログとファイル比較実測値** を根拠にしている)
- 既存証跡: `docs/evidence/225-noteeditor-song-switch.md` (統合担当が更新する。本ディレクトリは個別に作成)
