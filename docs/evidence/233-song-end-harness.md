# Evidence — Issue #233: 「1 曲完走 → Result 画面到達」の実機ハーネス

日付: 2026-11-10 / Branch: `w/w233-songend-harness` (base `origin/main` = 877bde7 を取り込み済み。
remote の履歴を消さないため **rebase ではなくマージ** で main を取り込んだ) /
対象: macOS 実機 (Unity 6000.5.6f1 の macOS player)

## 1. 何を証明するための仕組みか

Issue **#233** の受入条件の最後の項目「**結果へ進める**」は、これまで Windows 実機でしか
確認できなかった。既存の実機自動テスト (`Assets/Game/Scripts/Common/AutoTestV6.cs`,
`-autotest v6`) は SelectScene → GameScene → Space → 4 秒再生 → スクリーンショットまでで
止まり、**曲終了を通らない**。

本作業で、実機で「曲終了が検出され、ResultScene へ遷移し、Result 画面が描画される」
ところまで自動で確認するハーネスを追加した。

## 2. 採用した手段と理由

### 採用: 実カタログの最短の曲を **最後まで本当に再生する** (クロック注入も譜面の差し替えもしない)

| 案 | 採否 | 理由 |
|---|---|---|
| (a) クロック注入 | **不採用** | 「終端直前の時刻を注入する」ことはできるが、注入できた分だけ **本番とは別の条件**をテストしている。#233 が求めているのは「本物の終了判定と本物の遷移が実機で起きること」であり、注入した条件が成立することは遷移の配線 (`Initiate.Fade("ResultScene")`) だけを証明する。曲終了の判定そのものは注入の前提になってしまい、注入できる口があっても **状態機械そのものは EditMode テストと重複**し、追加の実測価値がない。 |
| (b) 数秒で終わるテスト譜面をカタログに追加 | **不採用** | `GManager` の 7 本の平行配列への追加が必要で、既存 index のずれ的风险がある。また譜面を追加すると **製品ビルドの選曲画面にもテスト曲が出る** (仕様に無い機能の追加) ことになる。 |
| (c) 実カタログの最短の曲を最後まで再生 | **採用** | 本物の曲・本物のメディア (yt-dlp ダウンロード)・本物の判定・本物の終了判定・本物の遷移をそのまま通す。製品コードに注入用の口も開けない (テストビルドと製品ビルドの差が小さい)。 |

採用した曲: **ツイッターランド** (カタログ index 4, `SongLong = 44 秒` = カタログ内で
再生可能な曲として最短)。YouTube 上の実動画は 139 秒だが、本ゲームの譜面と判定はカタログの
duration (44 秒) を基準に作られているため、**約 43 秒の再生で曲終了条件が成立する**。
(44 秒を選んだのはハーネスの実行時間を抑えるためで、終了判定そのものは
`MediaTimeline.HasSongEnded` = `mediaTime >= duration - 1s` という **製品コードの契約**である。)

この設計なら、**窓リサイズによる擬似全画面ではなく**、実ディスプレイ全画面で
「映像 → 判定 → 曲終了 → Result」を一気通貫で確認できる。

### 変更した製品コード (2 ファイル / 数十行)

| ファイル | 変更 | 理由 |
|---|---|---|
| `Assets/Game/Scripts/Gameplay/MusicManager.cs` | `SongEndState` / `ResultTriggered` / `SongEndForcedByDeadline` の public getter を追加 | ハーネスが「曲終了が検出された」ことと「**期限切れ強制完了ではない**」ことを **観測** するため。状態機械 (`SongEndMachine`) 自体は無変更。 |
| `Assets/Game/Scripts/Gameplay/Judge.cs` | `IsLaneKeyPressed` が `Input.GetKeyDown` ではなく `KeyBindings.GetKeyDownOrInjected` を見る | 検証ハーネスからレーンのキーを **注入** できるようにするため (「キーで判定される」を実証するため)。`GetKeyDownOrInjected` は `Input.GetKeyDown || 注入されたキー` なので、**製品ビルドでは注入が起きず挙動は従来と完全に同じ**。参照するキー表 (`KeyBindings.GetKeys`) も従来通り。 |

**注入用の口は製品コードに開いていない**: クロック注入も譜面の差し替えも行っていないため、
テストビルドと製品ビルドの差は上記 2 点 (観測用 getter と入力の注入受付) だけである。
判定・曲終了・遷移のロジックは一切変えていない。

### 作ったハーネス

