# Evidence — Issue 233: プレイフィールドが空に見える (playfield)

日付: 2026-11-09 / Branch: `w/w233-playfield` / 基点: `origin/main` = `ab8c336`
対象: 3D レーン (旧プレイフィールド) の「ノートが見えない / レーン構造が無い」問題

## 結論 (要約)

「空っぽ」に見えた原因は **描画の失敗ではなく、譜面の見える時間が短すぎた** +
**レーン構造の部品が全部無効／透明だった** の 2 つだった。加えて、座標指示 (#233 fact 2)
で指摘されたとおり、**映像プレーンが 1 フレームも描画されなかった**。3 点ともコード側で
修正し、シーンファイル (`GameScene.unity`) は **一切編集していない**。

> **原因の訂正 (PR #263 レビュー B2)**: 当初「映像プレーンはシーンで inactive のまま
> で、`MusicManager` が有効化していたのは YoutubePlayer の *親* だけ。プレーン本体
> (`VPlayer[GManager.songID]`) は一度も有効化されなかった」と記載していたが、**誤り**。
> 実際は `MusicManager.Start` が `YPlayer[GManager.songID].SetActive(true)` で
> **選択した平面をランタイムに有効化している** (`YPlayer[1]` = fileID 1583463234 =
> 平面 "1"、`VPlayer[1]` = 1583463237 = 同じ平面の VideoPlayer)。
> つまり映像が出ない **本当の原因** は、シーンの VideoPlayer の
> **描画先レンダラが設定されていなかった**こと
> (`m_TargetMaterialRenderer: {fileID: 0}`)。
> `m_RenderMode: 3` は **MaterialOverride** (UnityEngine.Video.VideoRenderMode:
> 0=API / 1=Camera / 2=RenderTexture / 3=MaterialOverride) であり、
> RenderTexture モードではないので `m_TargetTexture` は無関係である。
> 実行時の配線 (`MusicManager.WireVideoPicture`) が実質的な修正である。
> シーンがプレーンを inactive のまま置くのは「映像表示をランタイムに委ねる」運用であり、
> シーン側の不備ではない。

| # | 症状 | 原因 | 修正 | 影響するファイル |
| --- | --- | --- | --- | --- |
| A | 画面に出る譜面が 0.86 秒分しかない (次のノートが画面外) | z スケールに「秒あたりワールド z 移動量」ではなく素の `noteSpeed` (10) を渡していた。カメラ可視範囲 8.61 / 10 = 0.861 秒 | z スケールをカメラ基準 (`ZScaleFor`) に統一。速度 10 で **1.600 秒** (設計意図どおり) | `MediaTimeline.cs`, `Notes.cs`, `NotesManager.cs` |
| B | レーン構造 (線) が見えない | "Lane" は `m_IsActive: 0`、しかも不透明な 8x40 の板で動画の上半分を覆う。"LaneLight1..8" は alpha 0 のまま (キー入力時だけ光る) | 実行時にレーン境界 9 本のガイド線を生成 (1 メッシュ / 1 レンダラ) | `PlayfieldLaneGuides.cs` (新規) |
| C | 縦長画面 (2560x1664) で外側レーンのノーツが画面外に出る | GateFit = Vertical のままなので、判定線での可視半幅が 3.567 でノーツ幅 1.0 を含め外側が切れる (約 43%) | カメラが描画するアスペクト比を `max(画面, 16:9)` に固定 (実行時のみ、**シーンは編集しない**)。垂直の幾何は不変 | `MediaTimeline.cs` (`PlayfieldCameraFit`), `PlayfieldLaneGuides.cs` |
| fact 2 | 映像が 1 フレームも描画されない | シーンの VideoPlayer の **描画先レンダラが未設定** (`m_TargetMaterialRenderer: {fileID: 0}`) で、MaterialOverride モードの出力先がなかった (実行時の配線に到達しなかった) | 実行時に `targetMaterialRenderer` / `renderMode` を配線 (`WireVideoPicture`)。板は **平面が選択された時点** で隠し、**描画済みフレーム** (`VideoPicture.HasRenderedFrame` = `frame > 0 || isPlaying`) が出たら出す | `VideoPicture.cs` (新規), `MusicManager.cs` |

## 変更ファイル (origin/main..HEAD)

| ファイル | 変更 |
| --- | --- |
| `Assets/Game/Scripts/Gameplay/MediaTimeline.cs` | `VisibleDepthZ = 8.61` / `ApproachSecondsAtSpeed10 = 1.6` を追加。`ZScaleFor(noteSpeed)` と `CurrentZScale` (= `ZScaleFor(GManager.noteSpeed)`) を追加。`NotePositionZ` / `NoteTimeFromPositionZ` の第 3 引数の意味を「秒あたり z 移動量」に確定 (N7: 単位付きの名前 `zUnitsPerSecond`)。**セッションごとの z スケール** (`SessionZScale` / `BeginSession` / `ResetSession`, S7) とカメラ幾何のヘルパ (`VisibleDepth` / `HalfWidthAtHitLine` / `IsVisible`) と `PlayfieldCameraFit` (S1) を追加 |
| `Assets/Game/Scripts/Gameplay/Notes.cs` | 3D レーンの z スケールを `MediaTimeline.SessionZScale` 1 箇所から読む (生成・時刻復元・移動・HOLD の胴体長で同じ値) |
| `Assets/Game/Scripts/Gameplay/NotesManager.cs` | セッション開始時に `MediaTimeline.BeginSession(GManager.noteSpeed)`、生成位置 `positionsZ.Add(time * MediaTimeline.SessionZScale)` |
| `Assets/Game/Scripts/Gameplay/PlayfieldLaneGuides.cs` (新規) | 実行時にレーン境界 9 本のガイド線を生成 (v6 遠近法プレイフィールドが有効なときは自動的に非表示)。**カメラ fit の適用** (S1)、生成した Mesh / Material の `OnDestroy` での破棄 (S2)、null 安全なマテリアル生成 (S3)、UV の設定 (N3) |
| `Assets/Game/Scripts/Gameplay/VideoPicture.cs` (新規) | 映像プレーンの実行時有効化 + **平面が選択された時点で板を隠す** + フレームが出るまで板を隠す。`Activate` の戻り値の意味を明記し `IsPlaneActive` を追加 (N9) |
| `Assets/Game/Scripts/Gameplay/MusicManager.cs` | `PrepareLocalVideo` / `StartPlayback` で映像プレーンを有効化、**平面選択時に板を隠す**、`Update` でフレーム確認、Invidious 除去は `VideoPicture` に一本化 |
| `Assets/Game/Scripts/UI/V5/GraphicVisibility.cs` | `IsDrawn` を alpha 考慮にする (S4): 祖先の alpha を掛け算で合成、祖先の Canvas が無効なら描かれていない、`graphic.color.a` も見る |
| `Assets/Game/Scripts/Common/AutoTestV6.cs` | 撮影タイミングを「実際にカメラに写っているノートが 3 枚以上ある区間」に変更 (疎な区間で撮ると何も写らない問題の回避)。**譜面依存の断言を soft 化**、待ちに上限、**撮影前にシーンが GameScene のままかを確認** (S5)。旧ロード表示の判定を `GraphicVisibility.IsDrawn` に統一 (N5/N6) |
| `Assets/Tests/Editor/PlaybackClockTests.cs` | z スケールの契約を固定 (値 + 3 箇所が同じ尺度を使うことのソース固定)。**どんなスケールでも時刻復元が厳密**であること、**セッションのスケールが曲中の速度変更で変わらない**ことを固定 (S7) |
| `Assets/Tests/Editor/PlayfieldGeometryTests.cs` (新規) | カメラ幾何を **シーンファイルから** 再導出 (8.6143) して 8.61 と突き合わせる。**1920x1080 と 2560x1664 の両方**で 8 レーン + ノーツ幅 1.0 が収まることを固定し、**判定線 (z = 0) と 1.6 秒のアプローチ時間の不変条件**がどちらのアスペクト比でも変わらないことを固定 (S1)。fit を入れない場合の外側切れを負の対照として固定 |
| `Assets/Tests/Editor/PlayfieldLaneGuidesTests.cs` (新規) | 生成した Mesh / Material が `OnDestroy` で破棄されること (S2)、マテリアル生成が例外を投げないこと (S3)、生成メッシュが 9 本の境界を覆うこと |
| `Assets/Tests/Editor/GraphicVisibilityTests.cs` | `IsDrawn` の alpha 合成・無効な Canvas・透明な色を追加 (S4) |
| `Assets/Tests/Editor/VideoPictureTests.cs` (新規) | 映像プレーンの有効化と板の出し分けを固定 (prepare 前から板が隠れていること / Invidious 除去は挙動で検証) |

編集していないもの: `Assets/Game/Scenes/GameScene.unity` ((cc が所有)) / `LaneFlash` /
`AutoTestKeyConfig.cs` (他ワーカーのハーネス) / v6 Option・Result・Title / Web Studio /
旧 UI 排他 / LoadingOverlay。

## A: 可視時間の是正 (0.861 秒 → 1.600 秒)

### 症状

判定線 (z = 0) に到達するノーツの高さが、毎フレーム `noteSpeed` (既定 10) ずつ
上がる implementation だったため、カメラ可視範囲 8.61 のうち **0.861 秒分** しか
譜面が見えなかった。譜面の疎な区間では次のノートが画面外なので、プレイフィールドが
「空っぽ」に見えた。

### 修正

z スケールを「秒あたりワールド z 移動量」として定義し、カメラで固定する。

```
ZScaleFor(noteSpeed) = noteSpeed * (VisibleDepthZ / (ApproachSecondsAtSpeed10 * 10))
VisibleDepthZ        = 8.61      (カメラの上端がノーツ面 y=0.55 に当たる z)
ApproachSecondsAtSpeed10 = 1.6   (設計意図: 速度 10 のとき 1.6 秒)
```

速度 10 で `ZScaleFor(10) = 8.61 / 1.6 = 5.38125` (ワールド z / 秒)、
可視時間 = `8.61 / 5.38125 = 1.600 秒`。

**不変条件は保っている**: `z = (noteTime - mediaTime) * zScale` は原点を通る
線形式で、生成・時刻復元・移動の 3 箇所が同じ `zScale` を使うので、
「ノーツは自分の判定時刻ちょうどに判定線 (z=0) に着く」「1 ノーツ = 1 判定」は
変わらない。速度オプションの意味も変わらない (速いほど反応時間は短い。旧 z 速度と
同じ向きで、1/速度に比例)。

### 「8.61」はシーンから再導出できる

`Assets/Tests/Editor/PlayfieldGeometryTests.cs` が `GameScene.unity` を**読み
取りだけ**して、Main Camera の位置 (0, 4.9, -0.8) ・回転 (pitch 54.8 度) ・
FOV 60 度から、ノーツ面 y = 0.55 に当たる視錐台の上端 z を再計算する。

- シーンから再導出した値: **8.614261967997546**
- `MediaTimeline.VisibleDepthZ`: **8.61** (許容差 0.05 以内で一致)

つまり、カメラを変えればテストが落ちる (8.61 が古くなる) ので、片方だけの
変更は通らない。

## B: レーン構造の復元 (実行時生成、シーン非編集)

### シーンにある部品

| オブジェクト | fileID | 状態 | 使えるか |
| --- | --- | --- | --- |
| `Lane` (8 x 1 x 40 の板, layer 8) | 816818327 | `m_IsActive: 0` | **不可**: 暗色の半透明マテリアル (`Lane.mat`, alpha 0.949) で動画の上半分を覆う |
| `LaneLight1..8` (幅 1 のレーン帯) | 1560073099 ほか | active だが alpha 0 | 待機中は何も描かない (`LaneFlash` がキー入力時だけ光る) |
| `JudgeLine` | 799674121 | active | 判定線 (これは見えていた) |

→ そのまま有効化する方法不能用。シーンも編集できないので、**実行時に生成**する。

### 実装

`PlayfieldLaneGuides` が `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` +
`SceneManager.sceneLoaded` で起動 (V6PlayfieldRuntime と同じ方式。シーンに
コンポーネントを足す必要がない)。

- レーン境界 9 本 (`Judge.LaneCount + 1` = 8 レーン = 9 境界) を **1 メッシュ / 1 レンダラ** で生成
- x は `Judge.LaneFirstCenterX - Judge.LaneSpacing * 0.5 + i * Judge.LaneSpacing` (= -4.0 .. +4.0) で、判定線上の 8 レーン (中心 -3.5 .. +3.5) と一致
- y = 0.53 (ノーツ面 0.55 より手前)、z = 0 .. 8.81 (カメラ可視範囲 8.61 + 0.2、視錐台に切れないように)
- マテリアルは実行時生成の無光半透明 (白 alpha 0.35、`_ZWrite 0`、`_Cull Off`、Transparent キュー)
- v6 遠近法プレイフィールド (`V6UiMode.Playfield`) が有効なときは自動で非表示 (二重表示の防止)

### 遮蔽しないことの確認 (数値)

映像プレーン (fact 2) のビュー深度は **14.15**、ノートは 7〜10、レーンガイドは
z <= 8.81 → ガイド線もノートも映像より手前なので、映像がガイドを覆わない。
(Lane マテリアルは不透明なので **使っていない**)

## C: 縦長画面での収まり (PR #263 レビュー S1)

### 症状

Main Camera は `m_GateFitMode: 2` (Vertical) で、垂直 FOV が 60 度に固定される。
そのため **水平の可視範囲は画面のアスペクト比に比例して狭くなる**。

| 画面 | アスペクト比 | 判定線 (z = 0) での可視半幅 | 外側ノーツ (x -4.0..-3.0) |
| --- | --- | --- | --- |
| 1920x1080 | 1.778 | **±4.121** | 収まる |
| 2560x1664 (展示機) | 1.538 | **±3.567** | **約 43% が画面外** |

レーン中心 (±3.5) は入るので「線は見えない」症状には出ないが、
**ノーツ幅 1.0 を含めた外側のレーンは切れている**。16:9 固定のテストでは
これが見えなかった。

### 修正 (実行時のみ、シーンは編集しない)

`PlayfieldCameraFit` が、カメラが描画するアスペクト比を
`max(画面アスペクト比, 16:9)` に固定する (GateFit は Vertical のまま)。

| 項目 | fit 前 (2560x1664) | fit 後 |
| --- | --- | --- |
| 描画アスペクト比 | 1.538 | **1.778** |
| 判定線での可視半幅 | 3.567 | **4.122** (必要値 4.1 以上) |
| 外側ノーツ (幅 1.0) | 43% 切れる | **収まる** |
| 可視深度 (縦方向) | 8.6143 | **8.6143 (不変)** |
| z スケール (速度 10) | 5.38125 | **5.38125 (不変)** |
| アプローチ時間 (速度 10) | 1.600 秒 | **1.600 秒 (不変)** |
| 判定線 (z = 0) 通過時刻 | 判定時刻ちょうど | **判定時刻ちょうど (不変)** |

### 判定タイミングがが変わらない理由

水平方向だけを変える:

- GateFit が Vertical のままなので **垂直 FOV = 60 度が変わらない** →
  視錐台の上端がノーツ面 (y = 0.55) に当たる z (= 可視深度 8.6143) は不変。
- z スケールは可視深度から決まるので 5.38125 のまま
  → 「画面に入るまでの時間」= 1.600 秒 のまま。
- ノーツの位置は `z = (noteTime - mediaTime) * zScale` で、
  `mediaTime = noteTime` のとき z = 0 (判定線) になる。
  水平の fit はこの式に現れないので、**判定線に着く時刻はビット単位で同一**。

シーン全体が同じ比率で水平に伸び縮みする (アナモーフィック) ので、
映像プレーンとノーツの相対位置 (譜面は動画に合わせて作られている) も保たれる。

### テスト

`PlayfieldGeometryTests` が 1920x1080 と 2560x1664 の **両方** で

1. 8 レーンの中心 **と** ノーツ幅 1.0 の外縁が判定線で見える
2. ガイド線の 9 本の境界が見える
3. 可視深度・1.6 秒・アプローチ時間の不変条件が成立する
4. fit を適用しないと外側が切れている (負の対照)

を固定する。

## fact 2: 映像プレーンの配線と板の出し分け

### 症状 (原因の訂正を含む)

`GameScene.unity` の映像プレーン "1".."29" は **すべて** `m_IsActive: 0`。
これは「シーンが映像表示をランタイムに委ねる」運用で、**不備ではない**。
ランタイムでは `MusicManager.Start()` が `YPlayer[GManager.songID].SetActive(true)`
で **選択した平面を有効化** している (`YPlayer[1]` = fileID 1583463234 = 平面 "1")。
したがって `VideoPicture.Activate()` 内の `SetActive(true)` は実行時には no-op である。

映像が 1 フレームも描画されなかった **本当の原因** は別のところにある。
シーンの `VideoPlayer` が

- `m_TargetMaterialRenderer: {fileID: 0}` (描画先のレンダラが無い)
- `m_RenderMode: 3` (**MaterialOverride**。`VideoRenderMode` は 0=API / 1=Camera /
  2=RenderTexture / 3=MaterialOverride。RenderTexture モードではないので
  `m_TargetTexture` は関係しない)

という状態だったため、**Prepare してもどこにも描画されなかった**。
`WireVideoPicture()` が `PrepareLocalVideo` からしか到達しなかったため、
実行時の配線が有効になる経路が限られていた。

### 修正 (コード側)

1. `MusicManager.WireVideoPicture()` が実行時に `targetMaterialRenderer` /
   `targetMaterialProperty` / `renderMode` を設定する (シーンは編集しない)。
2. `VideoPicture.Activate(video)`:
   - プレーン (`video.gameObject`) を必要なら `SetActive(true)`
   - プレーンに乗っている `InvidiousVideoPlayer` を無効化して破棄
     (実装は `VideoPicture.RemoveDeadInvidiousPlayer` に一本化。
     `MusicManager.DisableInvidiousPlayer` も同じ実装を呼ぶので二重に壊さない)
   - 板 (レンダラ) を **無効** にする
3. `VideoPicture.ShowWhenFrameReady(video, hasFrame)` は
   `VideoPicture.HasRenderedFrame(video)` (= `isPrepared` かつ
   `frame > 0 || isPlaying`) が真になった時点でレンダラを有効にする。
   `MusicManager.Update` から呼ぶ。**これだけが板を有効化する経路**である。
   `frameCount` (動画全体のフレーム数) ではないのは、prepare 済みだが
   まだ 1 フレームも描画されていない間に板が戻ってしまうため。

### 板 (白い quad) を prepare 前に隠す (レビュー B1)

`Assets/Game/Art/Materials/VideoPicture.mat` は `FlatUnlit` で `_MainTex` が空 (= 白)。
28.7 x 15.84 の板が画面を覆う。

当初の実装では板を隠すのは `VideoPicture.Activate` だけであり、これは
**`songVideo.isPrepared` が true のときだけ** 呼ばれていた
(`PrepareLocalVideo` / `StartPlayback`)。Issue #13 (Media Foundation が
`googlevideo.com` を開けない) の環境では `isPrepared` が true にならず、
**白い板が永久に出続ける**。つまり改良が効いていなかった。

修正: 板は **その曲の平面が選択された時点** (`MusicManager.Start` の
`YPlayer[GManager.songID].SetActive(true)` の直後) に `VideoPicture.HideBoard` で隠す。
prepare の完了とは独立に隠すので、prepare が失敗する環境でも白い板は出ない。
有効化は `ShowWhenFrameReady` だけが行う (EditMode テスト
`VideoPictureTests.TheBoardIsHiddenBeforeTheVideoIsPrepared` /
`TheBoardIsHiddenWhenThePlaneIsSelectedAndNotWhenTheVideoIsPrepared` で固定)。

## 検証

### EditMode テスト (Unity 6000.5.6f1, `-runTests -testPlatform editmode`, `-quit` なし)

| 対象 | total | passed | failed |
| --- | --- | --- | --- |
| **本ブランチ (PR #263 レビュー反映後)** | **1295** | **1295** | **0** |
| 本ブランチ (レビュー反映前) | 1196 | 1196 | 0 |
| 本ブランチ (main rebase 前、負の対照の基準点) | 1132 | 1132 | 0 |
| ベースライン (`origin/main` = `ab8c336`、同一 worktree で detached checkout して測定) | 1118 | 1118 | 0 |
| ベースライン (`origin/main` = `40b0dbc`、PR #256 統合後) | 1173 | 1173 | 0 |

差分 +14 = 追加したテスト (PlaybackClockTests +3 / PlayfieldGeometryTests +7 /
VideoPictureTests +4)。main rebase 後はさらに旧 UI 残存検査のテスト (GraphicVisibility 6 件 +
LegacyLyricObjectNames 3 件) を足している。

レビュー反映で足した分 (本ブランチ 1196 → 1295 の差には、`origin/main` の
マージで入ってきた他ワーカーのテストも含まれる):

- `PlayfieldGeometryTests`: 2 アスペクト比の「8 レーン + ノーツ幅が収まる」「判定線と
  1.6 秒の不変条件」「負の対照」「実行時 fit」= +7
- `PlayfieldLaneGuidesTests` (新規): Mesh / Material の破棄、null 安全な生成、メッシュの
  境界被覆 = +4
- `GraphicVisibilityTests`: alpha の積、無効な Canvas、透明な色 = +4
- `PlaybackClockTests`: どのスケールでも厳密な時刻復元、セッション固定 = +6
- 既存のテストがパラメータ化 (TestCase) されて件数が増えたもの

実行コマンド (`-quit` を付けない。付けるとテストランナーが起動する前に終了する):

```
"/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -projectPath <worktree> -runTests -testPlatform editmode \
  -testResults /tmp/testresults-263-fix4.xml
```

cc が先に示していたベースライン 1073 は `d8c1b39` のもの。`origin/main` は
`ab8c336` まで進み、他ワーカーの PR のテストが入っているので 1118 になる
(本ブランチが既存テストを壊していないことは「同一 worktree での base / head
比較」で確認している)。

結果 XML: `TestResults-wpf.xml` (本ブランチ) / `TestResults-base.xml` (ベースライン)
ログ: `unity-wpf.log` / `unity-base.log` (worktree 直下。**コミットしていない**)

### 負の対照 (negative control) — 実施済み

固定した値を 2 つ書き換えて、**対応するテストだけが落ちる**ことを確認してから戻した。

| 変えた値 | 期待した失敗 | 実測 |
| --- | --- | --- |
| `MediaTimeline.VisibleDepthZ` 8.61 → **9.5** | `PlayfieldGeometryTests.TheVisibleDepthMatchesTheCameraInTheSceneFile` が落ちる (シーンから再導出すると 8.6143 なので一致しない) | **失敗した** (total 1132 / passed 1130 / failed 2) メッセージ: `MediaTimeline.VisibleDepthZ does not match the camera in Assets/Game/Scenes/GameScene.unity (derived 8.6143). Re-derive it.` |
| `Notes.cs` の移動を `ZScale` → **`GManager.noteSpeed`** | `PlaybackClockTests.TheThreeLaneSitesUseTheSameZScale` が落ちる (3 箇所が同じ尺度を使うことのソース固定) | **失敗した** メッセージ: `The note movement must use the shared z scale.` |

変えたのはこの 2 テストだけ (他の 1130 は通った)。変更加入後は元に戻し、
1132 / 1132 に戻ることを確認済み。

### macOS 実機 (2560x1664 実ディスプレイ全画面) — 実施済み

本機の実ディスプレイはネイティブ 2560x1664 (Liquid Retina) のため、
「実ディスプレイ全画面」の条件を満たした状態で実行した
(窓リサイズでの擬似全画面は不可。`[FullscreenDiagnostics]` 行で実効値を確認する)。

| 項目 | 値 |
| --- | --- |
| ビルド | Unity 6000.5.6f1 / macOS スタンドアロン (`BuildScript.BuildStandaloneMacOS`) |
| 起動 | `-screen-width 2560 -screen-height 1664 -screen-fullscreen 1 -autotest v6` |
| 実効解像度 | `screen=2560x1664, fullScreen=True, fullScreenMode=FullScreenWindow, dpi=299` (Player.log の `[FullscreenDiagnostics]`) |
| autotest 結果 | **pass=25 / fail=0** (旧:main では `no legacy graphics are visible` が fail していた) |
| スクリーンショット | `screenshots/game-03-prepare-2560x1664.png` / `game-05-gameplay-2560x1664.png` / `game-06-dense-2560x1664.png` |
| Player.log | `screenshots/Player-2560x1664.log` |
| 判定結果 | `screenshots/autotest-v6-result.txt` |

#### 実際に見えたもの (スクリーンショットの読み取り)

| 要素 | 結果 | 根拠 |
| --- | --- | --- |
| **映像 (動画プレーン)** | **出ている** | ゲーム画面全体でアニメーションが再生されている。`fact 2` の「映像プレーンが 1 フレームも描画されない」は解消 |
| **レーン構造 (境界 9 本)** | **出ている** | 判定線から画面上端へ伸びる斜めの白線が 9 本 (8 レーンの境界) |
| **判定ライン** | **出ている** | 画面下部 (y≈1480/1664) を横切る全幅の白線 |
| **ノート** | **出ている** | 密区間の撮影 (`game-06-dense-2560x1664.png`) で **白い横棒 5 本** が並んでいる。Player.log (`screenshots/Player-2560x1664.log` 178 行目) の `[AutoTestV6] media time at 2nd capture` = **mediaTime 7.40 / visible notes 5 / z range 3.16..7.88** |
| **歌詞 (v6 HUD)** | 出ている | 中央の黒パネルに「ああ / ala」 |
| **旧 v5 歌詞の二重表示** | **出ていない** | 旧歌詞オブジェクト (QuestionText / FuriganaText / RomajiText) は非 active。autotest に検査を追加 |

#### 旧 UI 残存検査の誤検出を直した

旧 main の autotest は `no legacy graphics are visible` で **fail** になっていた。
原因は誤検出で、実行時生成の旧ロード表示 (`LoadingOverlay`) は alpha 0 で待機しているだけだが、
`Graphic.isActiveAndEnabled` が true を返すため「見えている」と判定されていた。

- 判定を `GraphicVisibility.IsDrawn` (祖先 CanvasGroup の alpha を考慮) に変更
- 旧歌詞オブジェクトが active でないこと、旧ロード表示が描かれていないことを
  **別個の検査**として追加 (EditMode テスト 9 件を追加)

#### 確認できたこと (この macOS 実機で確認できた範囲)

1. プレイフィールド (映像 / レーン / 判定ライン / ノート) が **実際に描画される**。
2. 可視時間は **1.600 秒** (速度 10) になり、譜面の疎な区間でも空画面にならない。
3. 旧 v5 の歌詞と v6 の歌詞の **二重表示はない**。

#### 実機で確認できていないこと (正直に)

1. **Windows 実機 (Windows 11 + Unity 6000.5.6f1) では未検証。**
   Media Foundation の URL 読み込み失敗 (issue #13) があるため、Windows では
   映像プレーンが有効化されてもフレームが出ない可能性がある。macOS で出した証拠は
   Windows の挙動を保証しない。
2. **判定 (キー入力 → スコア) と曲終了 → Result 到達は、この撮影では未確認。**
   autotest v6 は Space だけを送るので、譜面中のノーツは Miss になる。
   曲終了 → Result の実機検証は別 PR (#258) の担当。
3. **HUD の歌詞パネルがプレイフィールドの中央を覆う** (上部のノートが隠れる)。
   v6 HUD のレイアウト設計 (#223) の領域であり、本 PR では直していない。
4. **fact 3 (メディア時計が 0.00 → 14.80 秒に飛ぶ) は未解決。**
   原因が特定できておらず、再現手順だけ記録してある (下「fact 3」)。
5. 映像プレーンを有効化すると **背景が黒から動画に変わる**。
   「ゲーム画面として見やすいか」は他者の目視判断が必要。

## 未検証点 / 残存リスク

0. **この PR の変更 (カメラ fit / 板の隠し方 / 判定ロジック) は EditMode テストと
   macOS 実機の撮影でしか確認していない。Windows 実機での「ノーツと映像の視認」は
   依然として未検証であり、#233 の未解決問題として残る。**
   EditMode テストが緑であることは実機合格を意味しない。
1. **Windows 実機 (Windows 11 + Unity 6000.5.6f1) では未検証。**
   Media Foundation の URL 読み込み失敗 (issue #13) と関連するため、Windows では
   映像プレーンが有効化されてもフレームが出ない可能性がある。macOS で出した証拠は
   Windows の挙動を保証しない。
2. **fact 3 (メディア時計が 0.00 → 14.80 秒に飛ぶ) は未解決。**
   原因が特定できておらず、再現手順だけ記録してある (下「fact 3」)。
3. 映像プレーンを有効化すると **背景が黒から動画に変わる**。
   スクリーンショットでは「動画が出る」ことを確認したが、
   「ゲーム画面として見やすいか」は他者の目視判断が必要。
4. レーンガイドは実行時生成なので、**シーンのプレビュー (EditMode) には出ない**。
   再生中だけ有効。
5. v6 モード (`V6UiMode.Playfield` / `VideoDim`) との併用はコード上は排他になっている
   だけ。v6 側の担当者の検証は別。
6. **HUD の歌詞パネルがプレイフィールドの中央を覆う。** 密区間の撮影では
   中央付近のノートが歌詞パネルの下に隠れている。v6 HUD のレイアウト設計 (#223) の
   領域であり、本 PR では直していない (フォローアップが必要)。
7. **キー入力 → 判定 → スコア → 曲終了 → Result** はこの撮影では未確認。
   autotest v6 は Space だけを送るため、譜面中のノーツは Miss になる。
   曲終了 → Result の実機検証は PR #258 の担当。

## fact 3: メディア時計の飛ばし (未解決)

### 記録された事象

座標指示で共有された run4 のログ: 再生開始直後にメディア時計が **0.00 秒 → 14.80 秒**
へ飛んだ結果、以降 19 連続 Miss になった。

### 調べた範囲 (再現せず)

- `MediaTimeline.FallbackMediaTime` / `JudgeStartTime` / `CurrentMediaTime` の
  相互関係をソースで確認。フォールバック時計は `max(0, wall - start)` で単調。
- `StartPlayback` は `mediaTime = CurrentMediaTime()` を採り、
  `GManager.StartTime = JudgeStartTime(Time.time, mediaTime)`。
  映像が master なので `songVideo.time` が基準。
- `DetectMediaEnd()` / `mediaEnded` 分岐は時計を壊さない (終端後も `step_time` は進む)。
- 既存の Win64 証跡ログでは `PLAYBACK_START ... mediaTime=0.000` で、以降も単調増加。
  該当する run4 のログはリポジトリに無い。

### 考えられる原因 (未検証)

1. シーンの VideoClip (`m_DataSource: VideoClip` + `m_VideoClip` に各曲の mp4) を
   使った経路で、`Prepare()` 前に `time` 已经有了値だった。
2. `VideoPlayer` が URL 準備後に以前の位置 (シーク位置) から再開した。
3. メディア終端分岐と壁時計フォールバックの組み合わせ。

### 再現手順 (cc / 次の担当向け)

1. 対象曲目でゲーム画面へ入る
2. Space で再生開始 (`MusicManager.StartPlayback` が走る)
3. `Debug.Log` の `[MusicManager] PLAYBACK_START ... mediaTime=...` を読む
4. `mediaTime` が 0.00 ではなく数秒以上なら再現
5. `songVideo.time` / `songVideo.frameCount` / `GManager.step_time` を同時刻で記録

### 採らない対応 (理由)

「開始時に 0.5 秒以上進んだら 0 へ戻す」ガードは **仕様を足す** ことに当たり、
(cc の prohibition) 既存の挙動を変えるので未実装。診断ログの追加だけなら
安全だが、既存ログの形式が変わるため本次は未実施。

<!-- 実施結果 (テスト・スクリーンショット) は下方のセクションに追記する -->
