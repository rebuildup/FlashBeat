# Spec: 121 — Select v5 (list + panel, typing filter, Ready, History, Ranking)

Issue: #121 (Refs #41) — branch `121`, base `release-0-1-3`.

## 目的

design/flashbeat-ui.pen **v5** の `Screens v5 · layers & shapes (FHD)` のうち
`02 Select` / `02b Select · typing (filter-away)` / `02c Select · Ready` /
`02d History` / `07 Ranking (songs × players)` を、コード生成 uGUI
(`FlashBeat.UI.V5` 基盤: UISizeProfile / V5Tokens / V5Ui) で 1:1 再現する。
既存の scene 配置 UI (SelectScene.unity の Canvas) は Start 時に非表示化し、
挙動 (選曲・プレイ開始・ナビゲーション・ハイスコア) は SelectSceneManager
の既存メソッド / GManager 状態をそのまま使う。

## 正本

- design/flashbeat-ui.pen v5 (pen CLI でノード木と実測値を取得。PNG エクスポートと突き合わせ)
- design/ui-size-profiles.json → `UISizeProfile` (実ピクセル, Constant Pixel Size x1)

## 成果物

1. `Assets/Game/Scripts/UI/V5/SelectView.cs` — 画面一式をコード生成する View。
   - 状態: List / Typing (filter-away) / Ready / History / Ranking。
   - List: 左ウェル (640) にバック・検索・フィルタチップ・曲リスト (行 72) ・
     件数・レジェンド、右にジャケット帯 (1280x540, cover) + No/長さタグ +
     タイトルブロック (曲名・アーティスト・BPM/NOTES/LEVEL) + 下部パネル
     (ScoreRail + BEST + SONG RANKING / PLAYERS ウェル) 、Ready ボタン。
   - Typing: `Input.inputString` で絞り込み (KeyCode は使わない)。曲名/アーティスト部分一致。
   - Ready: パネルが「このプレイの設定」(Key Guide / Lyrics / NoteSpeed の
     SettingRow 3 行 + Start ボタン) に差し替わる。NoteSpeed は
     `GManager.noteSpeed` に反映。
   - History: 左に記録リスト (Hiscore>0 の曲。日付データはないため日付グループ無し)、
     右にジャケット帯 +グレード大文字 + SCORE + ScoreRail + Judge ストリップ
     (Perfect/Great/Bad/Miss/Hold/MaxCombo は未記録のため `–`)。
   - Ranking: 左に曲リスト (480 ウェル)、中央に曲別ランキング (Local のみ。
     データは自分の Hiscore のみ)、右にプレイヤーランキング (データ無し → 空状態)、
     下にプレイヤーサマリ (累計 = Hiscore 合計 / プレイ曲 / S 以上)。
   - データは `GManager.ActiveSongs` + `GManager.Hiscore` のみ。無いデータは
     空状態または `–` で示す (デザインの値を捏造しない)。
   - ジャケット: `Resources/V5/select/jacket/<曲名>` (実曲と一致するもののみ同梱) →
     YouTube サムネイル (既存の取得パターン) → 無地タイルの順。デザインの
     jk-* を全曲に流用しない。
   - キー: ↑↓ 選択 / Enter 決定→Ready / Space プレイ開始 (レガシー維持) /
     Esc 戻る / Tab 絞り込みトグル / F2 履歴 / F3 ランキング /
     Ready 中 ←→ 値変更・R スタート。マウス: 行クリック選択・ホイール移動・
     ボタンクリック。
2. `Assets/Game/Scripts/UI/SelectSceneManager.cs` — Update のキー処理を
   SelectView に委譲。カタログ更新時は view へ再描画依頼。プレイ開始パス
   (`GameScene()` / `StartRemoteSong`) は既存のまま view から呼ぶ。
3. `Assets/Game/Resources/V5/select/jacket/` — 実曲と一致するジャケットのみ同梱
   (Poison / Calamity / まにまに / エウタナシア / 終焉逃避行 / 4th smile)。
4. `Assets/Tests/Editor/SelectViewLogicTests.cs` — 純ロジック (グレード判定,
   フィルタ/ソート, スコア・時間フォーマット, リストのスクロール窓)。

## 非目標

- 他画面 (Title/Game/Result/Option) の改修、オンラインランキング (API 未実装)、
  履歴の永続化 (保存先が存在しない)、V5Ui.cs 自体の編集。
