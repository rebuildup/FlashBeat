# Evidence — Issue #224 Loading (実進捗 / jacket / Retry / loading.mp4)

- 作業ブランチ: `w/wload` (worktree `/Users/rebui/work/FlashBeat.wload`)
- 対象: Issue #224 の Loading レーン (07c)。Pause レーンには触れていない。
- 実機プレイ検証: **未検証** (Unity Editor を起動しない作業制約のため)。
  代替検証は compile-check + EditMode テスト (下記)。実機確認は統合 Supervisor が行うこと。

## 実装

### 1. 実進捗の経路

- 新設 `Assets/Game/Scripts/UI/V6/Game/Loading/GameLoadingProgress.cs`:
  譜面準備の進捗 (0..1) の静的な共有口。未報告 = 負値 = 不明。
- `Assets/Game/Scripts/Gameplay/NotesManager.cs`:
  - `OnEnable` で `GameLoadingProgress.ResetNotes()` (Retry によるシーン再読み込みのたびに不明へ戻す)。
  - `LoadRoutine` のノーツ生成バッチごとに `ReportNotes((float)done / total)` を報告
    (旧 `LoadingOverlay.SetProgress` と同じ値。v6 無効時の旧 overlay 経路は従来どおり)。変更行: 91-92, 212。
- `Assets/Game/Scripts/UI/V6/V6GameRuntime.cs` `UpdateLoading` (変更行: 226-235):
  `GameLoadingProgress.HasNotesProgress` なら `NotesProgress` を、未報告なら `null` を
  `SetLoading` に渡す (Loading に関する呼び出しのみ変更。Pause 部分は未変更)。
- `Assets/Game/Scripts/UI/V6/Game/Loading/V6LoadingView.cs`:
  - `SetLoading(songTitle, artist, jacket, float? chartProgress)` — 進捗値を受け取る。
  - `Tick`: 既知なら確定バー (幅 = `DeterminateFillWidth`)、不明のときだけ不定バーへフォールバック。
    **0 は既知**として 0 幅の確定バーを出す (不明扱いにしない)。
  - 進捗テキスト: 既知時「譜面を準備中… NN%」 (`V6LoadingViewLogic.ProgressDetail`)、
    不明時は従来の「動画と音声を読み込んでいます」。

### 2. jacket の実表示

- `V6LoadingViewLogic.JacketResourcePathOf(int songId)`: 選曲画面
  (`V6SelectDataSource.JacketResourcePathOf`) と同じ対応表の写し。`Select/**` は編集不可のため、
  `V6ResultView` と同じ「写しを置く」既存規約に従う。画像が無い曲 (22 など) は空文字。
- `V6GameRuntime.LoadJacket(song)`: 対応表のパスから `Resources.Load<Texture2D>`。
  songId ごとにキャッシュ (null もキャッシュ、毎フレームの Load を回避)。変更行: 250-274 付近。
- `V6LoadingView.ApplySong`: テクスチャが変わったときだけ `Sprite.Create` (旧実装は
  `SetLoading` 毎フレーム呼び出しで毎フレームスプライトを生成 → リーク。修正)。
  jacket が無い曲はジャケット枠に「無し」ラベルを表示 (空欄・壊れた画像にしない)。

### 3. Retry

- `V6LoadingView.Retry()` 新設 → 失敗状態の「再試行」ボタン (Error グループ内) に接続。
  実在する操作のみに割当: `GameSceneManager.RetryGame()` (`GManager.ResetSession()` +
  `Initiate.Fade("GameScene", ...)`)。sceneManager 未発見時は同一遷移を直接実行。
  `leaving` ガードで多重遷移を防止 (既存 `Back()` と同じ規約)。
- 「選曲にもどる」(`RetireGame`) は既存どおり併存。

### 4. loading.mp4 の表示

- `V6LoadingView.Build`: `VideoRenderMode.APIOnly` のまま `RenderTexture(1920, 1080, 0)` を
  作り `videoPlayer.targetTexture` に割当、同じテクスチャを `RawImage` に設定。
  旧実装は targetTexture 未設定でデコードだけされて画面に出なかった。
- `Dispose` で `Release()` + `Destroy` (VideoPlayer への参照も先に解除)。
- 素材は実在を確認: `Assets/Game/Resources/Videos/loading.mp4` 実体 8,742,742 bytes
  (LFS 実体化済み)、ffprobe: 1920×1080, 8.008s。`.gitattributes` の LFS 設定は未変更。

## 検証 (代替検証 — 実機は未検証)

- `script/compile-check.sh --project /Users/rebui/work/FlashBeat.wload --assembly flashbeat`
  → **Build succeeded** (141 sources)。
- `script/compile-check.sh --project /Users/rebui/work/FlashBeat.wload --assembly tests`
  → **Build succeeded** (38 sources)。
- EditMode テスト (統合 Supervisor が実行):
  - `Assets/Game/Tests/V6LoadingViewLogicTests.cs` 追加分:
    進捗の既知/不明判定 (`HasProgress`: null は不明、0 は既知)、確定バー幅の比例とクランプ、
    パーセント文言 (0/42/100%、丸め・クランプ)、進捗テキストに段階名+割合を含むこと、
    jacket 対応表 (1/10/26 → 実パス、22/0/27 → 空文字)。
  - `Assets/Game/Tests/V6LoadingProgressTests.cs` 新設: 初期状態は不明、0 は既知のまま、
    クランプ (-0.5/1.5)、最後の報告が正、Reset で不明へ戻る。
- 対応表の全 25 ジャケットファイルの実在をディスクで確認 (欠落なし)。
- 既存テストの期待値は変更していない (既存 `IndeterminateX` 系テストは無修正で成立)。

## 残存リスク / 既知の限界

1. **実機未検証**: 動画の実際の描画、確定バーの見た目、Retry ボタンの操作感は Editor で未確認。
   統合時に Test Runner + 実機 (FHD/2K/Mac) での目視確認が必要。
2. **RawImage はアスペクト比を保持しない** (uGUI RawImage に preserveAspect は存在しないため)。
   16:9 以外の画面では動画が伸びる。背景動画 (減光 + HUD の下) なので許容した。
   厳密に保つなら AspectRatioFitter 付きの別レイヤー構成が必要。
3. **数値進捗は譜面準備のみ**: メディア (動画/音源) の準備は `MusicManager` が段階名しか持たず、
   同ファイルは本レーンの編集対象外のため数値化していない。表示は「譜面を準備中… NN%」と
   数値の正体を正直に示す。メディア側の進捗 (ダウンロードバイト数等) を出すには
   `MusicManager` / `YouTubeMediaCache` 側の報告口の追加が後続で必要。
4. **jacket 対応表の写し**: 正本は `V6SelectDataSource`。曲追加時は Loading
   (`V6LoadingViewLogic.JacketFiles`) と Result (`V6ResultView.JacketFiles`) の写し 2 箇所の
   更新が必要 (既存規約の踏襲。単一化するには Select 側の public 化が後続で必要)。
5. **Retry 中の二重遷移**: `leaving` ガードで Back/Retry の連打は 1 回に制限。ただし
   失敗 → Retry → 再失敗の繰り返し自体は正規の動作 (シーン再読み込みで状態は初期化される)。
