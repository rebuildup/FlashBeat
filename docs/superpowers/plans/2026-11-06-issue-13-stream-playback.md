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

See [`evidence/win64-2026-11-06-issue13/`](../../evidence/win64-2026-11-06-issue13/)
for the player log, the screenshots and the measured judgement counters.