`Assets/Game/Scripts/Common/AutoTestV6SongEnd.cs` (新規 MonoBehaviour)

- 起動: プレイヤー引数 `-autotest v6songend -autotest-out <dir>` (既存 `-autotest v6` とは別の
  スイッチ。既存 `AutoTestV6.cs` は **編集していない**)。
- 手順: SelectScene → 実カタログから「ツイッターランド」を選ぶ → Ready → GameScene →
  メディアの準備 (yt-dlp でのダウンロード) を待つ → Space で開始 → ノーツを叩きながら
  再生 → 曲終了 → ResultScene → 描画を確認 → スクリーンショット。
- アサーション (`Check()` 形式。失敗した項目は `[AutoTestV6SongEnd] FAIL <項目>` のログ行
  として残り、どの項目で落ちたかが分かる):
  1. 選曲画面が立ち上がる / v6 の SelectView がある / カタログに曲がある
  2. 目的の曲がカタログにある / 選択が反映された
  3. Ready で GameScene へ遷移する / 再生中の曲が選んだ曲
  4. メディアのパイプラインが完了する (`MediaReady`)
  5. 再生が始まる (`PlaybackStarted`)
  6. メディア時計が進む (`MediaTimeNow` が増加)
  7. **譜面の全ノーツが確定する** (`GManager.settledNoteCount` == 譜面のノーツ数)
  8. **実行時のノーツ数が譜面と一致する** (`NotesManager.noteNum` == 譜面のノーツ数)
  9. **スコアが 0 より大きい** (`GManager.score > 0`)
  10. **コンボが 0 より大きくなる** (再生中に `GManager.combo` のピーク > 0、`GManager.maxCombo` > 0)
  11. **曲終了が検出される** (`SongEndMachine.State == Completed`)
  12. **期限切れ強制完了ではない** (`MusicManager.SongEndForcedByDeadline` == false)
  13. **ResultScene へ遷移する** (`SceneManager.GetActiveScene().name == "ResultScene"`)
  14. **Result 画面が v6 の Result Canvas の中で描画されている**
      (`V6 Result Canvas` が active、その **子孫** に空でない文字があり、
      その文字に **曲名** と **最終スコア** が含まれる)
- 出力: `[AutoTestV6SongEnd] RESULT pass=N fail=M` / `<outDir>/autotest-v6songend-result.txt` /
  `<outDir>/autotest-v6songend-NN-*.png`
- 再現スクリプト: `script/mac-songend-harness.sh`

### EditMode テスト

`Assets/Tests/Editor/SongEndHarnessTests.cs` (新規, 8 件)

ハーネスが前提とするもの (＝「この曲なら *期限切れ強制完了 WITHOUT* 自然に完走できる」こと) を
データで固定する:

- ハーネスの対象曲がカタログにあり、選択可能 (unplayable でない) である
- 7 本の平行配列が lockstep のまま (この作業では配列を触っていないことの固定)
- 対象曲に譜面とノーツがある
- **対象曲に HOLD ノーツが無い** (HOLD の判定イベント数問題は下記参照)
- 対象曲が 60 秒以下 = テスト実行に耐える長さである
- **末尾ノーツの確定時刻 (判定時刻 + Miss 窓) が「曲長 − 1 秒」より前** である
  (ここが外れると曲終了が「期限切れ強制完了」になり、判定合計がノーツ数に
  合わない結果 = 成功偽装になる。EditMode でこの関係を固定する)
- その前提のもとで **本物の `SongEndMachine` が `ForcedByDeadline` なしで `Completed` になる**
  (メディア時刻を 0.05 秒刻みで進め、終了条件と pending を実データから再現)
- `MusicManager` が曲終了状態を観測できる (新規 getter が実体に入っている)。
  `SongEndForcedByDeadline` も「新しい MusicManager では false」であることを確認する

### `GManager.nextNotes` と HOLD について (Issue #182 / PR #256 との関係)

- **本ハーネスは `GManager.nextNotes` を一切参照していない。** 「曲終了が起きた」ことは
  状態機械そのもの (`MusicManager.SongEndState` = `SongEndMachine.State`) で観測する。
  (`AutoTestV6SongEnd.cs` の `lastSongEndState` の doc コメントにも明記)
- 読み取る判定値は `GManager.perfect / great / bad / miss` と `score` / `combo` だけで、
  これらは `JudgeScoreboard` の集計で **ノーツ 1 個につき 1 判定** (HOLD 中断は BAD への
  *差し替え* で二重加算されない) なので、判定合計はノーツ数に一致する。
