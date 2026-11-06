# Media rights audit — what third party audio/video ships with FlashBeat

Captured: 2026-11-06 (issue #13, branch `13`)
Scope: the distributed Win64 build, `Assets/Game/Resources/`, `Assets/Resources/`,
`Assets/StreamingAssets/` and the repository.

**Nothing was deleted.** This is an inventory plus exclusion proposals; removing
the files is a separate decision (see "Exclusion proposals").

## 1. The media cache of this PR (new)

| Location | Content | In the distribution? |
|---|---|---|
| `%LOCALAPPDATA%\KimuraYusuke\FlashBeat\FlashBeatMedia\<videoId>\` | `video.mp4` / `audio.m4a` downloaded by `yt-dlp` at runtime | **No** — the cache is outside the repository, outside `Resources` and outside `StreamingAssets` |

Verified after the Win64 run:

```
%LOCALAPPDATA%\KimuraYusuke\FlashBeat\FlashBeatMedia\9O2VyUM5MlQ\video.mp4
%LOCALAPPDATA%\KimuraYusuke\FlashBeat\FlashBeatMedia\9O2VyUM5MlQ\audio.m4a
```

`.gitignore` now names the cache folders explicitly
(`/Assets/StreamingAssets/FlashBeatMedia/`, `/Assets/Game/Resources/FlashBeatMedia/`,
`/Assets/Resources/FlashBeatMedia/`, `/Builds/StreamingAssets/FlashBeatMedia/`).
`Assets/StreamingAssets/` was already ignored as a whole.

## 2. Third party media that already ships with the build (pre-existing)

### 2.1 Song audio — `Assets/Game/Resources/Musics/`

15 WAV files plus one MP3, **283,865,881 bytes (270.7 MiB)** in total. They are
the audio of third party songs (LeaF, t+pazolite, r-906, STEAKA, Azari, …) and they
are loaded with `Resources.Load("Musics/" + songName)` in `MusicManager.Start()`.

| File | Bytes | Song |
|---|---|---|
| `終焉逃避行.wav` | 47,646,782 | 終焉逃避行 (STEAKA) |
| `まにまに.wav` | 29,746,808 | まにまに (r-906) |
| `ロストアンブレラ.wav` | 23,816,108 | ロストアンブレラ (稲葉曇) |
| `エウタナシア.wav` | 23,816,108 | エウタナシア (ど～ぱみん) |
| `Dogbite.wav` | 21,475,406 | Dogbite (t+pazolite) |
| `Who.wav` | 18,110,678 | Who (Azari) |
| `セルフィー.wav` | 15,362,110 | セルフィー (たぴぼ!!) |
| `狂喜蘭舞.wav` | 15,362,108 | 狂喜蘭舞 (LeaF) |
| `Psyched.wav` | 15,346,878 | Psyched (t+pazolite) |
| `Chartreuse.wav` | 14,112,078 | Chartreuse (t+pazolite) |
| `チュートリアル.wav` | 13,442,108 | チュートリアル (LeaF) |
| `4th smile.wav` | 13,167,826 | 4th smile (LeaF) |
| `ツイッターランド.wav` | 11,965,016 | ツイッターランド (STEAKA) |
| `Poison.wav` | 10,562,108 | Poison (LeaF) |
| `Calamity.wav` | 9,909,310 | Calamity (LeaF) |
| `カーソル移動4.mp3` | 24,449 | UI 効果音 |

### 2.2 Song video — `Assets/Game/Resources/Videos/UnityYoutube/`

25 MP4 files, **1,144,714,929 bytes (1.07 GiB)** — these are the original YouTube
videos of the songs (downloaded by the NoteEditor import), kept in `Resources`.

`ビビビビ.mp4` (120,442,983), `灰Φ倶楽部.mp4` (102,532,499), `セルフィ.mp4`
(82,167,599), `一龠.mp4` (73,530,765), `Major.mp4` (70,452,093),
`エウタナシア.mp4` (59,521,319), `GURU.mp4` (55,289,435), `Who_.mp4`
(54,897,298), `混沌ブギ.mp4` (40,011,536), `Chartreuse Green.mp4` (37,669,959),
`Psyched Fevereiro.mp4` (37,631,057), `春嵐.mp4` (37,377,439),
`Poison AND÷OR Affection.mp4` (36,759,215), `ツイッターランド.mp4`
(36,507,546), `パノプティコン.mp4` (35,490,665), `Calamity Fortune.mp4`
(34,031,150), `Evanescent.mp4` (32,572,116), `オーバーライド.mp4` (32,555,650),
`狂喜蘭舞.mp4` (31,825,879), `Dogbite.mp4` (30,674,749),
`テレキャスタービーボーイ.mp4` (29,657,574), `阿吽のビーツ.mp4` (19,588,114),
`イガク.mp4` (15,427,992), `終焉逃避行.mp4` (8,370,576), `まにまに.mp4`
(6,632,203), `ロストアンブレラ.mp4` (5,078,388), `4th smile.mp4` (3,125,319).

### 2.3 Game videos — `Assets/Game/Resources/Videos/`

`背景アニメーションタイトル.mp4` (139,537,128), `コンプラに配慮した人マニア.mp4`
(22,337,377), `背景アニメーション3.mp4` (16,741,890), `背景アニメーション結果.mp4`
(11,979,767), `loading.mp4` (8,742,742), `オープニング.mp4` (5,850,138),
`opening1.mp4` (859,732) — **205,188,774 bytes (195.7 MiB)**.

### 2.4 Result: the Win64 build ships ~1.4 GiB of third party media

```
Builds/Windows/FlashBeat_Data/resources.resource   1,399,081,540 bytes
```

`Resources` content is packed into that single file, so the audio and the video
above are part of the distributed player (build total ≈ 1.5 GiB).

`Assets/StreamingAssets/` is **empty** and git-ignored — nothing ships from there.
`Assets/Resources/` only holds chart JSON files (no media).

## 3. Exclusion proposals (not applied)

1. **Song videos in `Resources/Videos/UnityYoutube/` (1.07 GiB).**
   They duplicate what the runtime pipeline of this PR fetches: the game only
   needs the YouTube video id (`GManager.SongURL`) plus the `yt-dlp` cache.
   Proposal: move them out of `Resources` (e.g. to a non-shipped folder that only
   the NoteEditor import uses), or delete them after the import; the runtime
   cache becomes the single source of the picture. Saves ~1.07 GiB of build size
   and removes the clearest rights risk.
2. **Song audio in `Resources/Musics/` (270.7 MiB).**
   Used today as the fallback when the streamed audio track turns out to be
   silent. Proposal: keep it for the exhibition (it guarantees sound) but treat it
   as a temporary item — once the stream audio is confirmed on the exhibition
   machine, the wavs can be replaced by the cached stream (also downloadable at
   build time via `MediaCacheTools.PrecacheAllSongs`) and removed from
   `Resources`. Requires a decision on offline playback.
3. **Game videos in `Resources/Videos/` (195.7 MiB).**
   `背景アニメーションタイトル.mp4` alone is 139 MB. Proposal: re-encode them with
   a lower bit rate (or move them to a compressed format) — this is a size
   question, not a rights question.
4. **Clearance.** Items 1 and 2 are third party recordings of songs by LeaF,
   t+pazolite, r-906, STEAKA, Azari, ど～ぱみん, たぴぼ!!, 稲葉曇 and others. A
   public exhibition needs either a licence from the right holders or a
   "no redistribution" arrangement. This audit cannot decide that - it only
   reports what is in the build.

## 4. How to reproduce

```powershell
# repository
find Assets/Game/Resources/Musics -type f ! -name '*.meta' -printf '%s\t%p\n' | sort -rn
find Assets/Game/Resources/Videos -type f ! -name '*.meta' -printf '%s\t%p\n' | sort -rn

# build output (the packed Resources)
Get-Item Builds\Windows\FlashBeat_Data\resources.resource | Select-Object Length

# runtime cache (not part of the distribution)
Get-ChildItem "$env:LOCALAPPDATA\KimuraYusuke\FlashBeat\FlashBeatMedia" -Recurse
```
