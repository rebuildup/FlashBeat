# Spec: 123 — v5 再実装: Result / Result-Register

Issue: #123 (Refs #88 #96 #100 #106 #41) — branch `123`, base `release-0-1-3`.

## 目的

ResultScene を design/flashbeat-ui.pen **v5** (exports `05-result.png`, `05b-result-register.png`)
に 1:1 で合わせてコードで再実装する。既存 `ResultSceneManager` の振る舞い
(スコア/判定数/ハイスコア表示, Retry, SelectScene 遷移, サムネ取得) は維持する。

## 正本

- `design/exports/screens/05-result.png` / `05b-result-register.png` (1920x1080)
- pen CLI で取得した `05 Result` (b7atN) / `05b Result · Register` (ZaG3V) の実測値:
  - Jacket band 0,0 1920x300 + Dim / RESULT 48,56 fs30 / Title(right) 1272,176 w600 (fs52 + fs22)
  - Rank letter 72,296 fs420 / Score column 640,330 w600 (SCORE fs22, score fs68, sub fs30)
  - Breakdown rows 640,592 (fs30 rows, Total fs38/52)
  - Judge Strip 48,836 w1192 (Marker r=3, value fs38 w500, label fs22) + Judge Ratio 48,944 1192x16 r8
  - Ranking Panel 1280,332 592x560 r16 (Head fs38 gold + 未登録 fs22 / Well r16 / RankRow 560x56)
  - Actions 1000,984 (Button h56 r8, icon 32, label fs30) / Legend 48,992 (KeyCap h36 fs22)
  - Register Dialog 560,280 800w r16 (head fs38, summary fs30, name field r8 fs52, hint fs22, buttons h56)
- 色/フォントは `V5Tokens`, サイズは `UISizeProfile.Current` (Constant Pixel Size x1)。

## 成果物

1. `Assets/Game/Scripts/UI/V5/ResultView.cs` — コードで v5 レイアウトを構築する View。
   - 純ロジック static: `RankFor(score)` (既存閾値 SS/S/A/B/C を 1:1)、
     `FormatScore` (3桁カンマ)、`DeltaLabel(prev,curr)`、`NormalizeName` (A-Z0-9, max 8)。
   - Result 画面: ジャケット帯 (YouTube サムネ) + RESULT + 曲名/アーティスト + 巨大ランク文字 +
     SCORE/ハイスコア差分 + 曲情報行 (BPM/LEVEL/NOTES) + 判定ストリップ (Perfect/Great/Bad/Miss/Combo) +
     判定比率バー + ローカルハイスコアパネル + 下部キーレジェンド + Retry/Register/OK ボタン。
   - データに無いものは表示しない: ACCURACY/ボーナス内訳/Hold/全国ランキング (#N/42) は省略し、
     対応する場所にはローカルデータ (ハイスコア差分, BPM/LEVEL/NOTES) を置く。
   - Register モーダル (05b): Scrim + ダイアログ。登録名入力 (TMP_InputField, A-Z0-9 最大8文字,
     PlayerPrefs `V5PlayerName` にローカル保存のみ — ネットワークなし)、Skip (Esc) / Register (Enter)。
     バッジは「ローカル」(オフライン) を表示。
   - 入力: R=Retry / Enter=Register / Space=OK (Select) / Esc=Select / ←→=ボタン選択 / マウスクリック。
     モーダル中は Esc=Skip, Enter=Register, 入力欄にタイピング。
   - レイアウトはエッジ相対アンカー + `UISizeProfile.Current` (1024x768〜3420x2148 で成立)。
2. `ResultSceneManager.cs` の最小改修: レガシー Canvas を非アクティブ化し View を生成、
   入力を View に委譲、サムネテクスチャを View に渡す。`Retry()` のリセット経路は今日と同一
   (GManager.ResetSession と同一フィールドの手動ゼロ + `Initiate.Fade("GameScene")`)。
3. `Assets/Tests/Editor/ResultV5ViewTests.cs` — RankFor/FormatScore/DeltaLabel/NormalizeName の
   EditMode NUnit。

## 非目標

- ネットワーク登録 (オフライン/ローカルのみ)、Hold ノーツ、精度 (accuracy)、ボーナス内訳、
  全曲ランキング、他画面の改修、V5Ui.cs の編集。
