# FlashBeat ゲームランタイム

この文書は Unity 側の構造と、変更時に踏みやすい罠をまとめる。agent の入口は [`AGENTS.md`](../../AGENTS.md)。

## Assemblies

| Assembly | Location | Platform | 役割 |
| --- | --- | --- | --- |
| `FlashBeat` | `Assets/Game/Scripts/FlashBeat.asmdef` | 全 platform | runtime gameplay (`Common`, `Gameplay`, `Persistence`, `Songs`, `UI`) |
| `FlashBeat.Editor` | `Assets/Editor/FlashBeat.Editor.asmdef` | Editor のみ | build、font、scene wiring、GManager 更新 |
| `NoteEditor` | `Assets/NoteEditor/NoteEditor.asmdef` | 全 platform | vendored setchi/NoteEditor。build scenes に含まれないため player には入らない |
| `NoteEditor.Editor` | `Assets/Editor/NoteEditor.Editor/NoteEditor.Editor.asmdef` | Editor のみ | `MigrateLegacyTextJson`, YouTube import |
| `FlashBeat.Tests` | `Assets/Tests/Editor/FlashBeat.Tests.asmdef` | Editor のみ | EditMode tests |

`SimpleFadeSystem` は `Assets/ThirdParty/` の vendored assembly。FlashBeat 本体は UniRx を参照しない。NoteEditor だけが参照する。

## Scene と manager

scene ごとに 1 つの `*SceneManager` が UI と `Update` を持つ。

`OpeningSceneManager`, `TitleSceneManager`, `SelectSceneManager`, `GameSceneManager`, `ResultSceneManager`, `OptionSceneManager`, `TypingSceneManager`, `MakeFileSceneManager`

build scenes は `Assets/Game/Scenes/` の Opening / Title / Select / Game / Result / Option。`TypingScene` と `MakeFileScene` は build に含めない。

## 共有状態

`GManager` (`Assets/Game/Scripts/Songs/GManager.cs`) は `DontDestroyOnLoad` の singleton で、cross-scene state を **static field** に持つ。

- song catalog: `SongName`, `Musician`, `SongURL`, `SBPM`, `Slevel`, `Shit`, `SongLong` の parallel array から `Songs` を構築
- play state: `score`, `combo`, `perfect/great/bad/miss`, `noteSpeed`, `noteTiming`, `Start`, `StartTime`, `played`
- settings: `mainVolume`, `effectVolume`, `BGMVolume`, `FlashBG`, `Flash`, `FlashT`
- high score: `int[] Hiscore`。`SaveLoadManager` が `PlayerPrefs` の `"Hiscore"` に CSV で保存する

play session の間は `GManager.ResetSession()` で combo / score を戻す（`GameSceneManager.RetryGame` が呼ぶ）。

## Gameplay loop

