# v0.2.5 RC 検証記録 (#789 歌詞実描画 + #794 HEAD 素材整理)

対象: `release-0-2-1` / ビルド元 SHA `2aaa277b` (VERSION=0.2.5、PR #840/#841/#842 マージ後)。
macOS 実機ビルド (`FlashBeat.macverify` worktree、Unity 6000.5.6f1) で AutoTest を実行。

## 自動検証

| 項目 | 結果 | 証跡 |
|---|---|---|
| 実機 AutoTest v6songend (歌詞 ON) | **pass=32 fail=0** (※新規チェック追加後の別ランでは pass=35 fail=0) | `lyrics-on/autotest-v6songend-result.txt` |
| 実機 AutoTest v6songend (歌詞 OFF 対照) | **pass=32 fail=0** | `lyrics-off/autotest-v6songend-result.txt` |
| 再生 | ツイッターランド (cloud chart v1, 130 notes)、score=1,165,000 = ScoreMaxV6(130) → SS 相当 | 同上 |

## #789 歌詞パネル実描画レビュー (vision model)

歌詞あり区間 (mediaTime ≈15–29s、`lyrics-on/*-06-lyrics-*.png` 18 枚) を
画像対応モデルで判読:

- 歌詞本文が実際に描画されることを確認。読み取れた文言
  (「切らす」「裁かれざる極悪な全能の神が」「うだつ上げたら去る」「愛」
  「限界と」「勘ぐって」「この呼吸を褒めて」) は
  `GET /api/v1/charts/twitterland?version=1` の `chart.lyrics.mondai` と一致。
- 歌詞 OFF 対照ラン (`-autotest-lyrics off`) の同区間フレームでは
  歌詞テキスト・パネル UI が描画されないことを確認
  (`lyrics-off/autotest-v6songend-70-06-lyrics-07.png` 他)。
- パネル枠は歌詞のない区間・曲終端まで維持 (ON ランの全 18 枚で枠の残像・
  黑塗りの異常なし)。ノーツ・判定線との重なりなし。

判定: **#789 受入条件を充足** (クラウド譜面→ChartLyrics→HUD の実描画まで確認)。

## 背景モザイク/破損フレーム (Issue #844 として分離)

- `notes/` の 3 枚は破損フレームの証跡
  (burst-17, burst-59, lyrics-17 相当)。
- 切り分け: ソース mp4 は ffmpeg で全編デコードエラー 0、破損時刻の
  ソースフレームも正常 → Unity VideoPlayer 経路の断続的デコード/テクスチャ
  破損。詳細は Issue #844。
- v0.2.5 では既知の制限として扱い、修正は #844 で継続。

## #794 HEAD 素材整理 (PR #840)

- Web fixture (`who.*`→`seed.*`)、`sample-chart.json`、design jackets、
  `Art/Images` 第三者画像、`publish-catalog.mjs` を権利クリア fixture/参照へ整理。
- 台帳 `docs/audit/asset-license-ledger.md` に残存項目と操作別リスクを記録
  (PR #843)。本番 D1・Git 履歴/LFS・過去 Release は承認境界を維持し未実施。

## 配布物

- macOS: `dist/macos/FlashBeat-0.2.5-macos-2aaa277b.zip`
  SHA-256 `46d2bae380e104a9777d7bf3f6ca5ae6b4328799cc2c56b2b6c4265892a9424c`
  実機 autotest 完走確認済み (上記ビルドと同一 .app を ZIP 化)。
- Win64: `dist/win64/FlashBeat-0.2.5-win64-2aaa277b.zip`
  SHA-256 `c882ac3a56f6573ac553f1eaca2c333d99dd174725e7e8bee04122d1a1fed8c9`
  **Win64 実機起動・プレイは未検証** (macOS からのクロスビルド)。
