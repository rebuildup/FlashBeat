# Evidence: #225 NoteEditor 曲切替で旧曲ノーツが混入し保存される

**Issue**: #225 | **Branch**: `w/a225` | **Date**: 2026-11-08

## 実装方針

`EditDataSerializer.Deserialize` を置換トランザクションにした
(`Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs`):

1. **先行パース**: `JsonUtility.FromJson` を最初に実行。壊れた JSON は
   `ArgumentException` で失敗し、この時点では何も変更していないため
   旧曲のノーツ / BPM / 歌詞 / 曲名が完全に維持される (受入条件 5)。
2. **旧状態の破棄**: パース成功後、`EditData.Notes.Values` の全 `NoteObject` を
   `Dispose()` してから `EditData.Notes.Clear()`。
   - 所有責務の確認: `NoteObject` は MonoBehaviour ではなくプレーン C# オブジェクト
     (`IDisposable`)。GameObject / UnityEngine ネイティブリソースは保持しない。
     `Init()` が `CompositeDisposable` に `isSelected` / `LateUpdateObservable` /
     `OnClickObservable` / `noteColor_` / `noteType` (GL 線描画の購読を含む) を
     登録しており、`Dispose()` で全て解放される。既存の `EditNotesPresenter.RemoveNote`
     と `MusicLoader.ResetEditor` が同じ「Dispose → 辞書から削除」パターンなので
     破棄責務の一貫した場所に置いた。GameObject 残骸は発生し得ない。
   - `EditState.LongNoteTailPosition` を `NotePosition.None` に戻す
     (旧曲のロングノーツ編集途中状態の持ち込み防止)。
3. **新データ適用**: BPM / MaxBlock / OffsetSamples / VideoId / 歌詞 / ノーツ。
   `editData.notes` が null の場合は空リストに正規化 (旧コードは NPE で
   メタデータだけ更新された中途半端な状態を作れた)。
4. 曲名 / `IsDirty` は既存どおり `FlashBeatSongLoader.OnFileSelected` が
   Deserialize 成功後に設定する (失敗時は変更されない)。

`FlashBeatSongLoader.OnFileSelected` の成功パスに再生状態のリセットを追加
(`Audio.TimeSamples = 0`, `SmoothedTimeSamples = 0`, `IsPlaying = false`)。
undo/redo 履歴は既存の `EditCommandManager` が `Audio.OnLoad → DelayFrame(1) → Clear`
で破棄するため、追加変更は不要 (OnFileSelected はロード後に `Audio.OnLoad` を発火)。

クリアを Deserialize 側に入れた理由: `MusicLoader.LoadEditData` 等の他の
Deserialize 呼び出し経路も同時に安全になり、Deserialize の契約が
「EditData を JSON で置換する」と素直に定義できるため。

Issue 受入条件の「別の一時モデルへの完全読込 → 原子的置換」については、
先行パースで壊れ JSON を保護した上で、パース成功後の適用は
`ToNoteObject` (純粋) + `AddNote` (辞書操作のみ、既知の throw 経路なし) であり
完全な一時モデルを作る複雑さに見合わないと判断した (plan.md の Design decisions)。

## テスト

`Assets/Tests/Editor/NoteEditorSongSwitchTests.cs` (新規、EditMode / NUnit)。
`EditData` / `EditDataSerializer` の実物を使用し、ローダーの Name/IsDirty 代入を模倣。
`SingletonTestHelper` に `EditState` の注入を追加した (Deserialize が
`LongNoteTailPosition` を触るため。既存テストへの影響は追記のみで無し)。

