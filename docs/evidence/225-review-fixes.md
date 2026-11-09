# Evidence: #225 独立レビュー指摘 (blocking 3 / should-fix 6) の解消

**Issue**: #225 | **PR**: #252 | **Branch**: `w/w225` | **Base (PR #252 head)**: `bdb99b2`
**レビューコメント**: https://github.com/rebuildup/FlashBeat/pull/252#issuecomment-6737430450
**Date**: 2026-11-09

---

## 1. 検証環境と測定値 (実測)

| 項目 | 実測値 |
|---|---|
| Unity | 6000.5.6f1 (`0e0577a1a2ac`), macOS arm64 (Darwin 27.2.0) |
| テストコマンド | `/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/rebui/work/FlashBeat.w225 -runTests -testPlatform editmode -testResults <path>/TestResults_final.xml -logFile <path>/unity-w225.log` |
| `-quit` | **付けていない** (`-quit` を付けるとテストランナー起動前に終了し `TestResults.xml` が書かれない) |
| ベースライン (レビュー指摘対応前) | `total=1086 passed=1086 failed=0` |
| **本対応後 (最終)** | **`total=1114 passed=1114 failed=0 skipped=0 inconclusive=0`** (差分 **+28**) |

`TestResults_final.xml` の `<test-run>` 属性から機械的に読み取った値。
自己申告の数字ではなく、生成された XML の実測値。

## 2. 対応 commit (PR #252 の head に push 済み)

| commit | 内容 | 対象 |
|---|---|---|
| `de8233c` | ノーツ編集の未保存検知をテストから観測可能にする | B-1 |
| `859cb48` | 曲切替の失敗で旧曲を壊さないようにする | B-3 / S-1 / S-2 / S-3 / S-4 / S-6 |
| `9f022c4` | 「未保存の変更」の notion を `IsDirty` 1 箇所に集約する | S-5 |
| `5cc248a` | 曲切替の回復経路を NoteEditor シーンに載せる | B-2 |

---

## 3. Blocking 3 件の対応 (コードマトリクス)

### B-1: 未保存変更の検知がテストから観測できなかった

**指摘内容**: dirty 検知が `EditNotesPresenter.Init()` の中にしか無く、EditMode テストは
`SingletonTestHelper` の `GetUninitializedObject` でインスタンスを作るため
`Awake()` / `Init()` が走らず購読が 0 件。既存 13 テストは全部 `IsDirty` を手代入していた。
レビュアー実測: 検知の 3 行を削除しても **1086/1086 が green のまま**。

**対応**:

| ファイル | 変更 | 役割 |
|---|---|---|
| `Assets/NoteEditor/Scripts/Presenter/NoteCanvas/NoteEditDirtyTracker.cs` (新規) | `MonoBehaviour` に依存しないクラス。`ReactiveProperty<bool>` と 3 本の `IObservable<Note>` を受け取って `MarkDirty()` を呼ぶ | dirty 判定を 1 箇所に集約し、EditMode から直接呼べる |
| `Assets/NoteEditor/Scripts/Presenter/NoteCanvas/EditNotesPresenter.cs:52` | `Init()` を `public` 化 + 冪等化 (`initialized_` ガード) | テストから本番と同じ購読を開始できる |
| `Assets/NoteEditor/Scripts/Presenter/NoteCanvas/EditNotesPresenter.cs:178` | `SubscribeDirtyTracking()` を `public` 化 | 購読開始を外部から観測可能にする |
| `Assets/NoteEditor/Scripts/Presenter/NoteCanvas/EditNotesPresenter.cs:39` | `Awake()` から `Init()` を呼ぶ (テストでは `Awake` が走らないため `Init()` を直接呼ぶ) | 本番経路とテスト経路を同一にする |
| `Assets/NoteEditor/Scripts/Presenter/NoteCanvas/EditNotesPresenter.cs` (Subject 4 個) | フィールド初期化をやめ遅延生成プロパティ化 | `GetUninitializedObject` ではフィールド初期化子が走らないため、`null` のままだと購読時に NPE になる |
| `Assets/NoteEditor/Scripts/Presenter/NoteCanvas/EditNotesPresenter.cs` | `Init()` 内の `RequestForAddNote.Subscribe(_ => EditData.IsDirty.Value = true);` 等の **3 行を削除** | 検知ロジックの重複をなくし、唯一の実体を `NoteEditDirtyTracker` にする |

