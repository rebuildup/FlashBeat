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
| `FlashBeat.V5CanvasTests` | `Assets/Game/Tests/FlashBeat.V5CanvasTests.asmdef` | Editor のみ | v5 UI canvas tests |
| `FlashBeat.V6CanvasTests` | `Assets/Game/Tests/V6CanvasTests/FlashBeat.V6CanvasTests.asmdef` | Editor のみ | v6 UI canvas tests |
| `UniRx` | `Assets/NoteEditor/Plugins/UniRx/Scripts/UniRx.asmdef` | 全 platform | vendored UniRx。NoteEditor 系だけが参照する |
| `SimpleFadeSystem` | `Assets/ThirdParty/Simple Scene Fade Load System/SimpleFadeSystem.asmdef` | 全 platform | vendored scene fade |

`SimpleFadeSystem` と `UniRx` は vendored assembly。FlashBeat 本体は UniRx を参照しない。NoteEditor だけが参照する。

## Scene と manager

scene ごとに 1 つの `*SceneManager` が UI と `Update` を持つ。

`OpeningSceneManager`, `TitleSceneManager`, `SelectSceneManager`, `GameSceneManager`, `ResultSceneManager`, `OptionSceneManager`, `TypingSceneManager`, `MakeFileSceneManager`

build scenes は `Assets/Game/Scenes/` の Opening / Title / Select / Game / Result / Option。`TypingScene` と `MakeFileScene` は build に含めない。

## 共有状態

`GManager` (`Assets/Game/Scripts/Songs/GManager.cs`) は `DontDestroyOnLoad` の singleton で、cross-scene state を **static field** に持つ。

