# Measurements — Issue 233 playfield

すべて **シーンファイル (`Assets/Game/Scenes/GameScene.unity`) から再導出できる値**。
同じ導出を `Assets/Tests/Editor/PlayfieldGeometryTests.cs` がテストとして固定している
(カメラが変わればテストが落ちる)。

## 1. カメラ (Main Camera)

| 項目 | 値 | 出典 |
| --- | --- | --- |
| GameObject | `Main Camera` (fileID 1188690223) | シーン |
| Transform | 1188690226 | シーン |
| position | (0, 4.9, -0.8) | シーン |
| rotation (quaternion) | (0.46019977, 0, 0, 0.8878155) | シーン |
| pitch (forward の仰角) | **54.80000789907349 度** | 導出 (`asin(-forward.y)`) |
| Camera component | 1188690225 | シーン |
| field of view (vertical) | **60 度** | シーン |
| near / far | 0.3 / 1000 | シーン |
| culling mask | 4294967295 (全部) | シーン |

forward = (0, -0.8171449778048699, 0.5764323433838943)
up = (0, 0.5764323433838943, 0.8171449778048699)
right = (1, 0, 0)

## 2. カメラ可視範囲 (ノーツ面 y = 0.55 の平面上)

| 項目 | 値 |
| --- | --- |
| 視錐台の上端がノーツ面 (y = 0.55) に当たる z | **8.614261967997546** |
| 視錐台の下端がノーツ面に当たる z | **-0.40411909108084787** |
| よって見える z の範囲 | **-0.404 .. 8.614** |
| `MediaTimeline.VisibleDepthZ` (丸め) | **8.61** (テストの許容差 0.05 以内で一致) |

上端方向 = normalize(forward * cos(30°) + up * sin(30°))
= (0, -0.4194521038434557, 0.9077774686459775)
カメラから y = 0.55 までの交点 t = 10.370671550197946 → z = 8.614261967997546。

## 3. z スケールと可視時間

| 項目 | 修正前 | 修正後 (noteSpeed = 10) |
| --- | --- | --- |
| 1 秒あたりのワールド z 移動量 | 10 (= noteSpeed そのもの) | **5.38125** (= 8.61 / 1.6) |
| カメラ可視範囲に入る時間 | 8.614261967997546 / 10 = **0.8614 秒** | 8.614261967997546 / 5.38125 = **1.600 秒** |
| 設計意図 (`ApproachSecondsAtSpeed10`) | 1.6 秒 | 1.6 秒 |

速度に比例: `ZScaleFor(s) = s * (8.61 / (1.6 * 10))`
= `s * 0.538125`

| noteSpeed | z スケール (u/s) | 可視時間 (秒) |
| --- | --- | --- |
| 5 | 2.690625 | 3.2 |
| 10 | 5.38125 | 1.6 |
| 20 | 10.7625 | 0.8 |
| 25 | 13.453125 | 0.64 |

PlayerPrefs の意味は変わらない: `OptionSettings.Load()` がタイトル画面
(`TitleSceneManager.cs:48`) で `GManager.noteSpeed` を読む。速度域 1..50 も不変。