**「`IsDirty` を直接代入してはいけない」要件の充足**:
`Assets/Tests/Editor/NoteEditorDirtyTrackingTests.cs` は **全テストで `EditData.IsDirty.Value = true` を書いていない**
(`EditData.IsDirty.Value = false` によるリセットのみ)。dirty は
「本番と同じ購読 (`EditNotesPresenter.Init` → `NoteEditDirtyTracker`) を通して
ノーツ追加 / 削除 / 状態変更を実際に行う」ことで立てる。

| テスト | 検証内容 |
|---|---|
| `AddingNote_MarksSongAsDirty` | 追加で dirty が立つ |
| `RemovingNote_MarksSongAsDirty` | 削除で dirty が立つ |
| `ChangingNoteStatus_MarksSongAsDirty` | 状態変更で dirty が立つ |
| `SavingAfterNoteEdit_ClearsDirty` | 保存で dirty が落ちる |
| `DirtyTracker_MarksDirtyOnEveryEditOperation` | `NoteEditDirtyTracker` 単体の契約 |

### B-2: 回復経路 (未保存変更の確認 → 曲切替) が製品に存在しなかった

**指摘内容**: `FlashBeatSongLoader` の GUID `bcc635c94b10495d8e662650d28e0761` は
自分の `.meta` としか一致せず、シーン / プレハブのどこにも配置されていない。
旧 `MusicSelectorPresenter.Start()` と `MusicLoader.LoadMusic()` は no-op なので、
「未保存変更があれば確認して切り替えられる」経路が製品に無い = 受入条件未達。

**対応**:

| ファイル | 変更 |
|---|---|
| `Assets/NoteEditor/Scenes/NoteEditor.unity` | 既存の `MusicSelector` GameObject (fileID `107512837`) に `FlashBeatSongLoader` (fileID `1930245711`) と `SongSwitchPromptDialog` (fileID `1930245712`) を追加。ローダーは既存の曲一覧コンテナ (`Content` の `RectTransform` fileID `381058713`) と `FileListItem.prefab` (guid `1e268423609e86e4da1648375715dd32`) へ配線 |
| `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs:29` | `RefreshList()` を EditMode でも呼べるように、破棄を `Destroy` / `DestroyImmediate` で分ける |
| `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs:114` | `ResolveResourcesDirectory()` を追加。相対パスが解決できないときはプロジェクトルートからの絶対パスを使う (ディレクトリ名は `ChartFileWriter.ResourcesDir` の 1 箇所のみ) |
| `Assets/Tests/Editor/NoteEditorSceneSongLoaderTests.cs` (新規) | NoteEditor シーンを実際に開いて検証する 5 テスト |

**シーン変更の理由 (cc が統合時に調整できるよう記録)**:

1. 回復経路は「曲一覧から曲を選ぶ UI」に居る。 NoteEditor シーンの `MusicSelector` が
   その一覧の置き場所であり、プレハブ化しても `FileListItem.prefab` への参照と
   リスト生成の入口は変わらない。よって **プレハブを新設せず、既存シーンの
   既存 GameObject にコンポーネントを足す** 方式を選んだ (新建プレハブは
   参照の付け替えが必要で、差分が大きくなる)。
2. `SongSwitchPromptDialog` は serialized field を持たない IMGUI ダイアログで、
   実行時に `AddComponent` しても動く。シーンに明示的に載せたのは
   **「到達可能であること」がシーン上で確認できる** ようにするため。
3. `NoteEditor.unity` は `EditorBuildSettings.scenes` に含まれないため
   (`NoteEditor.asmdef` は all-platform だが scene list で除外されている)、
   **展示ビルド (`FlashBeat.exe`) には影響しない**。
4. `GameScene.unity` / `TitleScene.unity` / `ResultScene.unity` / `OptionScene.unity` は
   指示により **一切編集していない** (`git diff --stat` でも確認できる)。

**到達可能であることの証拠** (テストは NoteEditor シーンを `OpenPreviewScene` で実際に開く):

| テスト | 検証内容 |
|---|---|
| `NoteEditorScene_ContainsFlashBeatSongLoader` | シーンに `FlashBeatSongLoader` が存在する |
| `NoteEditorScene_LoaderIsWiredToFileList` | `fileItemContainer` / `fileItemPrefab` が未割り当てでない |
| `NoteEditorScene_ContainsSongSwitchPromptDialog` | 確認ダイアログが同じ GameObject に載っている |
| `NoteEditorScene_RefreshList_BuildsSongListFromResources` | 本番 `Resources` の譜面 JSON と同数の項目が生成され、各項目に `Button` がある (= 押すと切替に到達する) |
| `NoteEditorScene_RecoveryPath_ShowsPromptWhenSongHasUnsavedChanges` | 本番と同じ経路で未保存変更を作った状態で `OnFileSelected` を呼ぶと **確認ダイアログが出て**、キャンセルすると曲が変わらない |

