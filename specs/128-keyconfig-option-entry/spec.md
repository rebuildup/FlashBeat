# Spec: キー設定を OPTION 画面の一人前の項目にする (Issue #128)

## 背景
キー設定 (KeyConfigUI) は F2 ショートカットのみで開け、存在が画面から見えない。
legacy Option 画面 (NOTE / SOUND / FLASH セクション + 左下 Media cache パネル) に
一人前の項目として見える化する。F2 は副次的ショートカットとして残す。

## 要件
1. KeyConfigUI は公開 API `Open()` / `Close()` / `IsOpen` を持つ (origin/109-patch の修正で済み)。
2. OptionScene ロード時にコード生成の「キー設定」ボタンを追加する
   (KeyConfigOptionButton.cs。MediaCacheOptionPanel と同じ sceneLoaded フック方式)。
   - 白ラベル + 1px アクセント枠 #3FA9F5。Media cache パネルの右隣 (600,40) に配置し
     1024x768〜2560x1440 で既存項目と重ならない。
   - キーボードでも到達可能: Tab でフォーカス、Enter で開く (文字キーは使わない)。
   - キャプション「F2 でも開けます」を添える。
   - クリックで `KeyConfigUI.Open()`。
3. KeyConfigUI を開いている間、Option シーン自身のキー処理は反応しない
   (OptionSceneManager.Update は Input を読まないためガード不要。EventSystem は
   KeyConfigUI 側で停止済み)。
4. フッター等の豆腐文字を修正する: NotoSansJP SDF の静的アトラスに無い漢字
   (選択/既定/配置/標準/準/列/置/最低/必要/個/存/除) を使わず、
   アトラスの元テスト (曲名・歌詞ほか) に存在するグリフのみに書き直す。
5. KeyConfigUI ヘッダー下に「現在: 初期設定 / 現在: カスタム」を表示する
   (KeyBindings.Current と既定の比較。既定テーブルは private のため複製し、
   テストでずれを検出)。
6. 純粋ロジック (既定 vs カスタム判定) の NUnit テストを追加する。

## 制約
- Unity を実行できないため、全コードを目視再読 + グリフ検証スクリプトで確認。
- 所有ファイル: KeyConfigUI.cs / KeyConfigOptionButton.cs (新規) / テスト / OptionSceneManager 最小ガード (不要と判断)。
- LFS バイナリには触れない。