**セッション固定 (PR #263 レビュー S7)**: 上のスケールは「プレイセッションごとに
1 回だけ」確定する (`MediaTimeline.BeginSession` = `NotesManager.LoadRoutine` で確定、
`MediaTimeline.SessionZScale` で参照)。生成 (NotesManager) / 時刻復元 (Notes.Start) /
移動 (Notes.Update) / HOLD の胴体長が同じ値を使うので、曲中に速度を変えても
ノーツが飛び、復元した時刻が判定時刻とずれることがない。曲中の速度変更は
次の曲から効く。

## 4. レーンとカメラ (16:9 = 1920x1080)

| 項目 | 値 |
| --- | --- |
| レーン数 | 8 (`Judge.LaneCount`) |
| レーン中心 x | -3.5 .. +3.5 (`Judge.LaneFirstCenterX = -3.5`, `Judge.LaneSpacing = 1`) |
| レーン境界 x (ガイド線) | -4.0 .. +4.0 (9 本) |
| ノーツ幅 (ワールド x) | **1.0** (`Note.prefab` = builtin Quad 1x1 を localScale x = 1) |
| 水平 FOV (16:9) | 45.7464222598386 度 (半角) |
| 判定線 z = 0 で見える x 範囲 | **±4.121** → 8 レーン (境界 ±4.0) がすべて収まる |
| 2560x1664 (aspect 1.538) の判定線上の x 範囲 | **±3.567** → レーン中心 (±3.5) は収まるが、**外側のレーン境界 (±4.0) とノーツ幅 1.0 を含む外側ノーツ (x -4.0..-3.0) は約 43% 切れる** |

## 4b. カメラ fit (PR #263 レビュー S1)

Main Camera は `m_GateFitMode: 2` (Vertical) なので、水平の可視範囲は
アスペクト比に比例する。実行時に `PlayfieldCameraFit` が
`camera.aspect = max(画面アスペクト比, 16/9)` に固定する
(シーンファイルは編集しない)。

| 項目 | fit 前 (2560x1664, aspect 1.538) | fit 後 (aspect 1.778) |
| --- | --- | --- |
| 描画アスペクト比 | 1.538 | **1.778** |
| 判定線での可視半幅 | 3.567 | **4.122** (必要値 4.1) |
| 外側ノーツ (幅 1.0) | 43% 切れる | **収まる** |
| 可視深度 (y = 0.55 の平面上) | 8.614261967997546 | **8.614261967997546 (不変)** |
| z スケール (noteSpeed 10) | 5.38125 | **5.38125 (不変)** |
| 可視時間 (noteSpeed 10) | 1.600 秒 | **1.600 秒 (不変)** |

水平方向だけが変わるので、判定線 (z = 0) を渡る時刻 (= 判定時刻) も不変。
導出と検証は `Assets/Tests/Editor/PlayfieldGeometryTests.cs`
(`PlayfieldCameraFit` / `MediaTimeline.HalfWidthAtHitLine` / `VisibleDepth`)。

## 5. オブジェクトのビュー深度 (重なり順)

| オブジェクト | ビュー深度 (カメラからの距離) | 備考 |
| --- | --- | --- |
| 判定線 (z = 0) | 0 | 手前 |
| ノーツ | 7 .. 10 (可視域 0..8.61 に対応) | |
| レーンガイド (z = 0..8.81) | 0 .. 10 程度 | 判定線と同じ平面から |
| 映像プレーン (世界位置 (0, -6.66, 7.36)) | **14.15** | ノーツより奥 → 隠さない |

映像プレーンの姿勢: ローカル位置 (0.565, -0.956, -4.305)、euler (54.8, 0, 180)、
scale (28.7, 15.84, 1.03)、法線 (0, 0.817, -0.577)。
親 YoutubePlayer (531929085) のローカル位置 (-0.565, -5.7036, 11.6648) を合成した
世界位置が上記。

## 6. シーンの無効な部品 (Issue #233 の実体)

| オブジェクト | fileID | 事実 |
| --- | --- | --- |
| `Lane` (8 x 1 x 40, layer 8) | 816818327 | `m_IsActive: 0`。マテリアル `Lane.mat` (alpha 0.949, 半透明) → 有効化すると動画の上半分を覆う |
| `LaneLight1..8` | 1560073099, 869781921, 1329490590, 39611652, 105888223, 175034223, 783048825, 1313426385 | active。幅 1 x 高さ 0.05 x 長さ 20 の帯。`LaneFlash` が alpha 0 のまま randomization しない → 待機中は描かれない |
| `JudgeLine` | 799674121 | active (判定線は見えていた) |
| 映像プレーン "1".."29" | 521727242 ("16") ほか | **すべて `m_IsActive: 0`**。`VPlayer[songID]` が指すのはこのプレーンの VideoPlayer (例: `VPlayer[16] = 521727245` = プレーン "16" の VideoPlayer) |

映像プレーンの VideoPlayer (プレーン "16" の 521727245) の設定:
`m_PlayOnAwake: 0`, `m_RenderMode: 3` (Material), `m_TargetMaterialRenderer: {fileID: 0}`
(未接続), `m_TargetMaterialProperty: _MainTex`, `m_DataSource: 0` (VideoClip),
`m_VideoClip` = 各曲の mp4 (`Assets/Game/Resources/Videos/UnityYoutube/`),
`m_WaitForFirstFrame: 0`, `m_AudioOutputMode: 1` (AudioSource)。

プレーンには `InvidiousVideoPlayer` (114) も乗っている (機能しない経路)。
親 `YoutubePlayer` (531929085) は Transform と AudioSource だけなので、
`MusicManager.DisableInvidiousPlayer()` が親を探すと **見つからない = 無効な処理**だった
(プレーン側のコンポーネントはそのまま残っていた)。

## 7. スクリーンショット

| 解像度 | ファイル | 見たもの |
| --- | --- | --- |
| 1920x1080 | `shot-1920x1080.png` | (実施後に記入) |
| 2560x1440 | `shot-2560x1440.png` | (実施後に記入) |
| 2560x1664 | `shot-2560x1664.png` | (実施後に記入) |