### B-3: ロード失敗時に旧曲が混在状態になり、次の保存で旧曲 JSON が空で上書きされた

**指摘内容**: `LoadSong` が `void`、例外を握り潰していた (`FlashBeatSongLoader` 側)、
`EditDataSerializer` は旧ノーツを先に `Dispose` してから失敗しうると
「`Name`=旧曲 / `Notes`=空 / `BPM`=新曲」という混在状態になり、次の保存で
旧曲 JSON が空ノーツで上書きされる。

**対応**:

| ファイル | 変更 | 効果 |
|---|---|---|
| `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs:67` | `Deserialize(json, Func<Note, NoteObject> noteObjectFactory)` を `internal` に追加 (既定 `CreateNoteObject`)。テストから準備フェーズの失敗を注入できる | 失敗経路を確実に再現できる |
| `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs:95` | `PrepareChart()` — 新曲の `NoteObject` を **一時領域** に作る。失敗したら作った分を `Dispose` し `EditState` を戻して **再送出** | 解析・準備の失敗では旧曲の状態が一切変化しない |
| `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs:164` | `ApplyChart()` — ここから先は例外を投げない処理だけで、旧状態を破棄してから原子的に差し替える | 「旧を壊してから失敗する」窓が消える |
| `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs:204` | 適用後に `EditData.IsDirty.Value = false` | 読み込み後の状態が「保存済み」になる |
| `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs:135` | `LoadSongFile(string, out string error)` が `bool` を返す。失敗は `false` + 理由 (`error`) を返す (例外は握り潰さない) | 呼び出し側が失敗を知る |
| `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/SongSwitchCoordinator.cs:67` | `RequestSwitch` の戻り値を `SongSwitchResult` (enum) にする。`LoadFailed` / `SaveFailed` / `Busy` / `AwaitingChoice` / `Switched` / `InvalidRequest` を明示 | 戻り値の意味が明確になる |
| `Assets/NoteEditor/Scripts/Model/SongName.cs` (新規) | `SongName.Normalize()` — 拡張子 `.json` だけを取り除く (ドットを含む曲名は壊さない) | `Path.GetFileNameWithoutExtension` が `曲.名.json` を `曲.名` にしてしまう問題 FIX |

**混在状態を作らないことの証拠**:

| テスト | 検証内容 |
|---|---|
| `LoadSongFile_ProductionPath_Failure_KeepsCurrentSongIntact` | 壊れ JSON のロード失敗後も `Name` / `Notes` / `BPM` が旧曲のまま (混在しない) |
| `SaveAfterFailedLoad_WritesCurrentSongUnchanged` | **ロード失敗後に保存しても旧曲 JSON は 1 ノーツのまま** (空で上書きされない) |
| `Deserialize_PreparePhaseFailure_KeepsCurrentSongIntact` | 準備フェーズ失敗 (注入) でも旧曲の状態が一切変わらない |
| `SaveAfterPreparePhaseFailure_WritesCurrentSongUnchanged` | 準備フェーズ失敗後に保存しても旧曲 JSON は壊れない |

---

## 4. Should-fix 6 項目 (7 箇条書き) の対応状況

