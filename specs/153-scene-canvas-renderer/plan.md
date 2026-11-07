# plan: Title/Select シーンの欠陥修復 (#153)

## アプローチ
1. 欠陥 A は supervisor 適用済みの working tree 差分をレビューして採用:
   - `script/fix_scene_canvas_renderer.py` のロジック確認
     (TMP 判定は `!u!114` + `m_text`、ID は `9000000000000090000` 起点で衝突回避)。
   - シーン差分の検証: 追加 222 ブロックの `m_GameObject` が直前の GameObject を指すか、
     `m_Component` リスト更新済みか、既存行の削除がないかを python で機械検証。
2. 欠陥 B は静的チェックでは検出済み項目 (単一 Transform/GO、親子相互リンク、
   fileID 重複、SceneRoots) が全て CLEAN のため、実証的に調査:
   - `Assets/Editor/SceneAudit153.cs` (batch mode) でシーンをロードし、
     ロード時 Error を捕捉 + ロード後のヒエラルキー (name/childCount) をダンプ。
   - `EditorSceneManager.SaveScene` で `/tmp` に再保存し、元 YAML と diff して
     Unity が書き換えた/落としたブロックを特定。
     → 結果: ブロック数・fileID・階層は完全一致。唯一の構造差分は
       **SceneRoots ブロックの位置** (TitleScene: 39 → 179、SelectScene: 40 → 536)。
   - 因果を確定するため、シーンのコピーを 2 パターン作り batch mode で比較:
     - `R_roots_last`: SceneRoots ブロックのみ末尾へ移動
     - `G_go_first` : 各 GameObject ブロックを自分のコンポーネントより前に並べ替え
                      (SceneRoots は先頭側に残す)
     → SceneRoots 位置だけが原因なら R のみ Error 消失 / G は Error 残存。
3. 修正は `script/fix_scene_canvas_renderer.py --scene-roots-last` で
   SceneRoots ブロックを末尾へ移動するのみ (他ブロックは 1 行も変えない)。
4. 検証: Unity batch mode EditMode テスト + 2 つの grep カウント。
5. 後片付け: 検証用バリアントシーン (一時ファイル) を削除、
   `ProjectSettings/ProjectSettings.asset` を確認、
   (`FlashBeat.153.slnx`, `script/__pycache__/` はコミットしない)。

## リスク
- 再保存 diff は Unity の正規化 (コンポーネント順序、float 表記) でノイズが出る。
  ノイズに紛れて本質的欠陥を見逃さないよう、ロード後ヒエラルキーのダンプとの
  突き合わせを併用した。
- シーンファイルは LF / BOM 無しの現状フォーマットを維持する
  (末尾改行の有無も元のファイルと同一に保つ)。
- SceneRoots の移動だけでは直らない可能性に備えて、
  `R` / `G` 2 系統のバリアントで原因を分離してから修正する。
