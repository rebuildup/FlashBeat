# tasks: Title/Select シーンの欠陥修復 (#153)

- [x] 1. 欠陥 A の working tree 差分をレビュー (222 ブロックの妥当性、m_GameObject 参照、
      fileID 一意性、m_Component 更新、削除 0 行) — 機械検証で CLEAN
- [x] 2. 全 14 シーンの静的チェック (両欠陥の有無) — Title/Select 以外 CLEAN
      (TMP↔CanvasRenderer 欠落 0 / Transform 階層整合 / SceneRoots 位置)
- [x] 3. 欠陥 B の実証調査 — SceneAudit153 でロード時 Error を捕捉、
      ロード後ヒエラルキーが完全であることを確認、
      再保存 diff で「唯一の差分 = SceneRoots ブロック位置」と特定
- [x] 4. 因果分離実験 — `R_roots_last` / `G_go_first` バリアントを batch mode で比較し、
      SceneRoots 位置が原因であること (G_go_first は GameObject 先行に並べ替えても
      Error が残る) を確定
- [x] 5. 欠陥 B の最小修正 — `script/fix_scene_canvas_renderer.py --scene-roots-last` で
      SceneRoots ブロックを末尾へ移動 (他ブロックは 1 行も変更なし)
- [ ] 6. Unity batch mode EditMode テスト実行 (368 total / 368 passed / 0 failed)
- [ ] 7. grep カウント確認 (CanvasRenderer 0 / Transform child 0)
- [ ] 8. 検証用バリアントシーン削除、ProjectSettings.asset 確認、ノイズファイル除外
- [ ] 9. コミット + push + Draft PR + Issue コメント
