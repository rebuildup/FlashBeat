# Issue #91

動画の prepare 失敗 / タイムアウト (WindowsVideoMedia error 0x80070005) でプレイヤーが死んだ画面に取り残されないようにする。HUD (HudPlayState/HudPlayStatus) に失敗を即時表示し、SelectScene へ戻れる導線を維持する。bundled wav 不在 (ニセモノ) でも例外を出さず、音源は必ず定義された値 (PlaybackSource) を持つ。Refs #41。

## 受け入れ条件

- パイプラインが再生不能と判定した瞬間に HUD が失敗を表示する (45s のタイムアウトを待たない)。
- Space 済み (壁時計で完走するケース) では HUD を非表示のまま維持。
- `PlaybackRecovery.DecideAudioSource` で音源決定が純粋関数になり、wav 不在時は `PlaybackSource.None` (throw しない)。
- EditMode テストが通ること (実機の再生検証は監督が行う)。