- 一方 `GManager.nextNotes` は `Judge.message()` が呼ばれるたび +1 される **判定イベント数**
  で、HOLD 中断では同じノーツが 2 回分として数えられる。PR #256 (#182) が
  正本の `GManager.settledNoteCount` (= `JudgeScoreboard.Settled`) を追加し、
  `nextNotes` は「1 + 確定数」の互換値になった。本ブランチは **PR #256 取り込み後の
  `origin/main` (877bde7) をマージ済み** なので、確定数は `GManager.settledNoteCount`
  から読む。
  (`settledNoteCount == Perfect + Great + Bad + Miss` が定義なので、判定の内訳の総数と
   必ず一致する。本ハーネスはこの **正** の値を「全ノーツ確定」の根拠にする。)
- ただし **HOLD を含む譜面での実機確認は今回できていない**。カタログの譜面は
  全て Tap のみで HOLD を含まない (`ChartDataIntegrityTests.EveryNoteOfTheExistingChartsIsATapNote`
  が固定) ため、HOLD 実機ケースは別譜面を用意する必要があり、本作業の範囲外。

### 実測 (macOS 実機)

環境: macOS 27.2 (Darwin 27.2.0, arm64) / Unity 6000.5.6f1 / ビルド:

```
Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneMacOS
→ [BuildScript] Build succeeded: 409424755 bytes, output to Builds/macOS/FlashBeat.app
```

起動 (`script/mac-songend-harness.sh`): **実行ファイルを直叩き**して pid を保持する
(ユーザーが手元で起動した FlashBeat は kill しない。`pkill` / `osascript` は使わない)。

```
Builds/macOS/FlashBeat.app/Contents/MacOS/FlashBeat \
  -screen-width 2560 -screen-height 1664 -screen-fullscreen 1 \
  -autotest v6songend -autotest-out <dir> -logFile <dir>/Player.log
```

= **実ディスプレイ (2560x1664) の全画面**。窓リサイズによる擬似全画面ではない。
(ログの `FullscreenDiagnostics`: `screen=2560x1664 ... fullScreen=True
fullScreenMode=FullScreenWindow`)。スクリプトは **失敗を隠さない**: 結果ファイルが無い /
`fail=0` でない / `Player.log` に `FAIL ` 行がある のいずれかなら非 0 で終了する
(スクリプトの終了コードが実行の合否の唯一の根拠)。

#### 実行 A (`Builds/songend-20261110-001927/`, レビュー指摘の修正後・main 取り込み後の実行)

結果ファイル `Builds/songend-20261110-001927/autotest-v6songend-result.txt`:

```
[AutoTestV6SongEnd] RESULT pass=29 fail=0
song=ツイッターランド
songEndState=Completed
forcedByDeadline=False
scene=ResultScene
perfect=129 great=0 bad=0 miss=1
judged=130
settled=130
chartNotes=130 runtimeNotes=130
score=992307 combo=129 maxCombo=129 comboPeak=129
```

判定合計 = 129 + 0 + 0 + 1 = **130 = 譜面のノーツ数と一致**、
かつ `settled == chartNotes == runtimeNotes == 130`、
`score = 992,307 > 0`、`combo / maxCombo / comboPeak = 129 > 0`、
`forcedByDeadline=False` (= 期限切れ強制完了ではない)。

スクリプトの終了コードも 0 (`[songend] OK: the harness passed`) で、
「結果ファイルが無い / `fail=0` でない / ログに `FAIL ` 行がある」のいずれでも
非 0 になることは、スタブプレイヤー (実機と同じ引数を受け取るダミー実行ファイル)
で 3 パターン (fail=1 / ログの FAIL 行 / 結果ファイル無し) を再現して確認した。

`Player.log` の抜粋:

```
[FullscreenDiagnostics] event=startup, screen=2560x1664, currentResolution=2560x1664@60.0024000960038Hz, fullScreen=True, fullScreenMode=FullScreenWindow, dpi=299, safeArea=0,0,2560x1664, displays=1, display0=system:3420x2224,rendering:2560x1664,active:True
[MusicManager] media plan 'ツイッターランド': combined mp4/H.264 itag=18 640x360 (merge available: True)
[MusicManager] video file: .../FlashBeatMedia/e_qQEU_uGjw/merged.mp4 (cache)
[MusicManager] VideoPlayer prepared=True file=merged.mp4 after 0.0s
[MusicManager] media ready: song='ツイッターランド' audioMode=StreamCombined video=ok (bundled wav missing) - press Space to start
[MusicManager] PLAYBACK_START song='ツイッターランド' videoId='e_qQEU_uGjw' source=StreamCombined mediaTime=0.000 wallClock=2026-11-10T00:10:33.374 gameTime=11.677
[MusicManager] STATUS mediaTime=4.03 audioMode=StreamCombined rms=0.2482 peak=0.4877 video[playing=True frame=4158 prepared=True] perfect=1 great=0 bad=0 miss=1 failure=None
[MusicManager] STATUS mediaTime=42.23 audioMode=StreamCombined rms=0.1631 peak=0.7098 video[playing=True frame=4158 prepared=True] perfect=129 great=0 bad=0 miss=1 failure=None
[MusicManager] song completed: judged 130/130, mediaEnded=False, state=Completed
[AutoTestV6SongEnd] playback summary: songEndState=Completed mediaTime=44.03 hitsSent=129 perfect=129 great=0 bad=0 miss=1 score=992307 combo=129 maxCombo=129 comboPeak=129 judged=130 settled=130 chartNotes=130 runtimeNotes=130 forcedByDeadline=False
```

読み取れること:

- **音が出ている**: `rms` が 0.24〜0.47 で 0 になっていない (bundled wav は無いので
  YouTube メディアの音声が唯一の音源)。
- `mediaEnded=False` = YouTube 動画 (139 秒) 自体は終わっていないが、**カタログ曲長
  (44 秒) の終了条件 (mediaTime >= duration − 1 = 43 秒) が成立して曲終了になった**。
  `judged 130/130` と `state=Completed` であり、**期限切れ強制完了ではない**
  (`forcedByDeadline=False`)。
- 判定は時間とともに積み上がっている (`mediaTime=4.03` で perfect=1 →
  `mediaTime=42.27` で perfect=129)。
- **毎フレーム注入**に直した結果、当たり判定 (perfect) が 129/130 と 大幅に良くなった
  (修正前は 0.25 秒周期のポーリングで 66〜74 個しか当たらなかった)。

ハーネスのアサーションは 29 件すべて PASS で、**FAIL 行は 1 行も出ていない**:

```
[AutoTestV6SongEnd] PASS the select scene is loaded
[AutoTestV6SongEnd] PASS the v6 select view exists
[AutoTestV6SongEnd] PASS the data source is present
[AutoTestV6SongEnd] PASS the catalog has more than one song
[AutoTestV6SongEnd] PASS the song 'ツイッターランド' is in the catalog
[AutoTestV6SongEnd] PASS the selection follows the picked song
[AutoTestV6SongEnd] PASS the screenshot 01-select was written
[AutoTestV6SongEnd] PASS ready starts the game scene
[AutoTestV6SongEnd] PASS the started song is the picked one
[AutoTestV6SongEnd] PASS the screenshot 02-game was written
[AutoTestV6SongEnd] PASS the music manager exists
[AutoTestV6SongEnd] PASS the media pipeline finished
[AutoTestV6SongEnd] PASS the playback started
[AutoTestV6SongEnd] PASS the screenshot 03-gameplay was written
[AutoTestV6SongEnd] PASS the media clock advances
[AutoTestV6SongEnd] PASS the chart note count is known
[AutoTestV6SongEnd] PASS the runtime note count matches the chart (runtimeNotes == chartNotes)
[AutoTestV6SongEnd] PASS every chart note is settled (settled == chart notes)
[AutoTestV6SongEnd] PASS the score is greater than zero
[AutoTestV6SongEnd] PASS the combo rises above zero during the playback (GManager.combo peaked above zero)
[AutoTestV6SongEnd] PASS the peak combo is recorded (GManager.maxCombo > 0)
[AutoTestV6SongEnd] PASS the song end state machine completes
[AutoTestV6SongEnd] PASS the song end is not a forced deadline completion
[AutoTestV6SongEnd] PASS the result scene is loaded
[AutoTestV6SongEnd] PASS the v6 result canvas exists
[AutoTestV6SongEnd] PASS the v6 result canvas has non-empty text
[AutoTestV6SongEnd] PASS the v6 result canvas shows the song title
[AutoTestV6SongEnd] PASS the v6 result canvas shows the final score
[AutoTestV6SongEnd] PASS the screenshot 04-result was written
[AutoTestV6SongEnd] RESULT pass=29 fail=0
```

Result Canvas の実測 (S2 の強化) :

