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
4. `Judge.Update` が `Input.GetKeyDown` を lane の key に照合し、`GManager.StartTime` との差で `Perfect` (≤0.10s) / `Great` (≤0.15s) / `Bad` (≤0.20s) / `Miss` を付ける。
5. 終了時に `MusicManager` が fade し、`Initiate.Fade("ResultScene", ...)` へ進む。

### 8 lane keymap

```text
Lane 0: 1 Q A Z          Lane 4: 6 7 Y U H J N M
Lane 1: 2 W S X          Lane 5: 8 I K , <
Lane 2: 3 E D C          Lane 6: 9 O L . >
Lane 3: 4 5 R T F G V B  Lane 7: 0 - ^ \ P @ [ ; : ] / _ * `
```

## 楽曲データ

- chart: `Assets/Game/Resources/<SongName>.json`。`name`, `maxBlock`, `BPM`, `offset`, `notes[]` (`type/num/block/LPB`)、任意の `lyrics`
- lyrics: chart 内の `lyrics` が正。`GameSceneManager.LoadTextData` は merged chart を優先し、legacy の `<SongName>_text.json` があっても chart 側を使う
- 音源: `Assets/Game/Resources/Musics/<SongName>.wav`
- `Assets/Resources/` にも旧 chart 群が残る。新しい曲は `Assets/Game/Resources/` だけを更新する

### 曲の追加

Editor menu `Tools → NoteEditor → Add Song to FlashBeat...` が `SongMetaDialog`, `YouTubeSceneWiring`, `GManagerParallelArrayUpdater` をまとめて実行する。Play Mode 中は `DeferredEditorActions` で書き込みを遅延する。

手作業で行う場合:

1. `Assets/Game/Resources/<SongName>.json` を置く。
2. `GManager` の 7 つの parallel array と `Hiscore` の長さ、`totalSong` を揃える。
3. `GameScene.unity` の `YoutubePlayer` と `MusicManager.YPlayer` / `VPlayer` を配線する（通常は Editor tool が行う）。
4. lyrics が無い場合は stub の `_text.json` を作る。

## YouTube 再生

`YouTubeStreamResolver` (`Assets/Game/Scripts/Gameplay/`) が `yt-dlp.exe` を起動し、videoId を `googlevideo.com` の direct URL に解決する（URL の寿命は約 6 時間）。`yt-dlp.exe` は `PATH`、一般的な Python install dir、`Assets/StreamingAssets/yt-dlp/` の順で探し、`Edit > Project Settings > FlashBeat > YouTube` で上書きできる。

`com.ibicha.youtube-player` の Invidious 経路は runtime では使わない。`InvidiousInstanceProber` は診断用。

## Tests

`Assets/Tests/Editor/` に 44 ファイル、447 個の `[Test]` / `[TestCase]` 属性がある。GManager の song lookup、`ResetSession`、lane wrap-around、判定時刻、NoteEditor の lyric roundtrip、`LyricsTabPresenter`、`FlashBeatSongLoader`、legacy migration、YouTube import、media cache、update checker を対象にする。Play Mode / 実機 integration suite はない。

`SingletonTestHelper.TeardownSingletons` で static state を test 間に戻す。`EditData.Lyrics` の配列を使う test では必須。

CLI 実行では `-quit` を付けない。

```sh
Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml
```

## NoteEditor

`Assets/NoteEditor/` は setchi/NoteEditor (MIT) の vendored copy。upstream SHA と import 情報は `Assets/NoteEditor/IMPORT.md`。

`NoteEditor.unity` は build scenes に無いため player には入らない。NoteEditor の保存形式と FlashBeat の chart JSON は互換ではない。変換が必要なら `NoteEditor.Editor` 側に exporter を足す。

## Conventions / gotchas

- 4 space indent、LF、UTF-8 BOM。method は block body（`.editorconfig`）。
- `GManager` は DI ではなく static field を使う。削除時は variable を消さず comment out する方針が `MakeFileSceneManager` にある。
- `GManager.Songs[0]` は `id=0`, `title="noSong"` の placeholder。実曲は `id=1` から。
- 7 つの parallel array と chart は positional に結び付く。曲を消す時は全 array と chart を同じ位置で扱う。
- `GManager.SongName[27]` / `[28]` はどちらも `ニセモノ` で、`Musics/ニセモノ.wav` が無い。`MusicManager` は `Resources.Load` の null を返さないため、この 2 つは選択しない。
- `Library/`, `Temp/`, `Obj/`, `UserSettings/`, `Logs/`, `Build/`, `Builds/`, `TestResults.xml` は生成物。手で編集しない。
- 日本語 font は `NotoSansJP-Medium SDF.asset`。新しい漢字を含む曲・歌詞を足したら `Tools → FlashBeat → Rebuild NotoSansJP SDF Asset` を実行する。atlas は静的で、古い asset は 620 glyph しか持っていなかった。
- 大きい font asset (`NotoSansJP-Medium SDF.asset`, `YuGothB SDF.asset`) は `.gitattributes` で個別に LFS 対象。
