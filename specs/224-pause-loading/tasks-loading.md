# Tasks — Issue #224 Loading レーン

- [x] T1 実進捗の経路: `GameLoadingProgress` 新設、`NotesManager` が done/total を報告、
      `V6GameRuntime` が読んで `SetLoading` に渡す。
      acceptance: 進捗不明 → 不定バー、0..1 → 確定バー + 「譜面を準備中… NN%」。
- [x] T2 jacket 表示: `V6LoadingViewLogic.JacketResourcePathOf` (選曲の対応表の写し)、
      `V6GameRuntime.LoadJacket` (Resources.Load + null キャッシュ)、無い曲は「無し」ラベル。
      acceptance: jacket 有無で表示が分岐すること (EditMode テスト + 対応表の全ファイル実在確認)。
- [x] T3 Retry: 失敗状態に「再試行」ボタン。`GameSceneManager.RetryGame()` (実在経路) に割当。
      acceptance: ボタンが死んでいない (実遷移メソッドに接続、leaving ガードで多重遷移防止)。
- [x] T4 loading.mp4 表示: `RenderTexture` (1920×1080) を `targetTexture` に割当、RawImage に出す。
      素材の実在 (LFS 実体) と寸法を ffprobe で確認。
- [x] T5 純ロジック分離 + EditMode テスト: 進捗 0..1 → 文言/幅、既知/不明判定、
      jacket 対応表、`GameLoadingProgress` の状態遷移。
      acceptance: `script/compile-check.sh --assembly flashbeat` / `--assembly tests` が通る。
- [x] T6 evidence: `docs/evidence/224-loading-progress.md` に実装・検証・残存リスクを記録。
      実機プレイは未検証と明記。