```
[AutoTestV6SongEnd] result screen: v6ResultCanvas=True nonEmptyTextUnderCanvas=True titleFound=True scoreFound=True textsUnderCanvas=51 texts=182 scene=ResultScene
```

= 「`V6 Result Canvas` の **子孫** に空でない文字がある (51 文字 / Canvas 全体で 182)」
かつ「その中に **曲名** がある」かつ「**最終スコア** がある」。

#### 実行 B (修正前, `Builds/songend-run4/` / `Builds/songend-run3/`) = 参考 (レビュー前)

```
[AutoTestV6SongEnd] RESULT pass=21 fail=0
song=ツイッターランド
songEndState=Completed
scene=ResultScene
perfect=66 great=21 bad=14 miss=29
score=626153 combo=28
```

2 回の独立実行で **どちらも pass=21 fail=0 / songEndState=Completed / scene=ResultScene**、
かつ **どちらも判定合計 130 = ノーツ数**。判定の粒 (perfect/great/bad/miss の内訳) は
キーの注入タイミングで変わるので run 間で差が出るのは想定どおり。
(この 2 回は **修正前の実装** — アサーション 21 件・0.25 秒周期ポーリング・結果ファイルに
`fail=0` を書かない — での実行であり、**スクリプトは常に 0 で終了していた**。
修正後は実行 A がそのままの合否判定になる。)

#### スクリーンショット (ゲーム内で `ScreenCapture` した実機の画面)