| # | 指摘 | 状況 | 対応 / 理由 |
|---|---|---|---|
| 1 | 本番のロード経路にテストが 0 件 (M2 で実証) | **解消** | `NoteEditorSongLoadFailureTests` が実物の `FlashBeatSongLoader` MonoBehaviour を直接操作する 6 テストを追加 (`LoadSongFile_ProductionPath_LoadsSongAndResetsPlaybackState` ほか) |
| 2 | `ChartFileWriter` のガードが本番経路で発火しない | **解消** | `ChartFileWriter.Save` を `internal` 化 (ガードを公開 API から不変条件へ降格) して `InternalsVisibleTo("FlashBeat.Tests")` (新規 `Assets/NoteEditor/Scripts/AssemblyInfo.cs`) で検証可能にし、`ChartFileWriter_Save_OtherSongName_ThrowsAndDoesNotOverwriteOtherSong` / `ChartFileWriter_SaveCurrentSong_DoesNotTouchOtherSongFiles` で固定 |
| 3 | ダイアログの再入で保留中の切替が失われる | **解消** | `ISongSwitchPrompt.Show` が `bool` を返すようにし、表示中の再入は `false` (拒否) + `Debug.LogWarning`。`SongSwitchCoordinator.RequestSwitch` は `SongSwitchResult.Busy` を返す。`SongSwitchPromptDialog` に `ShowError` / `Close` / `IsVisible` / `HasPendingChoice` を追加 |
| 4 | 保存失敗が UI に反映されない | **解消** | `SongSwitchCoordinator.Complete` / `LoadOrReportError` が `prompt_.ShowError(...)` を呼ぶ。実物の `SongSwitchPromptDialog` を使う `NoteEditorSongSwitchDialogTests.ShowError_ShowsErrorMessageToUser` で固定 |
| 5 | `RequestSwitch` の戻り値の意味が不明 | **解消** | `SongSwitchResult` enum に置換 (意味ごとに 6 値)。`RequestSwitch_EmptyPath_IsRejected` / `RequestSwitch_LoadFails_ReportsErrorToUser` などで固定 |
| 6 | `IsDirty` と `mustBeSaved` の notion が食い違う | **解消** | `SavePresenter.mustBeSaved` を `EditData.IsDirty` の派生に置き換え、旧 `Observable.Merge(...)` を削除。`EditData` 自身が BPM / MaxBlock / OffsetSamples の変更で `IsDirty` を立てる (`EnsureDirtyTracking`)。「保存して終了」は `ChartFileWriter` が `IsDirty` を落とすので手動クリアを廃止 |
| 7 | `ResourcesDir` が 2 箇所で定義 | **解消** | `ChartFileWriter.ResourcesDir` 1 箇所のみ (`FlashBeatSongLoader` の重複定義を削除)。列挙・保存の双方で `FlashBeatSongLoader.ResolveResourcesDirectory()` を通す |

---

## 5. 変異 (mutation) テスト — 壊して赤くなることの実測

各行は「意図的に 1 箇所のロジックを壊す → 対象テストを実行して赤くなることを確認 → 元に戻す」。
`-testFilter` で対象 fixture のみを実行している (合計は数える)。

| # | 壊した箇所 | 実行した fixture | 実測 (total / passed / **failed**) | 落ちたテスト |
|---|---|---|---|---|
| M1 | `NoteEditDirtyTracker` の `noteAdded.Subscribe(_ => MarkDirty());` を削除 (B-1) | `NoteEditorDirtyTrackingTests` | 9 / 6 / **3** | `AddingNote_MarksSongAsDirty`, `DirtyTracker_MarksDirtyOnEveryEditOperation`, `SavingAfterNoteEdit_ClearsDirty` |
| M2 | `EditDataSerializer.PrepareChart` の `catch` で `throw` せず握り潰す (B-3) | `NoteEditorSongLoadFailureTests` | 6 / 4 / **2** | `Deserialize_PreparePhaseFailure_KeepsCurrentSongIntact`, `SaveAfterPreparePhaseFailure_WritesCurrentSongUnchanged` |
| M3 | NoteEditor シーンから `FlashBeatSongLoader` / `SongSwitchPromptDialog` のコンポーネント参照を外す (B-2) | `NoteEditorSceneSongLoaderTests` | 5 / 0 / **5** | 5 件すべて |
| M4 | `SongSwitchPromptDialog.Show` の「表示中なら拒否」を外す (S-3) | `NoteEditorSongSwitchDialogTests` | 4 / 2 / **2** | `Show_WhileVisible_IsRejectedAndFirstChoiceIsKept`, `RequestSwitch_WhileDialogVisible_ReturnsBusyAndKeepsPendingSwitch` |
| M5 | `EditData.EnsureDirtyTracking` の `BPM_.Skip(1).Subscribe(...)` を削除 (S-5) | `NoteEditorDirtyTrackingTests` | 9 / 8 / **1** | `ChangingBPM_MarksSongAsDirty` |
| M6 | `ChartFileWriter.Save` の「編集中の曲と保存先が一致しない」ガードを削除 (S-2) | `NoteEditorSongSwitchPromptTests` | 17 / 16 / **1** | `ChartFileWriter_Save_OtherSongName_ThrowsAndDoesNotOverwriteOtherSong` |
| M7 | `SongSwitchCoordinator.Complete` の保存失敗時の `prompt_.ShowError(...)` を削除 (S-4) | `NoteEditorSongSwitchPromptTests` | 17 / 16 / **1** | `RequestSwitch_SaveFails_ReportsErrorToUser` |

