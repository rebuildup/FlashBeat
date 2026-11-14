# リリース承認チェックリスト — FlashBeat Win64 展示ビルド（#22）

対象 Issue: **#22**。証跡索引: [`../evidence/index.md`](../evidence/index.md) /
運用手順: [`festival-runbook.md`](festival-runbook.md)。

このチェックリストは **「そのビルドを展示にreleased してよいか」** を判断するためのものです。
**1 項目でも確認できていないものは「未証明」と書き、green にしません。**

## A. 前提と権限の境界

- [ ] 承認対象は **PR 単位**で特定されている（branch / commit SHA / PR 番号）。
- [ ] 承認権限は統合・リリース担当の手元にある（**#22 は PR の readiness を確認するだけで、merge しない**）。
- [ ] `release-0-1-0` への取り込みは **レビュー済み PR のみ**。`main`（本番）と Cloudflare の公開は
      **別の承認**。片方の承認を他方の承認に読み替えない。
- [ ] backend API は **v1 のまま**（既存バイナリ互換）。承認記録に API 破壊変更があれば本チェックリストを通らない。
- [ ] 承認対象のビルドに **Unity Editor を起動し直していない**（Win の Unity 所有者は 1 プロセスだけ。
      #13 と並行してビルドしない）。

## B. 成果物チェック（実在 + SHA）

- [ ] 成果物フォルダが **外部（Git の外）** にあり、`FlashBeat.exe` / `FlashBeat_Data` / `UnityPlayer.dll` /
      `MonoBleedingEdge` が入っている。
- [ ] フォルダ fingerprint（`release/check/stage-manifest.json` 記載のアルゴリズム）が manifest と一致。
- [ ] `FlashBeat.exe` の SHA-256 が manifest と一致。
- [ ] 既存成果物を **上書き・削除していない**（新しい成果物は別名フォルダ）。
- [ ] **配布 ZIP に第三者楽曲メディアが含まれない**ことを `tools/release/audit-release-media.py` で確認した
      （`FlashBeat_Data/resources.resource` の非圧縮サイズとルーズなメディア 0 件、
      `flashbeat-media-policy.json` の有無）。結果の記録は
      [`release-media-audit.md`](release-media-audit.md)。
- [ ] 検証コマンドの結果が記録に残っている。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/verify-stage.ps1 -Stage <S0|S1|...>
```

## C. プレイ証拠（実機・実物）

- [ ] **実物の Win64 ビルド**で再生した記録がある（Editor での動作確認は不可）。
- [ ] 全画面表示のゲート: `release/check/drive-gameplay.ps1 -RequireFullscreenCoverage` の `run-summary.json` で
      `displayCoverage >= 0.99`（Issue #88 の受入基準 / `specs/088-fullscreen-ui/tasks.md` T1）。
      0.99 未満なら FAIL として扱います。
- [ ] 選曲 → 譜面 → リザルト → リトライが 1 曲ぶん通る（スクリーンショットあり）。
- [ ] 判定数が **譜面のノーツ数と一致**（例: まにまに = 76 / ロストアンブレラ = 268）。
- [ ] YouTube ストリームの再生が観測できた（`[MusicManager]` の成功行 + 音）— **S1 の核心**。
- [ ] 起動遅延（タイトル画面までの実測秒）、音画同期の誤差が記録に残っている。
- [ ] 使用した譜面の identity（曲名 / BPM / ノーツ数）と **chartVersion**（API 導入後は `chartVersion`、
      導入前は「ローカル chart リソース」と明記）。
- [ ] 使用した Cloudflare URL（存在する場合）。
- [ ] スクリーンショットとログが **外部 evidence ディレクトリ** と `docs/evidence/index.md` に索引されている。

## D. 1280×720 UX スモーク（運営 runbook §4 と同じ項目）

- [ ] 起動 → 選曲 → ゲーム → リザルト → リトライが 1280×720 で通る。
- [ ] 日本語グリフ（曲名・アーティスト名・歌詞）が `□` にならない。
- [ ] 入力マッピング: レーンキー、選曲キー（`J`/`K`/`L`/`;`/`→`、`F`/`D`/`S`/`A`/`←`）、`Space`、`R`、`Esc` が反応する。
- [ ] 音量: 音が鳴り、展示会場で聞き取り可能な音量で、極端に大きくない。
- [ ] 音画同期: ノーツ落下と曲がずれていない。
- [ ] ネットワーク断: 固まらず、エラーがログ/画面に残り、別の曲へ進める。
- [ ] フリーズ: 起動直後 / 選曲中 / 曲中のいずれでも 30 秒以上の停止がない。
- [ ] 1280×720 で文字が切れていない。

**「ソース検査」と「実プレイ証拠」は混ぜない。** コードを読んだ結果（例: `Judge.LaneKeys` の定義）は
*ソース検査*であり、**実プレイで確認した項目とは別の欄**に書く。S1 の音源のように、
コード上は直っていても未実機確認のものは **未証明** のまま扱う。

## E. 証跡の秘密情報チェック

- [ ] プレイヤーログに署名付き一時 URL（`googlevideo.com`）が残っていない。
- [ ] video id / API token / Cloudflare 資格情報が残っていない。
- [ ] 成果物・ZIP・Release に秘密情報が混ざっていない。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/redact-log.ps1 -InPath '<log>' -Check
```

