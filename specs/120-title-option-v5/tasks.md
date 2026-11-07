# Tasks: 120

1. [x] `Assets/Game/Resources/V5/Common/` に edge-left/right.png, 13.png, title-bg-hi.png をコピー
2. [x] `V5Decor.cs` (背景/エッジ/ロゴ) + `TitleView.cs` + `TitleSceneManager` 統合
3. [x] `OptionView.cs` (4 タブ, キー設定行, F2 言及) + `OptionSceneManager` 統合
4. [x] `OpeningView.cs` + `OpeningSceneManager` 統合
5. [x] `TitleOptionV5Tests.cs` + 全変更ファイル再読 (コンパイルチェック) + commit/push/Draft PR

## Feedback round (Supervisor Mac review, 3420x2148)

6. [x] ロゴ上端クリップ修正: V5Decor.BuildLogo の pivot (0,0)→(0,1) (anchoredPos=左上) + TitleLayout (design 比率, 高さは画面高さに比例, 左右中央)
7. [x] フレーム線: 太さは高さ比例, 縦バーは横バー右端に揃えて画面下端まで, 解像度変化で ApplyLayout 再適用 (TitleLayout.cs 純関数 + テスト)
8. [x] Ranking pill: TitleLinks.RankingRequested (新ファイル) が設定されている間のみ表示/遷移。History は遷移先未実装のため非表示のまま
9. [x] メニューアイコンは見送り (NotoSansJP 静的アトラスに字形なし, 豆腐リスク)。凡例の日本語 (選択/決定) は維持 (フォント修正は Supervisor)
