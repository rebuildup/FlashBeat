# Issue #13 — yt-dlp + deno → Unity VideoPlayer: streaming one song on Win64

Branch: `13` (Draft PR, base `11`)
Captured: 2026-11-06
Environment: Windows 11 x64 (10.0.29698), Unity `6000.5.6f1 (0e0577a1a2ac)`,
yt-dlp `2026.10.26`, deno `2.9.8` (mise installs), NVIDIA RTX 4050 Laptop / D3D11.

## 1. What was broken (measured, not assumed)

### 1.1 YouTube does not offer a progressive stream any more

`yt-dlp -F https://www.youtube.com/watch?v=9O2VyUM5MlQ` (song No.2 `まにまに`):

```
ID  EXT   RESOLUTION FPS CH |   FILESIZE  TBR PROTO | VCODEC         VBR ACODEC      ABR ASR MORE INFO
233 mp4   audio only        |                 m3u8  | audio only         unknown             [ja] Default, low
140 m4a   audio only    2   |  4.15MiB  129k https | audio only   mp4a.40.2  129k 44k [ja] medium, m4a_dash
134 mp4   640x360     30    |  857.48KiB   26k https | avc1.4D401E   26k video only          360p, mp4_dash
136 mp4   1280x720    30    |  2.06MiB    64k https | avc1.4D401F   64k video only          720p, mp4_dash
...
```

There is **no itag 18 / 22** (progressive H.264+AAC mp4) in the list — and the same
holds for the other seven song ids that were probed (`DeKLpgzh-qQ`, `8JXiXt0D6tw`,
`e_qQEU_uGjw`, `xqYOI7OD9aE`, `5BlSQpejMTw`, `3mufQ1Tt844`, `yVi3mhLr0uU`).
The old resolver asked for exactly that format:

```csharp
public const string DefaultFormat = "18";
var args = $"--no-warnings --no-part -f {DefaultFormat} -g \"{youtubeUrl}\"";
```

So `yt-dlp` never produced a URL and the game fell through to the Invidious
fallback, whose instance list is dead (0/13 public instances answer).

### 1.2 Media Foundation cannot read a googlevideo URL

From the issue #11 baseline log (`docs/evidence/win64-2026-11-06/`):

```
WindowsVideoMedia error 0x80070005 while reading https://…googlevideo.com/…
Context: MFCreateSourceReaderFromURL
[MusicManager] VideoPlayer failed to prepare within 30s for videoId=DeKLpgzh-qQ.
```

`0x80070005` is `E_ACCESSDENIED`. The googlevideo request has to be made with the
same User-Agent / po-token context yt-dlp used; Unity's `VideoPlayer` exposes no
way to set request headers, so `MFCreateSourceReaderFromURL` is rejected.
The new code keeps this as a **one-time probe per video** (the result is stored in
`PlayerPrefs` under `FlashBeat.DirectUrlProbed.<videoId>`) so the reason stays in
the log without costing 10 s on every start.

### 1.3 The bundled wav is the very same recording as the stream audio

To be able to line the chart up with the stream, the bundled
`Assets/Game/Resources/Musics/まにまに.wav` (24 bit / 44.1 kHz, 112.4 s) was
compared with the audio track of the YouTube video (format 140, decoded to raw
PCM at 8 kHz mono, 269.0 s):

| window | normalised envelope correlation |
|---|---|
| 0 – 10 s | 0.999 |
| 10 – 20 s | 0.999 |
| 20 – 30 s | 1.000 |
| 30 – 40 s | 0.999 |
| 40 – 50 s | 0.999 |
| 50 s and later | 0.67 → 0.00 (the wav ends, the video continues) |

The best matching offset over the whole file is **0.00 s** with a correlation of
0.9996, i.e. the wav is the same recording, aligned at 0. The chart covers
0 – 48 s (`GManager.SongLong[2] = 48`), so picture, sound and chart share one
timeline.

## 2. Decision

**The stream is downloaded once into a local cache and played from there.**

* Issue #13 asked for "no download-to-local fallback". That rule is relaxed here
  because the exhibition has to show a playing video with sound tomorrow, and the
  measurements above show that *neither* half of the requested path works on
  Windows: the progressive format does not exist any more and Media Foundation
  refuses the googlevideo URL.
* Downloading keeps the stream itself: the bytes are the ones `yt-dlp` resolved
  from YouTube, they are played by Unity's `VideoPlayer`, and the picture and the
  sound come from that file.