## F. 承認記録

| 日付 | ステージ | 成果物 fingerprint (SHA-256) | PR | 実プレイ証拠 | 1280×720 UX | 承認者 | 判定 |
|---|---|---|---|---|---|---|---|
| 2026-11-08 | Release 0.1.14 (`v0.1.14-win64-617c2b6`) | `3ecf9d8ea65a435b4e99b70a0ed8691859968bc2c06da6e56b4366750e124171`（ビルド元 commit `617c2b6`，song-free） | #170 / #168 | **未証明**（Windows 展示機での実プレイ未実施。バッチ検証のみ: EditMode 407/407 成功、`FlatRenderCheck` `audit: lights=0 casters=0 receivers=0` / `failures 0`、パッケージング検査 pass） | 未実施 | — | **部分承認**（#22 チェックリスト §C/§D の実機項目が未確認のため「未証明」。映像・音声の実機確認はオーナー側の実施項目） |
| 2026-11-07 | **0.1.11 一括リリース** (ゲーム Win64 + Web Studio, 展示的第 3 世代の成果物は**未上書き**) | `58a15d13eaea39a0aa5f5ea6384d46a14796ca368aaa97fcb3487081731dd3fe`（ZIP `FlashBeat-0.1.11-win64-5f94d12.zip` / 265,312,659 bytes / 224 entries / song-free, code state `5f94d12`, tag `v0.1.11-win64-5f94d12`） | #143 / #144 / #159 / #160 | **Mac ネイティブビルドで起動確認**（Title 画面描画・例外 0・スクリーンショット取得済）。**Windows 実機のプレイ（選曲→演奏→Result→リトライ）は未実施**（指示により展示機では自動テストを実施していない） | **Web Studio**: 実ブラウザ (Chromium) で 1280×720 / 1440×900 / 390×844 / 1440×900@200% のスクリーンショット 40 枚を検証（横溢れ 0 / コンソールエラー 0 / 12px 未満の文字 0）。**ゲームの 1280×720 実機は未実施** | —（owner 未承認） | **未承認**（Windows 実機プレイ証拠が未実施のため。ZIP は公開済み・展示への適用は承認後） |
| 2026-11-07 | **展示 第 3 世代（現在の展示用）** `FlashBeatExhibit_s2\` | `2907102fcab0142e8d93b7bfb025d74a98cc274fe6ebbac55843cdca14cbedd9`（#13 修正込み統合、code state `b77b63c`） | #13 / #27 / #22 | **実機で完走**: Title → Select → まにまに → 判定 76（譜面全部、実入力で Perfect 1 / Bad 2）→ Result → `R` リトライ / **映像あり**（`VideoPlayer prepared=True`、`video[playing=True]`）/ **音あり**（`rms=0.44–0.56`、ただし音源は同梱 wav = `audioMode=Bundled`。**ストリーム音声は未証明**） | windowed 1280×720 で実機確認（実機 fullscreen は未検証） | #22（Draft PR） | **承認**（展示用。ストリーム音声のみ未証明） |
| 2026-11-06 | 展示 第 2 世代 `FlashBeatExhibit_s1\` | `cbae8f677eb60dfeaa6ca6676121d4266fa99583f2b76bfeacacebcbd0924758`（#13 + #27 統合、code state `f53575c`） | #13 / #27 / #22 | **実機で完走**: Title → Select → まにまに → 判定 76（譜面全部）→ Result → `R` リトライ / **音あり**（`rms=0.30 peak=0.52`）/ **映像は未証明**（VideoPlayer が拡張子なしファイルを認識できず → 同梱 wav フォールバック） | windowed 1280×720 で実機確認（実機 fullscreen は未検証） | #22（Draft PR） | **部分承認**（予備。映像・ストリーム音声は未承認） |
| 2026-11-06 | 展示 第 1 世代 `FlashBeatExhibit\` | `da338a6a8a73b23dbf6e0a7d26e498003e2f773bdb574323c416d9d17f6c563b`（`743c88d`、#27 + #35 ZIP） | #34 / #35 | 実機で Title → Select → 判定 76 → Result → `R` リトライ / **音は出ない** | windowed 1280×720 で実機確認 | #22（Draft PR） | **部分承認**（予備。音は未承認） |
| 2026-11-06 | S0 | `7ef323adcf1855ea1f192d0f542c099ec6fdfe52540bdf7737b55014f2ba4d53` | #26 | 譜面→リザルト→リトライ **proven** / ストリーム・音 **未証明** | 2026-11-06 実施（windowed 1280×720、実機 fullscreen は未証明） | #22（Draft PR） | **部分承認**（表示系・譜面のみ。音は未承認） |
| — | S0-FONT | `e07ccba2cc406306254a50e0bfb5f43a197013aa3820fe907d28da60ffe1b242` | #34 | グリフ + 譜面 **proven** / ストリーム **未証明** | 未実施 | — | **未承認**（#35 が Release 化済み = 展示 第 1 世代） |
| — | S1 | `b8c0acde7a318bc3f8d7929025743638042a3b0e2dec7836e418a2d80c60830f` | #31 | **未証明**（#22 が統合ビルドで「音あり」まで確認。映像と #13 単体の stream は未証明） | 統合ビルドで windowed 1280×720 を確認 | #22（Draft PR） | **部分承認**（統合ビルド経由のみ） |
| — | S2 | — | — | **未証明** | — | — | **未承認** |
| — | S3 | — | — | **未証明** | — | — | **未承認** |
| — | S4 | — | — | **未証明** | — | — | **未承認** |

判定欄の意味: **承認**（展示に使ってよい）/ **部分承認**（使える範囲を明記して使う）/ **未承認**（使わない）。

## G. 承認後の手続き

1. 承認した成果物を #35 に渡し、**フォルダ一式**の SHA-256 付き GitHub Release を作ってもらう
   （#22 はパッケージングを行わない）。
2. 運営者が適用する手順は #36（`Update.ps1` / 手動 ZIP 差し替え）。
3. 運営者への通知は次の 1 文で足りる:

   > 「展示中のゲームを終了 → パッチを 1 回だけ適用 → `FlashBeat.exe` を直接起動してください。」

4. **訪問者セッション中の強制更新は行わない。**
5. 不具合が出た場合は **新しいパッチを別途配布** して直す（過去版への切替 / 自動ロールバック / ダウングレードは作らない）。

## H. 禁止事項（#22 の範囲外）

- `Play.cmd` / `Update.cmd` / `Rollback.cmd`、常設ランチャー、起動時自動更新、ユーザー向けロールバック UI・コマンド、独自更新サーバー。
- `tools/**` の改変（**#35** が `tools/release/**`、**#36** が `tools/update/**` を所有）。
- Unity のゲームコード / シーン / プレハブの改変（Win ワーカーの所有領域）。
- 未証明のステージへの green 表示。