**全 7 変異を元に戻した後の最終実行**: `total=1114 passed=1114 failed=0 skipped=0`。

---

## 6. 残存リスク / 未検証点

1. **Play Mode での実機確認はしていない**。EditMode テストで
   NoteEditor シーンを `OpenPreviewScene` して回復経路の到達までは確認したが、
   `Start()` → `RefreshList()` の初期生成と、uGUI 実画面でのクリック操作は
   Play Mode は未実施。`NoteEditor.unity` は build scenes list に無いため展示機では
   動かない (譜面作成ツール専用)。
2. **旧ノーツの破棄責務**: `ApplyChart` は旧 `NoteObject` を `Dispose` してから
   差し替える。`NoteObject` は MonoBehaviour ではないため `Dispose` で
   購読 (GL 線描画を含む) は解放されるが、`Dispose` 漏れがあれば旧曲の上に
   描画購読が残る可能性が理論上ある。既存の
   `EditNotesPresenter.RemoveNote` / `MusicLoader.ResetEditor` と同じ
   「`Dispose` → 辞書から削除」パターンに揃えた形であり、
   EditMode では検証できていない。
3. **`PrepareChart` の一時領域**: 新曲_notes_ を作ってから旧を捨てるため、
   短時間だけ新旧 2 セットの `NoteObject` が同時に存在する。
   メモリ上の一時的な重複のみ (永続化しない)。
4. **undo / redo 履歴のクリア**は `Audio.OnLoad` → `EditCommandManager.Clear`
   の既存機構に依存している (1 フレーム窓)。変更なし。
5. **`ResolveResourcesDirectory()`** は作業ディレクトリがプロジェクトルートでない
   場合に絶対パスへフォールバックする。`ChartFileWriter.ResourcesDir` は
   単一定義のまま (二重定義なし) を保っている。
6. **`Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset`** は本作業前から
   変更済みの状態で、**本 PR では commit していない** (LFS 追跡)。
7. **レビューコメント投稿は cc (開発監督) が行う**。本作業では PR ブランチ
   (`w/w225`) への push までで、Issue へのコメントは投稿していない。

---

## 7. 変更ファイル一覧

```
Assets/NoteEditor/Scenes/NoteEditor.unity                                (+22)
Assets/NoteEditor/Scripts/AssemblyInfo.cs                                (新規, +6)
Assets/NoteEditor/Scripts/AssemblyInfo.cs.meta                           (新規)
Assets/NoteEditor/Scripts/Model/ChartFileWriter.cs                       (±12)
Assets/NoteEditor/Scripts/Model/EditData.cs                              (±29)
Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs                   (±150)
Assets/NoteEditor/Scripts/Model/SongName.cs                              (新規, +28)
Assets/NoteEditor/Scripts/Model/SongName.cs.meta                         (新規)
Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs (±62)
Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/ISongSwitchPrompt.cs   (±10)
Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/SongSwitchCoordinator.cs (±67)
Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/SongSwitchPromptDialog.cs (±63)
Assets/NoteEditor/Scripts/Presenter/NoteCanvas/EditNotesPresenter.cs     (±73)
Assets/NoteEditor/Scripts/Presenter/NoteCanvas/NoteEditDirtyTracker.cs   (新規, +39)
Assets/NoteEditor/Scripts/Presenter/NoteCanvas/NoteEditDirtyTracker.cs.meta (新規)
Assets/NoteEditor/Scripts/Presenter/Save/SavePresenter.cs                (±27)
Assets/Tests/Editor/NoteEditorDirtyTrackingTests.cs                      (新規, +199)
Assets/Tests/Editor/NoteEditorDirtyTrackingTests.cs.meta                 (新規)
Assets/Tests/Editor/NoteEditorSceneSongLoaderTests.cs                    (新規, +169)
Assets/Tests/Editor/NoteEditorSceneSongLoaderTests.cs.meta               (新規)
Assets/Tests/Editor/NoteEditorSongLoadFailureTests.cs                   (新規, +232)
Assets/Tests/Editor/NoteEditorSongLoadFailureTests.cs.meta               (新規)
Assets/Tests/Editor/NoteEditorSongSwitchDialogTests.cs                   (新規, +97)
Assets/Tests/Editor/NoteEditorSongSwitchDialogTests.cs.meta              (新規)
Assets/Tests/Editor/NoteEditorSongSwitchPromptTests.cs                   (±143)
docs/evidence/225-review-fixes.md                                       (新規)
```

合計: 26 ファイル, +1398 / -107 (docs を除く)。
