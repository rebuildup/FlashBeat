# 0.1.26 Win64 実機受入証跡 (2026-11-13)

Issue #233 / #242 / #13 / #17 / #424 系の Win64 実機確認。展示対応中止後の
通常受入として、現行リリース `0.1.26` (buildSha `fe2324b99bc61764093b8d0a49ba20f46bd58544`)
を展示機 (`device-windows`, Windows 11 / UUU\basic) で実行した。

- 成果物: `FlashBeat-0.1.26-win64-fe2324b9.zip`
  (Release `v0.1.26-win64-fe2324b9`、展開先 `C:\Users\rebui\Desktop\FlashBeat-0.1.26-win64-fe2324b9\`)
- 展開後 `version.json`: `0.1.26` / `buildSha=fe2324b9…` / `mediaPolicy=song-free` — 実機で確認
- ZIP SHA-256 `670e324b…f5799` はリリース側 digest と一致 (ダウンロードは実機側で直接実施)
- 起動は Interactive ログオンのスケジュールタスク (`FBVerify0126` → `C:\Users\rebui\fb-verify-run.ps1`)
  経由。SSH session 0 からの直接起動は Unity が即終了するため (0.1.24 証跡と同じ方式)。
- 解像度: 主画面 2560×1440 ネイティブ (`v6screens` result の `resolution=2560x1440`)

## ハーネス結果

| ハーネス | 結果 | 内容 |
|---|---|---|
| `-autotest v6songend -fbperf` | **pass=32 fail=0** | ツイッターランド完走。Select→Ready→Game→Result、judged=130==chartNotes==settled、score=992,307、songEndState=Completed、forcedByDeadline=False。`songend/` |
| `-autotest v6screens` | **pass=46 fail=0** | 全 15 画面・状態の監査 (symbols=63)。Title/Select(通常・空・設定・履歴・ランキング)/Option(4タブ)/Result(登録モーダル含む)/Game(HUD・Pause・Loading)。`v6screens/` |
| `-autotest v6interact` | **pass=45 fail=2** | 下記「既知の失敗」参照。`v6interact/` |
| `-autotest v6` | **pass=29 fail=0** | v6 結合フロー。`v6/` |

## v6interact の fail=2 について

- `the register field opens automatically at 2s` / `the reveal completes on its own`
- **原因はハーネス側の期待値の陳腐化**。#425 で登録モーダルの自動表示を
  2 秒 → 3 秒 (`V6ResultRevealLogic.RegisterStart = 3.00f`、全体 `TotalSeconds = 3.18f`)
  へ遅延させたが、`AutoTestV6Interact` は 1.9–2.4 秒・2.6 秒窓のままだった。
- 本 PR でハーネスを `V6ResultRevealLogic` の定数参照へ修正済み
  (`Assets/Game/Scripts/Common/AutoTestV6Interact.cs`)。プロダクトコードの退行ではない。
- 修正後の再実行は次回ビルドで確認する (現行 0.1.26 ビルドには修正が入っていない)。

## この証跡がカバーする受入

- #233 Done 条件「実 Windows で 1 曲が開始し、映像とノーツが表示され、キーで判定され、
  スコアが動き、結果へ進める」: `v6songend` pass=32 fail=0 (現行 SHA `fe2324b9`)。
- #242 受入条件の Win64 側: 全画面到達・重なりなし (`v6screens` 46/0)、
  実曲 1 曲完走 (`v6songend` 32/0)、操作系 (`v6interact` / `v6`)。
- #13 (S1 YouTube 1 曲ストリーミング): 同上の完走が YouTube メディア経路
  (`merged.mp4` キャッシュ経由) で成立。
- #17 (Cloud Catalog): Select は `https://flashbeat.rebuildup.dev/api/v1/catalog`
  (catalogVersion=38, 28 曲) を取得して動作 (Player.log の catalog 取得は
  `v6screens`/`v6songend` の Select 表示と整合)。

## 未実施 (引き続き別途必要なもの)

- 人間による聴取・実キー操作でのプレイ感 (ハーネスはキー注入)
- Result → Retry の実遷移、物理 FHD モニターでの引数なし全画面起動
- オンラインスコア登録の実 API 書き込み (#134 / #21 / #73 系)
- 1920×1080 / 1024×768 の別解像度プロファイル (#88 / #96)
- 未キャッシュ経路の #395 stall 再計測 (本 run はキャッシュ済み経路)