- song catalog: `SongName`, `Musician`, `SongURL`, `SBPM`, `Slevel`, `Shit`, `SongLong` の parallel array から `Songs` を構築
- play state: `score`, `combo`, `perfect/great/bad/miss`, `noteSpeed`, `noteTiming`, `Start`, `StartTime`, `played`
- settings: `mainVolume` (AudioListener 全体), `songVolume` (プレイ楽曲), `BGMVolume` (メニュー背景動画), `effectVolume` (SE), `FlashBG`, `Flash`, `FlashT` (Issue #851 でプレイ楽曲とメニュー BGM を分離)
- high score: `int[] Hiscore`。`SaveLoadManager` が `PlayerPrefs` の `"Hiscore"` に CSV で保存する

play session の間は `GManager.ResetSession()` で combo / score を戻す（`GameSceneManager.RetryGame` が呼ぶ）。

## Gameplay loop

1. `MusicManager` は `Space` を待ち、`VideoPlayer` と `AudioSource` を開始する。
2. `NotesManager.OnEnable` が chart JSON を読み、BPM / LPB から各 note の時刻を計算し、8 lane に prefab を置く。
3. `Notes` が `GManager.noteSpeed` で note を判定線へ動かす。
4. `Judge.Update` が `Input.GetKeyDown` を lane の key に照合し、`GManager.StartTime` との差で `Perfect` (≤0.10s) / `Great` (≤0.15s) / `Bad` (≤0.20s) / `Miss` を付ける。判定確定のたびに `Judge.message` が (判定, レーン) を静的キュー (`Judge.TryDequeueVerdict`) へ積み、v6 HUD のレーン別判定行がこれをドレインする (Issue #680。シーンルートの判定クローン走査には依存しない)。判定行が有効な間は `Judge.SuppressJudgementEffects` で 3D 判定エフェクトの Instantiate を止める。同じフレームで `Judge.PlayLaneClickSound` がレーンキー押下 (空押しを含む) に 1 フレーム 1 回だけ `clickSound` を鳴らす (Issue #604)。入力の受付可否は判定とクリック音で共有の `GameplayInputGate.CanAcceptInput` (`GManager.Start` かつ非ポーズ・曲終了シーケンス前) が決める。クリック音は `audioHit` (音量 = `GManager.effectVolume`、Effect mixer) を使うが `hitSound` とは別クリップ。
5. 終了時に `MusicManager` が fade し、`Initiate.Fade("ResultScene", ...)` へ進む。

### 採点 (scoring)

- 判定配点は `LongNote.ScoreFor` = Perfect 5 / Great 3 / Bad 1。現在の採点は scoring v3 (ADR-0029): `Scoring.BreakdownV3` が `judgment + maxCombo*5 + (miss==0 ? hits*5 : 0) + comboMilestones*25 + (judgmentsComplete && miss==0 ? 100 : 0)` を ratio として出し、`Scoring.ScoreV3` が `floor(ratio * 1_000_000 / maxScore)` (`maxScore = Scoring.MaxRatioV3(noteCount)` = `15*N + floor(N/50)*25 + 100`) を **整数演算**で計算する (Issue #631: float を介さないため全プラットフォームで一致。v1/v2 は従来どおり float 経路)。`judgmentsComplete` は全ノーツの判定確定 (P+G+B+M == noteCount、`GManager.judgmentsComplete`) で、フルコンボ終了ボーナスはこの条件でのみ曲終了時に一度だけ乗る (Issue #618)。Miss は加点なしでコンボのみ切断し、Bad はコンボを切らない。節目 (50 コンボごと) の到達回数は `JudgeScoreboard.MilestoneCrossings` が数え、コンボ切断後も累積する。
- 採点 v2 (コンボ継続・ノーミス加点、`scoringVersion = 2`) の式・バージョン互換・記録分離の正本は `docs/adr/ADR-0028.md`。ローカル記録は `ScoreKey` の `songId@chartVersion@scoringVersion` 形式で版を区別する (v1/v2 の旧記録はそのまま残る)。
- 採点 v3 (コンボ節目ボーナス・フルコンボ最終ボーナス、`scoringVersion = 3`) は ADR-0029 が正本。節目は 50 コンボごとに 25 ratio 点、フルコンボ (miss==0 かつ全ノーツ判定確定) で終了時に 100 ratio 点。節目回数はパス依存のため `comboMilestones` フィールドで申告し、サーバは miss==0 で `floor((P+G+B)/50)` との等値、miss>0 で `min(floor(hits/50), (miss+1)*floor(maxCombo/50))` の上限と `maxCombo <= hits` を検査する (Issue #618)。
- 採点 v4 (判定由来ベース ≤1M + 加算ボーナス ≤1M、`scoringVersion = 4`) は ADR-0033 が正本。連続コンボボーナス `maxCombo*5` と節目 `comboMilestones*25` を含む。
- 採点 v5 (節目到達のみがコンボ報酬、`scoringVersion = 5`) は ADR-0035 が正本 (Issue #797)。連続コンボボーナスは廃止し、節目 1 回あたり 250 ratio 点 (`Scoring.MilestoneBonusPointsV5`) が唯一のコンボ報酬。`maxBonusRatio = floor(N/50)*250 + 5N + 100`、スコア上限は v4 と同じ 2,000,000。miss==0 の確定プレイは v4 と同一スコア。

### 8 lane keymap

```text
Lane 0: 1 Q A Z          Lane 4: 6 7 Y U H J N M
Lane 1: 2 W S X          Lane 5: 8 I K , <
Lane 2: 3 E D C          Lane 6: 9 O L . >
Lane 3: 4 5 R T F G V B  Lane 7: 0 - ^ \ P @ [ ; : ] / _ * `
```

## 楽曲データ

Issue #823 以降、実曲の譜面は **クラウドカタログ経由でのみ供給される**。`Assets/Game/Resources/` に実曲の譜面 JSON は置かない (CI の `script/check-repo-integrity.sh` が拒否する)。権利未確認のローカルフォールバックは存在しない。

- chart の取得経路: 選曲 (`SelectSceneManager.GameScene`) → リモート曲は `StartRemoteSong` が `GET /api/v1/charts/{remoteId}?version={chartVersion}` を 1 回だけ取得 → `ChartValidator.Validate` → `CloudChartHolder.Hold` → `GManager.ResetSession` (`BeginPlaySession`) → `PlaySessionSnapshot` が曲・版・JSON を固定。取得失敗・検証失敗・選択変更時は開始を中止し、選曲画面へ明確なエラーを返す。非リモート行 (カタログ未取得の固定曲) はローカル譜面を持たないため開始できない。
- chart の読み取り経路: `NotesManager.ParseChart` と `ChartLyrics.Resolve` は同一の優先順 — `PlaySessionSnapshot` の固定 JSON → `CloudChartHolder` の保持 JSON。それ以外は null / Pending で、ローカル Resources へは落ちない。
- chart JSON の形: `{"songId":…,"chartVersion":…,"chart":{…}}` の封筒 (`CloudCatalog.ParseChartResponse` が読む)。chart 本体は `name`, `maxBlock`, `BPM`, `offset`, `notes[]` (`type/num/block/LPB`, HOLD は `endNum`)、任意の `lyrics` (`startTime/endTime/mondai/furigana/romaji` の並列配列)。
- chart の入力境界検証 (Issue #632): `ChartValidator.Validate` が `type ∈ {1,2}`・`num >= 0`・`block ∈ [0,7]`・`LPB ∈ [1,192]`・HOLD の `endNum > num`・`BPM ∈ [1,600]`・`notes <= 10000` を公開スキーマ (`contracts/schemas/chart.v1.schema.json`) と同じ範囲で検査する。リモート曲は `SelectSceneManager` の譜面取得直後が検証点で、違反した譜面は開始キャンセル。NotesManager 側も同じ検証を行い、不正譜面はゲーム進行へ流さず拒否する。
- lyrics: chart 内の `lyrics` が正。`ChartLyrics` が固定譜面から `TextData` を解決し、`Pending` / `NoLyrics` / `HasLyrics` を明示する (Pending はキャッシュしない)。
- 音源: YouTube ストリームのみ。ローカル WAV フォールバックは Issue #823 で撤去済み。
- 開発・テスト用の合成 fixture: `SyntheticCharts` (`Assets/Game/Scripts/Common/`) が権利クリアな譜面 JSON を組み立て、`CloudChartHolder` / `PlaySessionSnapshot` 経由で注入する。`remoteId` は `synthetic-` 接頭辞で本番カタログ・ランキングとは混ざらない。`HoldProbeChart` も同じ封筒形式の HOLD 検証用 fixture。

### 曲の追加

製品版の曲追加はクラウドカタログ (Web 側の公開パイプライン) で行う。ローカルの `GManager` 並列配列は曲情報のフォールバック (タイトル・アーティスト・動画 ID) であり、譜面は持たない。

## YouTube 再生

`YouTubeStreamResolver` (`Assets/Game/Scripts/Gameplay/`) が `yt-dlp.exe` を起動し、videoId を `googlevideo.com` の direct URL に解決する（URL の寿命は約 6 時間）。`yt-dlp.exe` は `PATH`、一般的な Python install dir、`Assets/StreamingAssets/yt-dlp/` の順で探し、`Edit > Project Settings > FlashBeat > YouTube` で上書きできる。

`com.ibicha.youtube-player` の Invidious 経路は runtime では使わない。`InvidiousInstanceProber` は診断用。

## Tests

`Assets/Tests/Editor/` に 151 の test file (2026-11-18 時点) があり、GManager の song lookup、`ResetSession`、lane wrap-around、判定時刻、NoteEditor の lyric roundtrip、`LyricsTabPresenter`、`FlashBeatSongLoader`、legacy migration、YouTube import、media cache、update checker、chart 整合性を対象にする。UI canvas tests (V5/V6) は `Assets/Game/Tests/` (60 file、EditMode で同じく実行) にある。Play Mode / 実機 integration suite はない。

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
- 7 つの parallel array は曲情報のフォールバック。譜面はクラウドカタログ経由 (上記「楽曲データ」参照)。曲を消す時は全 array を同じ位置で扱う。
- `Library/`, `Temp/`, `Obj/`, `UserSettings/`, `Logs/`, `Build/`, `Builds/`, `TestResults.xml` は生成物。手で編集しない。
- `GManager.SongName[27]` / `[28]` はどちらも `ニセモノ` で、cloud catalog の placeholder（chart / wav 無し）。index をずらさないため配列には残し、`GManager.UnplayableTitles`（`noSong` も含む）で選択不可にしている。
- `GManager` の 7 つの parallel array は 29 entry で lockstep。かつて 30 番目の数値 entry は comment out されており、`ChartDataIntegrityTests` がこの対応を検査する。
- `MusicManager` の音源は YouTube ストリームのみ (Issue #823 でローカル WAV フォールバックを撤去)。無音ストリームは `AudioSilent` のハード失敗で、同梱 WAV へは落ちない（`PlaybackRecovery.DecideAudioSource`）。
- `NoteEditor.Editor` の callback（YouTube import 登録、deferred editor actions）は `[InitializeOnLoad]` の static registrar pattern。cross-asmdef の callback 配線は `Assets/Editor/NoteEditor.Editor/YouTubeImportRegistrar.cs` を参照。
- 例外を握りつぶして degraded fallback へ進む経路は、原因が分かる警告ログを残す (Issue #613): `MusicManager` (音声トラック有効化失敗)、`MediaCacheManager` / `YouTubeMediaCache` (キャッシュ走査・削除・ダウンロード中断)、`YouTubeStreamResolver` (yt-dlp の exit / stdout 読み取り失敗)、`UpdateChecker` / `OptionSceneManager` / `RankingClient` (fallback の裏にある本当の原因)。
- プレイフィールドは unlit / 影なしの flat rendering。`docs/architecture/flat-rendering.md` を参照。lit shader に戻すと動画 quad がほぼ黒くなる。
- 日本語 font は `NotoSansJP-Medium SDF.asset`。HUD への適用は `JapaneseFontFixer.ApplyJapaneseFontToProject`（Editor menu `Tools → FlashBeat → Apply Japanese Font To Scenes And Prefabs`）。新しい漢字を含む曲・歌詞を足したら `Tools → FlashBeat → Rebuild NotoSansJP SDF Asset` を実行する。atlas は静的で、古い asset は 620 glyph しか持っていなかった。
- Windows baseline (issue #11): batch build と select → play → result → retry の 1 曲 loop は実機で確認済み（`docs/superpowers/plans/baseline-win64.md`）。Windows Media Foundation の URL reader は解決済み `googlevideo.com` stream で失敗する（`0x80070005` / `MFCreateSourceReaderFromURL`）ため、`MusicManager` は yt-dlp で media を `persistentDataPath/FlashBeatMedia/<videoId>/` に download し local file を再生する（`PrepareLocalVideo`）。direct URL の試行は video ごと 1 回の診断 probe（`ProbeDirectUrl`）として残るので、`Player.log` の `0x80070005` は missing picture の原因ではない。Windows での download 済み file 再生と audible output は未検証（同 § 2026-11-10 追記、issue #13）。
- 大きい font asset (`NotoSansJP-Medium SDF.asset`) は `.gitattributes` で個別に LFS 対象。