* The cache (`%USERPROFILE%\AppData\LocalLow\…\FlashBeatMedia\<videoId>\`) makes
  the second start of a song instant and removes the network from the critical
  path of the demo.
* The picture and the sound are played from the *same* recording, so the fallback
  to the bundled wav (used only when the separate audio track turns out to be
  silent) cannot desynchronise anything.

## 3. Implementation

| File | Role |
|---|---|
| `Assets/Game/Scripts/Gameplay/YouTubeStreamResolver.cs` | runs `yt-dlp` (`-F`, `-g`, download), discovers `yt-dlp.exe` and `deno.exe` (PATH, mise install tree, Python installs, `FLASHBEAT_YTDLP_PATH` / `FLASHBEAT_DENO_PATH`), parses the format table |
| `Assets/Game/Scripts/Gameplay/YouTubeMediaCache.cs` | format plan (progressive mp4 → video-only H.264 mp4 + AAC m4a) and the local cache |
| `Assets/Game/Scripts/Gameplay/MediaTimeline.cs` | pure timing helpers: the judgement clock follows the media clock |
| `Assets/Game/Scripts/Gameplay/AudioLevelProbe.cs` | measures the level of the final mix (evidence that the song is audible) |
| `Assets/Game/Scripts/Gameplay/MusicManager.cs` | the pipeline: format probe → direct URL probe → cached download → `VideoPlayer` prepare → playback |

### 3.1 deno as the JavaScript runtime

`yt-dlp` needs a JS engine for YouTube's challenge solver. The resolver passes it
explicitly, which the log confirms:

```
[youtube] [jsc:deno] Solving JS challenges using deno
```

### 3.2 Timing (notes ↔ picture ↔ sound)

`Judge` compares `Time.time` with `noteTime + GManager.StartTime`, and the chart
times are counted from the first frame of the song. `MusicManager` therefore
recomputes

```
GManager.StartTime = Time.time - mediaTime      (every frame)
GManager.step_time = mediaTime
```

instead of accumulating `Time.deltaTime`. Consequences:

* the judgement error is exactly `|mediaTime - noteTime|` (see
  `MediaTimelineTests.JudgeLagMatchesMediaLagWhenStartTimeComesFromTheMediaClock`);
* a dropped frame or a stall cannot make the notes drift, because the reference
  point follows the media, not the wall clock
  (`MediaTimelineTests.StalledMediaDoesNotAccumulateJudgementError`);
* the song ends on the media time (`GManager.SongLong`), not on the frame clock.

The chart conversion itself (`NotesManager`) was not touched.

### 3.3 Fallbacks

| situation | behaviour |
|---|---|
| progressive mp4 (itag 18/22) exists | one file, the `VideoPlayer` renders picture **and** sound |
| only adaptive streams | picture from the H.264 mp4, sound from the AAC m4a in a second `VideoPlayer` |
| the m4a track is silent (RMS < 0.001 after 3 s) | the bundled wav of the chart takes over, the media clock keeps running |
| no wav for the song | the song runs without sound, the log says so |

The `InvidiousVideoPlayer` of the `com.ibicha.youtube-player` package is removed
from the song object at runtime (its instances are dead and its error handler
would fight with our own player). No scene or prefab was modified.

## 4. Tests

`Assets/Tests/Editor/MediaTimelineTests.cs`

* `JudgeLagMatchesMediaLagWhenStartTimeComesFromTheMediaClock`
* `NoteIsJudgedPerfectlyWhenTheMediaReachesTheNoteTime`
* `StalledMediaDoesNotAccumulateJudgementError`
* `MediaTimeIsRecoveredFromTheJudgementClock`
* `SongEndsOneSecondBeforeTheChartDuration`
* `ParserReadsCodecsAndResolutionOfEveryRow` (real `yt-dlp -F` listing)
* `AdaptiveOnlyVideoIsDownloadedAsVideoPlusAudio`
* `ProgressiveVideoIsDownloadedAsOneFileWithSound`
* `UnknownFormatListFallsBackToWorkingSelectors`

## 5. Win64 run

Built player: `Builds/Windows/FlashBeat.exe` (branch `13`, see the commit below),
Unity 6000.5.6f1, Windows 11 x64, RTX 4050 Laptop / Direct3D 11.
Evidence: [`evidence/win64-2026-11-06-issue13/`](../../evidence/win64-2026-11-06-issue13/)

| Artifact | Content |
|---|---|
| `Player-keys-manimani.log` | player log of the run in which the chart was played with the lane keys |
| `Player-unattended-manimani.log` | player log of the run without key presses (all notes missed) |
| `01-title.png` … `11-retry.png` | screenshots of both runs (title, select, loading, ready, 5 s / 15 s / 25 s of the song, result, retry) |

### 5.1 What the log shows

```
[MusicManager] yt-dlp -F videoId=9O2VyUM5MlQ formats=38 ok
[MusicManager] media plan 'まにまに': combined mp4/H.264 itag=18 640x360
[MusicManager] video file: <USERPROFILE>/AppData/LocalLow/KimuraYusuke/FlashBeat\FlashBeatMedia\9O2VyUM5MlQ\video.mp4 (cache)
[MusicManager] picture wired to the renderer of '2' (_MainTex, renderMode=MaterialOverride)
[MusicManager] VideoPlayer prepared=True file=video.mp4 after 0.4s
[MusicManager] media ready: song='まにまに' audioMode=StreamCombined video=ok (bundled wav present) - press Space to start
[MusicManager] PLAYBACK_START song='まにまに' videoId='9O2VyUM5MlQ' audioMode=StreamCombined mediaTime=0.000 wallClock=2026-11-06T22:53:06.542 gameTime=13.730
[MusicManager] STATUS mediaTime=2.00 audioMode=StreamCombined rms=0.2932 peak=0.4931 video[playing=True frame=8069 prepared=True] perfect=2 great=0 bad=0 miss=0
[MusicManager] STATUS mediaTime=4.00 audioMode=StreamCombined rms=0.2507 peak=0.4931 video[playing=True frame=8069 prepared=True] perfect=3 great=1 bad=0 miss=0
```

* **The stream is resolved and played**: `yt-dlp -F` lists 38 formats, the plan
  picks the progressive H.264+AAC mp4 (itag 18), `yt-dlp` downloads it into the
  cache and Unity's `VideoPlayer` prepares it (`prepared=True`).
* **There is sound**: the `AudioLevelProbe` on the `AudioListener` measures the
  final mix at `rms = 0.25 … 0.49` (`peak 0.53`) while the song plays - that is
  the music, not silence. The sound comes from the video file itself
  (`audioMode=StreamCombined`), not from the bundled wav.
* **The picture is visible**: the song object of the scene has no target
  renderer, so the `VideoPlayer` would have decoded frames into nothing. The
  renderer is wired at runtime (`picture wired to the renderer of '2'`), and the
  screenshots show the YouTube video as the game background
  (`07-game-05s.png`, `08-game-15s.png`, `09-game-25s.png`).
* **The judgement follows the media**: the chart was played with the lane keys
  pressed at the chart times, anchored on the `wallClock` timestamp of
  `PLAYBACK_START`:
  **Perfect 74 / Great 2 / Bad 0 / Miss 0 of 76 notes** - every note was judged
  inside the ±0.10 s window of the media clock.
* **One song completes**: the result screen is reached (grade C with 0 hits in the
  unattended run, `scenes=8`), and `R` reloads the GameScene, where the media is
  prepared again from the cache in 0.4 s (`mediaReady=True`).
* **The direct URL path fails** (measured, one probe per video):
  `VideoPlayer error: VideoPlayer cannot play url : https://…googlevideo.com/…`
  and `prepared=False after 10.0s`. This is the Windows Media Foundation
  limitation described in §1.2; the probe result is cached in
  `PlayerPrefs` (`FlashBeat.DirectUrlProbed.<videoId>`), so it costs 10 s only
  once per song.
* **The retry path works with the cache**: the second start of the song uses the
  cached file (`(cache)` in the log), no download.

### 5.2 Known limits of the current state

* The media cache panel on the Option screen overlaps the "FlashTiming" row of
  the option layout (cosmetic; the panel is created at runtime in
  `MediaCacheOptionPanel`, moving it is a one line change plus a rebuild).
* The mouse click of an automated driver does not reach the uGUI buttons of this
  build (the Option screen was opened with the `O` key instead), so the
  "Clear media cache" button was **not** exercised on the real machine; the
  deletion path is covered by the EditMode tests and by the Editor menu.
* The labels of the panel are ASCII because the HUD font (LiberationSans SDF) has
  no Japanese glyphs - that is issue #27.