1. `MusicManager` は `Space` を待ち、`VideoPlayer` と `AudioSource` を開始する。
2. `NotesManager.OnEnable` が chart JSON を読み、BPM / LPB から各 note の時刻を計算し、8 lane に prefab を置く。
3. `Notes` が `GManager.noteSpeed` で note を判定線へ動かす。
4. `Judge.Update` が `Input.GetKeyDown` を lane の key に照合し、`GManager.StartTime` との差で `Perfect` (≤0.10s) / `Great` (≤0.15s) / `Bad` (≤0.20s) / `Miss` を付ける。同じフレームで `Judge.PlayLaneClickSound` がレーンキー押下 (空押しを含む) に 1 フレーム 1 回だけ `clickSound` を鳴らす (Issue #604)。入力の受付可否は判定とクリック音で共有の `GameplayInputGate.CanAcceptInput` (`GManager.Start` かつ非ポーズ・曲終了シーケンス前) が決める。クリック音は `audioHit` (音量 = `GManager.effectVolume`、Effect mixer) を使うが `hitSound` とは別クリップ。
5. 終了時に `MusicManager` が fade し、`Initiate.Fade("ResultScene", ...)` へ進む。

### 採点 (scoring)

- 判定配点は `LongNote.ScoreFor` = Perfect 5 / Great 3 / Bad 1。現在の採点は scoring v3 (ADR-0029): `Scoring.BreakdownV3` が `judgment + maxCombo*5 + (miss==0 ? hits*5 : 0) + comboMilestones*25 + (judgmentsComplete && miss==0 ? 100 : 0)` を ratio として出し、`Scoring.ScoreV3` が `floor(ratio * 1_000_000 / maxScore)` (`maxScore = Scoring.MaxRatioV3(noteCount)` = `15*N + floor(N/50)*25 + 100`) を **整数演算**で計算する (Issue #631: float を介さないため全プラットフォームで一致。v1/v2 は従来どおり float 経路)。`judgmentsComplete` は全ノーツの判定確定 (P+G+B+M == noteCount、`GManager.judgmentsComplete`) で、フルコンボ終了ボーナスはこの条件でのみ曲終了時に一度だけ乗る (Issue #618)。Miss は加点なしでコンボのみ切断し、Bad はコンボを切らない。節目 (50 コンボごと) の到達回数は `JudgeScoreboard.MilestoneCrossings` が数え、コンボ切断後も累積する。
- 採点 v2 (コンボ継続・ノーミス加点、`scoringVersion = 2`) の式・バージョン互換・記録分離の正本は `docs/adr/ADR-0028.md`。ローカル記録は `ScoreKey` の `songId@chartVersion@scoringVersion` 形式で版を区別する (v1/v2 の旧記録はそのまま残る)。
- 採点 v3 (コンボ節目ボーナス・フルコンボ最終ボーナス、`scoringVersion = 3`) は ADR-0029 が正本。節目は 50 コンボごとに 25 ratio 点、フルコンボ (miss==0 かつ全ノーツ判定確定) で終了時に 100 ratio 点。節目回数はパス依存のため `comboMilestones` フィールドで申告し、サーバは miss==0 で `floor((P+G+B)/50)` との等値、miss>0 で `min(floor(hits/50), (miss+1)*floor(maxCombo/50))` の上限と `maxCombo <= hits` を検査する (Issue #618)。

### 8 lane keymap

```text
Lane 0: 1 Q A Z          Lane 4: 6 7 Y U H J N M
Lane 1: 2 W S X          Lane 5: 8 I K , <
Lane 2: 3 E D C          Lane 6: 9 O L . >
Lane 3: 4 5 R T F G V B  Lane 7: 0 - ^ \ P @ [ ; : ] / _ * `
```

## 楽曲データ

- chart: `Assets/Game/Resources/<SongName>.json`。`name`, `maxBlock`, `BPM`, `offset`, `notes[]` (`type/num/block/LPB`)、任意の `lyrics`
- chart の入力境界検証 (Issue #632): `ChartValidator.Validate` が `type ∈ {1,2}`・`num >= 0`・`block ∈ [0,7]`・`LPB ∈ [1,192]`・HOLD の `endNum > num`・`BPM ∈ [1,600]`・`notes <= 10000` を公開スキーマ (`contracts/schemas/chart.v1.schema.json`) と同じ範囲で検査する。ローカル曲は `NotesManager.LoadRoutine` が、リモート曲は `SelectSceneManager` の譜面取得直後が検証点で、違反した譜面はゲーム進行へ流さず拒否する (リモートは開始キャンセル、ローカルは開始不可メッセージのあと選曲へ戻る)。範囲外レーンを含む譜面が「読み込めるが判定が永久に完了しない」状態になることを防ぐ。
- lyrics: chart 内の `lyrics` が正。`GameSceneManager.LoadTextData` は merged chart を優先し、legacy の `<SongName>_text.json` があっても chart 側を使う
- 音源: `Assets/Game/Resources/Musics/<SongName>.wav`
- chart JSON は Resources folder に 1 つだけ置く。同名 chart が 2 つの Resources folder にあると `Resources.Load` がどちらか一方を恣意的に返す。`Assets/Tests/Editor/ResourceChartUniquenessTests.cs` がこれを検査する

### 曲の追加

Editor menu `Tools → NoteEditor → Add Song to FlashBeat...` が `SongMetaDialog`, `YouTubeSceneWiring`, `GManagerParallelArrayUpdater` をまとめて実行する。Play Mode 中は `DeferredEditorActions` で書き込みを遅延する。

手作業で行う場合:

1. `Assets/Game/Resources/<SongName>.json` を置く。
2. `GManager` の 7 つの parallel array と `Hiscore` の長さ、`totalSong` を揃える。
3. `GameScene.unity` の `YoutubePlayer` と `MusicManager.YPlayer` / `VPlayer` を配線する（通常は Editor tool が行う）。
4. lyrics は chart JSON 内の `lyrics` が正。別ファイル (`*_text.json`) はもう置かない — 旧形式が残っていても chart 側が優先される (上記「楽曲データ」参照)。

## YouTube 再生

`YouTubeStreamResolver` (`Assets/Game/Scripts/Gameplay/`) が `yt-dlp.exe` を起動し、videoId を `googlevideo.com` の direct URL に解決する（URL の寿命は約 6 時間）。`yt-dlp.exe` は `PATH`、一般的な Python install dir、`Assets/StreamingAssets/yt-dlp/` の順で探し、`Edit > Project Settings > FlashBeat > YouTube` で上書きできる。

`com.ibicha.youtube-player` の Invidious 経路は runtime では使わない。`InvidiousInstanceProber` は診断用。

## Tests

`Assets/Tests/Editor/` に 146 の test file (2026-11-18 時点) があり、GManager の song lookup、`ResetSession`、lane wrap-around、判定時刻、NoteEditor の lyric roundtrip、`LyricsTabPresenter`、`FlashBeatSongLoader`、legacy migration、YouTube import、media cache、update checker、chart 整合性を対象にする。v6 UI の純ロジックは `Assets/Game/Tests/` (60 file、EditMode で同じく実行) にある。Play Mode / 実機 integration suite はない。

`SingletonTestHelper.TeardownSingletons` で static state を test 間に戻す。`EditData.Lyrics` の配列を使う test では必須。

CLI 実行では `-quit` を付けない（test runner 起動前に editor が終了し、test が 1 件も走らず `TestResults.xml` も書かれない）。

```sh
Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml
```

Unity を起動せずに C# の型検査だけ行う補助として `script/compile-check.sh` がある（dotnet、`mise.toml` で pin）。複数 worker が並行で Unity C# を編集する時の競合回避用で、最終的な正本は Unity Editor / batch の結果。

## NoteEditor

`Assets/NoteEditor/` は setchi/NoteEditor (MIT) の vendored copy。upstream SHA と import 情報は `Assets/NoteEditor/IMPORT.md`。

`NoteEditor.unity` は build scenes に無いため player には入らない。NoteEditor の保存形式と FlashBeat の chart JSON は互換ではない。変換が必要なら `NoteEditor.Editor` 側に exporter を足す。

## Conventions / gotchas

- 4 space indent、LF、UTF-8 BOM。method は block body（`.editorconfig`）。
- `GManager` は DI ではなく static field を使う。削除時は variable を消さず comment out する方針が `MakeFileSceneManager` にある。
- `GManager.Songs[0]` は `id=0`, `title="noSong"` の placeholder。実曲は `id=1` から。
- 7 つの parallel array と chart は positional に結び付く。曲を消す時は全 array と chart を同じ位置で扱う。
- `Library/`, `Temp/`, `Obj/`, `UserSettings/`, `Logs/`, `Build/`, `Builds/`, `TestResults.xml` は生成物。手で編集しない。
- `GManager.SongName[27]` / `[28]` はどちらも `ニセモノ` で、cloud catalog の placeholder（chart / wav 無し）。index をずらさないため配列には残し、`GManager.UnplayableTitles`（`noSong` も含む）で選択不可にしている。
- `GManager` の 7 つの parallel array は 29 entry で lockstep。かつて 30 番目の数値 entry は comment out されており、`ChartDataIntegrityTests` がこの対応を検査する。
- `MusicManager` は bundled wav を `Resources.LoadAsync<AudioClip>` で非同期に読み、clip が無くても throw せず YouTube media へ fallback する（`PlaybackRecovery.DecideAudioSource`）。
- `NoteEditor.Editor` の callback（YouTube import 登録、deferred editor actions）は `[InitializeOnLoad]` の static registrar pattern。cross-asmdef の callback 配線は `Assets/Editor/NoteEditor.Editor/YouTubeImportRegistrar.cs` を参照。
- 例外を握りつぶして degraded fallback へ進む経路は、原因が分かる警告ログを残す (Issue #613): `MusicManager` (音声トラック有効化失敗)、`MediaCacheManager` / `YouTubeMediaCache` (キャッシュ走査・削除・ダウンロード中断)、`YouTubeStreamResolver` (yt-dlp の exit / stdout 読み取り失敗)、`UpdateChecker` / `OptionSceneManager` / `RankingClient` (fallback の裏にある本当の原因)。
- プレイフィールドは unlit / 影なしの flat rendering。`docs/architecture/flat-rendering.md` を参照。lit shader に戻すと動画 quad がほぼ黒くなる。
- 日本語 font は `NotoSansJP-Medium SDF.asset`。HUD への適用は `JapaneseFontFixer.ApplyJapaneseFontToProject`（Editor menu `Tools → FlashBeat → Apply Japanese Font To Scenes And Prefabs`）。新しい漢字を含む曲・歌詞を足したら `Tools → FlashBeat → Rebuild NotoSansJP SDF Asset` を実行する。atlas は静的で、古い asset は 620 glyph しか持っていなかった。
- Windows baseline (issue #11): batch build と select → play → result → retry の 1 曲 loop は実機で確認済み（`docs/superpowers/plans/baseline-win64.md`）。Windows Media Foundation の URL reader は解決済み `googlevideo.com` stream で失敗する（`0x80070005` / `MFCreateSourceReaderFromURL`）ため、`MusicManager` は yt-dlp で media を `persistentDataPath/FlashBeatMedia/<videoId>/` に download し local file を再生する（`PrepareLocalVideo`）。direct URL の試行は video ごと 1 回の診断 probe（`ProbeDirectUrl`）として残るので、`Player.log` の `0x80070005` は missing picture の原因ではない。Windows での download 済み file 再生と audible output は未検証（同 § 2026-11-10 追記、issue #13）。
- 大きい font asset (`NotoSansJP-Medium SDF.asset`) は `.gitattributes` で個別に LFS 対象。
