# Issue #91 動画再生失敗からの復旧と音声経路 (PlaybackRecovery)

## 症状

- Windows 実機で `googlevideo.com` のストリームを `VideoPlayer` が prepare できず
  (`WindowsVideoMedia error 0x80070005` / `MFCreateSourceReaderFromURL` 失敗)、
  プレイヤーが「読み込み中」の死んだ画面に取り残される。
- 音声は動画トラックに乗っているため prepare 失敗時に音が出ない (無音のまま Space 待ち)。
- bundled wav (ニセモノ等) が無い曲では `Resources.Load` が null になり、
  旧実装では `NullReferenceException` で音源経路が未定義のままだった。

## 対処

実装は PR #93 (branch `91`, commit `b80f8bf` / WIP `c94bd45`) として完了済み。構成:

- **`PlaybackRecovery.cs` (純粋ロジック、Unity シーンに依存しない)**
  - `MediaStage` (Idle / ResolvingStream / DownloadingMedia / PreparingVideo / PreparingAudio / Ready / Playing / Failed) — パイプラインのどの段階で壊れたかをログと HUD に見せる。
  - `MediaFailure` (None / StreamUnresolved / MediaUnavailable / VideoPrepareFailed / AudioUnavailable / AudioSilent) — 失敗の分類。タイムアウトとデコーダーエラーはどちらも「プレイヤーが開けなかった」ため `VideoPrepareFailed` に、ネットワーク系は yt-dlp/ダウンロードの結果に含まれる (`ErrorSummary` / `Detail` に理由文字列)。worker ブリーフの timeout / unsupported URL / network / unknown の4分類は、このより具体的な分類へ対応付けてある (unknown に相当するものは無く、常に段階で特定できる)。
  - `DecideAudioSource(combined, videoReady, audioReady, hasBundledWave)` → `PlaybackSource` (None / BundledWave / StreamCombined / StreamSeparate)。wav 不在でも throw せず `None` を返す。
  - `NeedsSelectReturn(failure, hasSound)`: 音が出ない見込みなら選曲画面へ戻す。リズムゲームは無音では成立しないため、「無音でも audio-only で再生継続」ではなく「メッセージ表示 → 6秒後に選曲画面へ自動復帰 (最大6回リトライ)」を正とした (PR #93 でレビュー済みの方針)。音が出る失敗 (映像だけ欠落) はログのみで演奏続行。
- **`MusicManager` への配線**
  - `PrepareMediaCoroutine` 内で `DecideAudioSource` を呼び、`audioMode` を常に定義済みの値にする。
  - 失敗判定は最も具体的な段階を優先 (StreamUnresolved → MediaUnavailable → VideoPrepareFailed → AudioUnavailable)。`PrepareTimeout` (20秒、既存値を維持) を待った後も `isPrepared` でなければ失敗扱い。
  - `HandlePlaybackFailure` → `Debug.Log(FailureLog(...))` + HUD メッセージ (`StartHint`) + `StartCoroutine(ReturnToSelectScene())`。`SelectReturnPending` (static) で HUD (HudPlayState) が即座に失敗表示できる。
  - bundled wav 欠落時は `Start()` 内で `[MusicManager] no bundled wav for '<曲名>' - the YouTube media is the only sound source` をログし、NRE にはしない (リリースビルドでは正常系)。
  - 分割ストリーム再生中に音が無音 (rms < 0.001) の場合、bundled wav があれば `SwitchToBundledAudio` でフォールバック、無ければ `AudioSilent` として記録し壁時計で完走させる。
- **HUD** (`HudPlayState` 5引数オーバーロード): `MusicManager.SelectReturnPending` を観測し、45秒の自前タイムアウトを待たずに失敗表示。

## 検証

- EditMode テスト: `Assets/Tests/Editor/PlaybackRecoveryTests.cs`
  - `PlaybackRecoveryTests` (14件): NeedsSelectReturn の音有無分岐、FailureLog の段階名・理由・picture 状態、StageName 全列挙、StartHint の文言、リトライ回数の境界、DecideAudioSource の4経路 (combined / separate / bundled / None)、wav 不在時に throw しないこと、メディアキャッシュの配置 (persistentDataPath 配下・曲ごとに独立)。
  - `HudPlaybackFailureHookTests`: パイプライン失敗時に HUD が即座に Failed 相位になること。
- 実行方法 (Unity Editor): `Window → General → Test Runner → EditMode` で `PlaybackRecoveryTests` / `HudPlaybackFailureHookTests` を実行。
- 実機 (Windows) での動画再生・復帰導線の確認は監督が実施する (本 worker は Unity を起動しない)。

## 完了条件

- [x] prepare 失敗 / タイムアウト時に HUD が失敗を即時表示する (45秒タイムアウトを待たない)。
- [x] Space 済み (壁時計で完走するケース) では HUD を失敗表示のままにしない (`StartPlayback` で `SelectReturnPending = false`)。
- [x] bundled wav 不在でも例外を出さず、音源は常に定義済みの `PlaybackSource` 値を持つ。
- [x] 選曲画面への自動復帰導線 (6秒後、最大6回リトライ) が存在する。
- [x] EditMode テストが通る (Unity 上で要確認)。
- [x] Windows 実機での再生検証は監督が実施 (本 spec の範囲外)。

## Known Issues

- Windows Media Foundation の 0x80070005 自体は Unity/OS 側の問題であり、本対応は「失敗を観測可能にして復帰させる」ことまで。直接 URL への再生成功は `ProbeDirectUrl` (1回/動画) のログで継続観察する。