| ファイル | 画面 | 目視所見 |
|---|---|---|
| `Builds/songend-20261110-001927/autotest-v6songend-01-01-select.png` | 選曲画面 | 「ツイッターランド」が**実機の画面上で選択**されている (ジャケット画像が描画)。詳細パネルに `No.4 / STEAKA`、BPM 142 / NOTES 130 / LEVEL 15 / BEST 738,461。曲リストは 28/28 を表示し、対象行だけがハイライト。右下に `Ready (Space)`。日本語は `□` にならず **`ツイッターランド` / `曲別ランキング` / `設定` などが正常に描画**されている。 |
| `Builds/songend-20261110-001927/autotest-v6songend-02-02-game.png` | GameScene (開始前) | 中央に `Space to Start` パネル。HUD は Rank C / SCORE 0 / レートバー (C B A S SS +) が描画。左に COMBO 0。**下端のレーンキー表示 (A S D F J K L …) と判定ライン (白い横線) は描画されている**。右上に `メニュー` ボタン。 |
| `Builds/songend-20261110-001927/autotest-v6songend-03-03-gameplay.png` | 再生中 | HUD と判定表示 (`Miss` の文字が 6 個同時に出ている) は描画されている。**ただしレーン上に落ちるノーツの絵と動画映像は写っていない** (プレイフィールドが黒のまま)。同じフレームのログは `video[playing=True]` なので動画自体は再生している。→ プレイフィールド/映像の見え方は本ハーネスの管外 (#233 の別受入項目で別ワークが対応中)。**本ハーネスの主張は「曲終了 → Result 到達」であり、プレイフィールドの見え方は主張していない。** |
| `Builds/songend-20261110-001927/autotest-v6songend-04-04-result.png` | **Result 画面** | **v6 の Result 画面が実機で描画されている**: 左に Rank **S**、`SCORE 992,307`、`前回最高 +98,461`、レートバー (C B A S SS + の目盛、先端まで到達)。判定欄 `Perfect 129 / Great 0 / Bad 0 / Miss 1 / Combo 129`。選曲欄 `ツイッターランド / STEAKA`、BPM 142 / NOTES 130 / LEVEL 15 / 曲長 0:44。右に `曲別ランキング / NO DATA` と `登録名ごとのランキング / NO DATA`。下部に操作表示 `R リトライ / Enter 登録 / Space 決定` とボタン `Retry (R)` / `Register (Enter)` / `OK (Space)`。**この 1 枚を目視で確認した** (スクリーンショットを実際に読み込んで目視)。曲名 (`ツイッターランド`) と最終スコア (`992,307`) が **同じ Result Canvas の中に** 描画されている = S2 の主張と整合する。 |

修正前の実行 B (`run3`) の Result 画面も同構造で、Rank **B** / SCORE **716,923** /
Perfect 74 / Great 25 / Bad 21 / Miss 10 / Combo 22 だった (数値は result.txt と一致)。
修正後の実行 A は Rank **S** / SCORE **992,307** / Perfect 129 / Great 0 / Bad 0 /
Miss 1 / Combo 129 (数値は result.txt と一致)。

OS レベルの `screencapture` は macOS の Space 分離で **ゲームの Space ではなく
デスクトップを撮る** ため `os-*.png` は本証拠には使っていない (Evidence として採用しない)。
**スクリプトは既定で OS レベルのスクリーンショットを撮らない** (`--with-os-screenshot`
を明示した時だけ撮る)。使用するのはゲーム内 `ScreenCapture` の 4 枚。

#### EditMode テスト (全件, `-quit` なし)

```
Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults-258.xml
→ total=1193 passed=1193 failed=0 skipped=0 inconclusive=0 duration=4.2817188s
```

`origin/main` (877bde7) のベースライン 1185 件 + 本作業で追加した 8 件 = **1193 件、失敗 0**。
新規 8 件 (`SongEndHarnessTests`) は全て PASS を確認済み
(うち 1 件は `MusicManager.SongEndForcedByDeadline` が「新しい MusicManager では false」
であることを確認するものを含む)。

## 3. 未検証点 / 残存リスク

1. **Windows 実機 (展示 PC) では未検証**。本証拠は macOS (Unity 6000.5.6f1, macOS player) での
   実測である。Windows の Media Foundation / VideoPlayer の挙動は別 (issue #13 の
   `WindowsVideoMedia error 0x80070005` は未解決のまま) なので、**Windows 実機での
   曲終了到達は依然として未証明**。本成果物は「macOS で曲終了 → Result まで通ることの
   証明」と「その手順を再現する仕組み」であり、Windows の代替ではない。
2. **判定の命中は macOS のキー注入での実証**。実機のキーボードを物理的に叩いた
   わけではない (`KeyBindings.InjectKeyDown` による注入)。Windows の実キーボードでの
   判定は未検証。
3. **メディアが無い場合の経路 (壁時計フォールバック) は今回は通っていない**:
   ダウンロードが成功したため実メディア経路で完走した。フォールバック経路
   (`MediaTimeline.FallbackSongEnd`) の実機確認は未実施。
4. **1 曲分だけ**。全曲への適用は未実施 (譜面によっては末尾ノーツが曲長を超えるため
   強制完了になる歌がある。EditMode テストは対象曲についてのみ固定している)。
5. `Judge` の入力判定を `KeyBindings.GetKeyDownOrInjected` に変えた点は、製品ビルドでは
   挙動不変 (注入は自動テストからのみ)。`GetKeyDownOrInjected` の注入は
   **注入したフレームの 1 回だけ** 真になる (`Time.frameCount == 注入フレーム + 1`)。
   つまり「注入されたフレームの次のフレーム」で真になる 것이 1 回だけで、
   さらに次のフレームでは false に戻る。同一キーを 2 回注入すると後勝ちで
   最初の注入が失われる (そのためハーネスは 1 フレームにつき 1 レーン 1 打に
   している)。製品ビルドでは注入が起きないため影響なし。
6. **再生中のプレイフィールド (落ちるノーツ / 動画映像) は目視で確認できなかった**
   (実機のスクリーンショット `autotest-v6songend-03-03-gameplay.png` では HUD と判定表示
   (`Miss` 等) と下端のレーンキー表示・判定ラインは描画されているが、レーン上にノートは
   写っていない)。同じフレームのログは `video[playing=True]` なので再生自体はしている。
   曲終了 → Result 到達 (本ハーネスの主張) とは別の論点であり、#233 の残りの受入条件
   (プレイフィールドの見え方) は本作業では触っていない。
7. **再現スクリプトは macOS 専用** (`script/mac-songend-harness.sh` は macOS の
   プレイヤー (.app) を直接起動する)。Windows 実機用の起動スクリプトは作っていない。
8. **本次の実行が確かめたのは「カタログ曲長 (duration) による終端」の経路だけ**。
   YouTube の実動画 (139 秒) は再生中に終わらないので、
   `mediaEnded` (実メディアの終端) 側は本研究では通っていない。
   したがって **「メディアが先に終わった場合」の曲終了経路**、
   および **メディアの破綻 (ストール / VideoPlayer エラー) で `Failed` になる経路** は
   実機未検証である (この 2 経路は `SongEndMachine` の EditMode テストと
   `MediaTimeline` の単体テストでロジックだけを検証している)。
   実機ハーネスが通した経路は「duration 到達 → ノーツ確定待ち → Completed」であり、
   `Failed` への遷移は本ハーネスでは観測していない。
