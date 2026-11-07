# spec: Title/Select シーンの CanvasRenderer 欠落と Transform child エラー解消 (#153)

## 目的
展示ライン (`expo-ui`) の Unity EditMode テストスイートをグリーンにする。
ベースライン (origin/expo-ui): 368 total / 367 passed / 1 failed
(`JapaneseFontCoverageTests.BuildScenes_UseTheJapaneseFont` がシーンロード時の
Error ログで失敗)。

## 欠陥 A — TextMeshProUGUI に CanvasRenderer が無い (u!222)

### 根本原因
`Assets/Game/Scenes/TitleScene.unity` の 10 個、`Assets/Game/Scenes/SelectScene.unity`
の 36 個の GameObject が `TextMeshProUGUI` (`!u!114`, `m_text` を保持) を持つのに
`CanvasRenderer` (`!u!222`) コンポーネントを持っていなかった。
Unity はシーンロード時に自動補完して
`[Error] Creating missing CanvasRenderer component for TextMeshProUGUI in <name>`
をログに出し、`BuildScenes_UseTheJapaneseFont` が「予期しない Error ログ」で失敗する。
原因は手書き (ツール外) のシーン編集で 222 ブロックが抜けたこと。

### 修正
`script/fix_scene_canvas_renderer.py` (新規ツール) が、TMP を持つが 222 を持たない
GameObject を検出して:
1. GameObject の `m_Component` リストに `- component: {fileID: <新ID>}` を追記
2. `--- !u!222 &<新ID>` ブロック (`m_GameObject` は当該 GameObject を指す) を直後に挿入
新 ID は `9000000000000090000` 起点で既存 ID と衝突しない。
削除は 0 行 (+91 / +324 行のみ)。検証は静的チェック
(222 の m_GameObject が所有 GameObject を指す / 全 TMP が 222 を持つ / fileID 重複なし)
で両シーン CLEAN。

## 欠陥 B — "Transform child can't be loaded"

### 根本原因
`--- !u!1660057539` (SceneRoots) ブロックがシーンファイル途中にあること。

Unity はシーンファイルを保存する際に常に SceneRoots ブロックを**最後のブロック**として書き出す。
本リポジトリの健全なシーンは全てこの形をしている:

|シーン|SceneRoots ブロック位置 (ブロック番号/総数)|
|---|---|
|GameScene.unity|458 / 473 (末尾)|
|MakeFileScene.unity|15 / 16 (末尾)|
|Opening.unity|25 / 26 (末尾)|
|OptionScene.unity|382 / 395 (末尾)|
|ResultScene.unity|287 / 288 (末尾)|
|TypingScene.unity|26 / 27 (末尾)|
|**TitleScene.unity**|**39 / 180 (途中)**|
|**SelectScene.unity**|**40 / 537 (途中)**|

TitleScene / SelectScene は `47b4798 feat(ui): pen デザイン ...` で
GameObject 単位でブロックを連結して手書きされたもので、
連結の継ぎ目に SceneRoots ブロックがそのまま残っていた。

Unity の batch-mode での再保存 (`EditorSceneManager.SaveScene`) と元 YAML を diff すると:

- ブロック数 180 / 179 GameObject / 39 Transform — **元と完全に同一** (落ちたオブジェクトは無い)
- fileID も**全て同一** (再採番は無い)
- 内容差分は浮動小数点の桁 (`0.10588235294117647` → `0.105882354`)、
  `m_Children:` → `m_Children: []`、スクリプトに存在しないフィールド
  (`catalogStatus` / `cards`) の除去のみ
- **唯一の構造的差分: SceneRoots ブロックがブロック 39 → 179 (末尾) へ移動**

= Unity は同じ中身を読み込んでいて、SceneRoots ブロックの位置だけが「正規形」と異なっていた。
読み込み後のヒエラルキーは完全に正しい (root 7 個 / Canvas配下も全生存) が、
SceneRoots が途中にあるとロード時に
`Problem detected while opening the Scene file: ...` / `Transform child can't be loaded`
という Error が 1 件出る。この Error ログが `BuildScenes_UseTheJapaneseFont` を失敗させる。

### 修正
`script/fix_scene_canvas_renderer.py --scene-roots-last` が SceneRoots ブロックを
ファイル末尾へ**移動するのみ** (ブロック内の行は 1 つも変えない)。
変更は 11 行の削除 + 11 行の追加だけで、GameObject / Transform / fileID には触れない。

## 他シーンの状態
静的チェック (Transform↔GameObject リンク / m_Children 解決 / 多重親 / fileID 重複 /
TMP↔CanvasRenderer) を `Assets/**/*.unity` 全 14 ファイルに実施。
`Assets/NoteEditor/**` と `Assets/ThirdParty/**` を含むが、
修正対象は TitleScene / SelectScene の 2 ファイルのみ。

|シーン|transforms|GOs|TMP|CanvasRenderer 欠落|SceneRoots 位置 (1-based/総数)|結果|
|---|---|---|---|---|---|---|
|GameScene.unity|89|89|19|0|459 / 473|CLEAN|
|MakeFileScene.unity|3|3|0|0|16 / 16|CLEAN|
|Opening.unity|5|5|0|0|26 / 26|CLEAN|
|OptionScene.unity|99|99|26|0|383 / 395|CLEAN|
|ResultScene.unity|70|70|30|0|288 / 288|CLEAN|
|SelectScene.unity|120|120|36|0|**41 / 537**|A 修正後 CLEAN / B は本 PR で修正|
|TitleScene.unity|39|39|10|0|**40 / 180**|A 修正後 CLEAN / B は本 PR で修正|
|TypingScene.unity|5|5|0|0|27 / 27|CLEAN|

`Assets/NoteEditor/Scenes/NoteEditor.unity`、`Assets/NoteEditor/Plugins/UniRx/Examples/*.unity`、
`Assets/ThirdParty/Simple Scene Fade Load System/.../Level1.unity` / `Level2.unity`
(後者 2 ファイルは Unity 2022 以前の形式で SceneRoots ブロックを持たない) は
両欠陥とも該当なし。

### 付随所見 (本 PR の修正対象外)
`TitleSceneManager` の `catalogStatus` と `SelectSceneManager` の `cards` は
対応するスクリプトに存在しないフィールド (孤児フィールド) のため、
Unity の再保存で削除される。ロードエラーとは無関係なので残置する。

## 完了条件
- EditMode テスト: 368 total / 368 passed / 0 failed
- `grep -c "Creating missing CanvasRenderer" <log>` → 0
- `grep -c "Transform child can't be loaded" <log>` → 0
- Draft PR (base `expo-ui`, head `153`) オープン、Issue #153 にエビデンス投稿
