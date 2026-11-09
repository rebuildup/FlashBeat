# Evidence — Issue 233: プレイフィールドが空に見える (playfield)

日付: 2026-11-09 / Branch: `w/w233-playfield` / 基点: `origin/main` = `ab8c336`
対象: 3D レーン (旧プレイフィールド) の「ノートが見えない / レーン構造が無い」問題

## 結論 (要約)

「空っぽ」に見えた原因は **描画の失敗ではなく、譜面の見える時間が短すぎた** +
**レーン構造の部品が全部無効／透明だった** の 2 つだった。加えて、座標指示 (#233 fact 2)
で指摘されたとおり、**映像プレーンが一度も有効化されておらず映像本身が描画されて
いなかった**。3 点ともコード側で修正し、シーンファイル (`GameScene.unity`) は
**一切編集していない**。

| # | 症状 | 原因 | 修正 | 影響するファイル |
| --- | --- | --- | --- | --- |
| A | 画面に出る譜面が 0.86 秒分しかない (次のノートが画面外) | z スケールに「秒あたりワールド z 移動量」ではなく素の `noteSpeed` (10) を渡していた。カメラ可視範囲 8.61 / 10 = 0.861 秒 | z スケールをカメラ基準 (`ZScaleFor`) に統一。速度 10 で **1.600 秒** (設計意図どおり) | `MediaTimeline.cs`, `Notes.cs`, `NotesManager.cs` |
| B | レーン構造 (線) が見えない | "Lane" は `m_IsActive: 0`、しかも不透明な 8x40 の板で動画の上半分を覆う。"LaneLight1..8" は alpha 0 のまま (キー入力時だけ光る) | 実行時にレーン境界 9 本のガイド線を生成 (1 メッシュ / 1 レンダラ) | `PlayfieldLaneGuides.cs` (新規) |
| fact 2 | 映像が 1 フレームも描画されない | 映像プレーン "1".."29" が**すべて** `m_IsActive: 0`。`MusicManager` が有効化していたのは YoutubePlayer の親だけ | 準備できた時点でプレーンを実行時に有効化 (フレームが出るまで白板は隠す) | `VideoPicture.cs` (新規), `MusicManager.cs` |

## 変更ファイル (origin/main..HEAD)

| ファイル | 変更 |
| --- | --- |
| `Assets/Game/Scripts/Gameplay/MediaTimeline.cs` | `VisibleDepthZ = 8.61` / `ApproachSecondsAtSpeed10 = 1.6` を追加。`ZScaleFor(noteSpeed)` と `CurrentZScale` (= `ZScaleFor(GManager.noteSpeed)`) を追加。`NotePositionZ` / `NoteTimeFromPositionZ` の第 3 引数の意味を「秒あたり z 移動量」に確定 |
| `Assets/Game/Scripts/Gameplay/Notes.cs` | 3D レーンの z スケールを `MediaTimeline.CurrentZScale` 1 箇所から読む (生成・時刻復元・移動で同じ値) |
| `Assets/Game/Scripts/Gameplay/NotesManager.cs` | 生成位置 `positionsZ.Add(time * MediaTimeline.CurrentZScale)` |
| `Assets/Game/Scripts/Gameplay/PlayfieldLaneGuides.cs` (新規) | 実行時にレーン境界 9 本のガイド線を生成 (v6 遠近法プレイフィールドが有効なときは自動的に非表示) |
| `Assets/Game/Scripts/Gameplay/VideoPicture.cs` (新規) | 映像プレーンの実行時有効化 + フレームが出るまで板を隠す |
| `Assets/Game/Scripts/Gameplay/MusicManager.cs` | `PrepareLocalVideo` / `StartPlayback` で映像プレーンを有効化、`Update` でフレーム確認 |
| `Assets/Game/Scripts/Common/AutoTestV6.cs` | 撮影タイミングを「実際にカメラに写っているノートが 3 枚以上ある区間」に変更 (疎な区間で撮ると何も写らない问题的回避) |
| `Assets/Tests/Editor/PlaybackClockTests.cs` | z スケールの契約を固定 (値 + 3 箇所が同じ尺度を使うことのソース固定) |
| `Assets/Tests/Editor/PlayfieldGeometryTests.cs` (新規) | カメラ幾何を **シーンファイルから** 再導出 (8.6143) して 8.61 と突き合わせる |
| `Assets/Tests/Editor/VideoPictureTests.cs` (新規) | 映像プレーンの有効化と板の出し分けを固定 |

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

## fact 2: 映像プレーンの有効化

### 症状

`GameScene.unity` の映像プレーン "1".."29" が **すべて** `m_IsActive: 0`。
`MusicManager.Start()` が有効化していたのは `YPlayer[songID]` (YoutubePlayer の親)
だけ で、`songVideo = VPlayer[GManager.songID]` (プレーン本体) は
**一度も有効化されなかった**。さらに `WireVideoPicture()` は `PrepareLocalVideo` から
しか呼ばれず、レンダラ もマテリアル も未設定のままだった。

つまり映像は 1 フレームも描画されず、画面には背景しか出ていなかった。

### 修正 (コード側)

`VideoPicture.Activate(video)`:

1. プレーン (`video.gameObject`) を `SetActive(true)`
2. プレーンに乗っている `InvidiousVideoPlayer` を無効化して破棄
   (Invidious の公開インスタンスは減衰しており応答 0/13。映像は yt-dlp の URL を使う)
3. レンダラを **無効** にする

3 番目が必要な理由: `Assets/Game/Art/Materials/VideoPicture.mat` は
`FlatUnlit` で `_MainTex` が空 (= 白) なので、映像フレームが出る前に板を出すと
**白い板が画面を覆う**。

`VideoPicture.ShowWhenFrameReady(video, hasFrame)` は `songVideo.frameCount > 0`
になった時点でレンダラを有効にする。`MusicManager.Update` から呼ぶ。

呼び出し位置: `PrepareLocalVideo` (準備できた時) と `StartPlayback` (準備済みの経路)。
どちらも **準備できたプレーンだけ** を有効化するので、映像がprepare できなければ
板は出ない (白の板も出ない)。

## 検証

### EditMode テスト (Unity 6000.5.6f1, `-runTests -testPlatform editmode`, `-quit` なし)

| 対象 | total | passed | failed |
| --- | --- | --- | --- |
| **本ブランチ** (`w/w233-playfield`, 81eade3) | **1132** | **1132** | **0** |
| ベースライン (`origin/main` = `ab8c336`、同一 worktree で detached checkout して測定) | 1118 | 1118 | 0 |

差分 +14 = 追加したテスト (PlaybackClockTests +3 / PlayfieldGeometryTests +7 /
VideoPictureTests +4)。

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

### macOS ビルドと 3 解像度の実画面

- 1920x1080 / 2560x1440 / 2560x1664 を**実ディスプレイ全画面**で実行 (窓リサイズでの
  擬似全画面は不可)。スクリーンショットは `docs/evidence/233-playfield/` に置く。

## 未検証点 / 残存リスク

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
