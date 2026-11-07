# Mac 検証スクリプト (`script/mac-verify`, Issue #111)

展示開始後は Windows 実機を占有しない。Mac では独立 worktree でテスト・ビルドを回す。**リリース可否の正本は Windows 実機 (Unity 6000.5.6f1)**。

```sh
script/mac-verify test  [<ref>]   # EditMode テスト (既定 ref: origin/release-0-1-3)
script/mac-verify build [<ref>]   # macOS ビルド → Builds/macOS/FlashBeat.app
script/mac-verify status
```

- 作業場所: `../FlashBeat.macverify` (`FB_MAC_WORKTREE` で変更)。実行元のチェックアウトには触らない。
- エディタ: `UNITY_BIN` > 6000.5.6f1 (正式) > 6000.6.8f1 (代替)。**6000.6 は `Packages/` `ProjectSettings/` を書き換える。コミットしない** (スクリプトは前回の生成物を stash に退避する)。
- テストが使う LFS (日本語フォント SDF / `Assets/Game/Art/UI/**`) のみ部分取得する。未取得だと `JapaneseFontCoverageTests` と Title/Select シーンが落ちる。
- `-runTests` に `-quit` を付けない (テストが走らず exit 0 になる)。
- ビルドは既存出力の上書きをしない (上書きで `level1 is corrupted` を実測)。既存の `Builds/macOS` は退避する。
- 実測 (2026-11-07, trunk a99a65a, 6000.6.8f1): total=167 passed=166 failed=1 (`JapaneseFontCoverageTests.BuildScenes_UseTheJapaneseFont`、Title/Select の `Transform child can't be loaded`)。初回 10分21秒 (Library 生成込み)。
- ディスク: Library 1〜1.5GB/worktree。空きが少ない場合は不要な worktree の `Library/` を整理する。