| Test | 検証内容 |
|---|---|
| `Deserialize_AfterPreviousSong_LeavesOnlyCurrentSongNotes` | A→B で B のノーツのみ / BPM / offset / 曲名が B |
| `Serialize_AfterSongSwitch_WritesOnlyCurrentSongNotes` | A→B→保存 (一時出力先) で B の JSON に A のノーツが含まれない |
| `Deserialize_SameSongReload_DoesNotDuplicateNotes` | 同曲再読込で重複しない |
| `Deserialize_LongNotesOfPreviousSong_AreDiscarded` | 旧曲のロングノーツが破棄され `LongNoteTailPosition` が None |
| `Deserialize_InvalidJson_PreservesPreviousSongState` | 壊れ JSON で例外、旧曲のノーツ / BPM / 曲名を維持 |

保存テストは本番 `Assets/Game/Resources` を書き換えないよう、
`SavePresenter.Save` の本体 (`EditDataSerializer.Serialize()` → UTF-8 書き込み) を
`Path.GetTempPath()` 直下へ向けて再現している。

## 検証 (実行したもの)

Unity Editor は起動していない (他ワーカーが Library を所有するため)。
代わりに dotnet build でアセンブリの型チェックを実施した:

- **NoteEditor アセンブリ** (`Assets/NoteEditor/Scripts`, 67 sources):
  `script/compile-check.sh` は `--assembly` に NoteEditor の src root が無いため、
  同スクリプトと同じ手順 (canonical `NoteEditor.csproj` をテンプレートに
  Compile items をディスクから再生成、ProjectReference を
  `Library/ScriptAssemblies/UniRx.dll` 参照に置換) で `dotnet build` を実行。

  ```
  compile-check(noteeditor): 67 sources -> NoteEditor.compilecheck.csproj
  Build succeeded.
  ```

- **FlashBeat.Tests アセンブリ** (`Assets/Tests/Editor`, 61 sources —
  新規テストファイルと `SingletonTestHelper` 変更を含む):
  同様に canonical `FlashBeat.Tests.csproj` から生成して `dotnet build`。

  ```
  compile-check(edittests): 61 sources -> FlashBeat.Tests.compilecheck.csproj
  Build succeeded.
  ```

- **`script/compile-check.sh --assembly editor` / `--assembly tests`** も試したが、
  どちらも本 Issue と無関係な既存エラーで失敗する (ベースライン):
  - `editor`: `Assets/Editor/MediaCacheTools.cs(5,17): CS0234` —
    スクリプトが `editor` アセンブリに `FlashBeat.dll` 参照を足さないため
    `FlashBeat.Gameplay` が解決できない (スクリプトの既知の限定)。
  - `tests`: `Assets/Game/Tests/V6CanvasTests/V6UiProfileTests.cs` の `CS0103`
    (`V6UiProfile` 未解決) — canonical `ScriptAssemblies` の DLL が
    この worktree のソースより古いことが原因。本変更は
    `Assets/Editor` / `Assets/Game/Tests` / `Assets/Game/Scripts` に触れていないため
    両エラーとも本 Issue のスコープ外。

## 残存リスク

1. **EditMode 実行は未実施**: Unity Editor を起動していないため、
   `NoteEditorSongSwitchTests` の実際の実行は Supervisor が統合時に行う。
   テストは `NoteObject.Init()` (UniRx 購読、`ObserveEveryValueChanged`) を
   EditMode で走らせる初めてのテストであり、UniRx の `MainThreadDispatcher`
   自動生成に依存する。環境的に失敗する場合はテスト側の Singleton 準備の
   調整が必要 (プロダクトコードの修正は不要なはず)。
2. **`JsonUtility` の例外型**: 壊れ JSON で `ArgumentException` を期待するテストは
   Unity の `JsonUtility.FromJson` のドキュメント挙動に依存する。
3. **undo/redo 履歴のクリアは `Audio.OnLoad` 経由の既存機構に依存**:
   `DelayFrame(1)` の 1 フレーム窓は理論上残るが、実害は確認されていない。
4. **パース成功後の適用中例外**では旧ノーツが既に破棄されている
   (完全な原子的置換ではない)。既知の throw 経路は無し (plan.md 参照)。
