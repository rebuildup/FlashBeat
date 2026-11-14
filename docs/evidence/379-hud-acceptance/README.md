# #379 受入証跡 — Game HUD (歌詞位置・黒帯・スコア折返し/ポップ・判定文字)

実行日: 2026-11-15 / ブランチ `w/379-hud-acceptance` / macOS ビルド (`Builds/macOS/FlashBeat.app`)

## 実行

```
./Builds/macOS/FlashBeat.app/Contents/MacOS/FlashBeat \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -autotest v6interact -autotest-out docs/evidence/379-hud-acceptance/fhd
```

## 結果

`fhd/autotest-v6interact-result.txt`: **pass=97 fail=0** (1920x1080)。

#379 の新規チェック (全て PASS、実行中の `V6GameHudView` から監査):

- **黒帯**: `Top Shade` が存在し、スプライトは 1×2 px の縦グラデーション
  (下端 alpha=0 / 上端 alpha>0)。高さは `m.TopShadeH` の design 値と一致。
  design 決定 D-379「廃止せずグラデーションにする」に合致 (一様な黒帯ではない)。
- **歌詞位置**: 歌詞スロットの実ワールド左上座標が
  `V6HudLogic.LyricSlotOrigin` (共有フレーム origin + design (304,250)) と
  2px 以内で一致。実測値は Player.log の `lyric origin actual=... expected=...`。
- **スコア**: `GManager.score` を 1,234,567 / 98,765,432 に書き換え、
  表示文字列 (`ScoreTextForAudit`)・使用中桁数 (9 / 10 = カンマ込み)・
  全スロット同一 y (`ScoreSingleLineForAudit`) を検証。折り返しは起きない
  (1 文字 = 1 固定幅スロット構成)。
- **スコアポップ**: 値変更直後に `ScorePopActiveForAudit` が true、
  `DigitPopTotal` (0.20s) 経過後に false へ戻ることを確認。
- **判定行**: `V6UiMode.VerdictRow = true` で v6 判定行が有効、
  判定テキストのフォントサイズが design テーブル `m.VerdictFs` と一致
  (`VerdictUsesDesignFontSizeForAudit`)。旧 3D 判定クローンは
  `ScanJudgeClones` が非表示化する既存経路 (Issue #432/#434/#445)。

## 追加した監査 API

- `V6GameHudView`: `TopShadeForAudit` / `TopShadeHeightForAudit` /
  `LyricBoxForAudit` / `LyricFontSizeForAudit` / `CanvasSizeForAudit` /
  `ScoreActiveDigitsForAudit` / `ScoreSingleLineForAudit` /
  `ScorePopActiveForAudit` / `VerdictFontSizeForAudit` /
  `VerdictUsesDesignFontSizeForAudit` / `VerdictTextForAudit`
- `V6HudScore.ValueStrip`: `ActiveCount` / `IsSingleLine` / `AnyAnimating`
- `V6HudChrome`: `topShadeImage` の保持 (監査用)

## 補足

- 歌詞とノートの非重複は、歌詞スロットが design の (304,250) 1120×内容高で
  プレイフィールド上部に固定される構造上の配置で担保される (判定線 y=792 より
  十分上)。FHD での実測 origin は result ファイルと Player.log を参照。
- 2K / Mac の歌詞位置は `LyricSlotOrigin` が解像度ごとの共有フレーム origin を
  使うため同じ式で決まる。FHD の実機値で式の正しさを確認済み。
- Windows 実機での最終確認は実機検証トラックで継続 (Mac 受入とは分離)。
