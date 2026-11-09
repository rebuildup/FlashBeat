# tasks — 239 v6 front door

依存順。各タスクは compile-check を通すこと。

- [x] 1. spec/plan 固定 (spec.md / plan.md)
- [x] 2. V6TitleLinks に EntryRequest (PendingEntry) を追加
- [x] 3. V6TitleLogic にタブキーの単一ソース (HistoryTabKey / RankingKey + KeyCode) を追加、V6TitleView から参照
- [x] 4. TitleSceneManager: V6Links 設定/解除・H/R poll・pending 設定つき遷移
- [x] 5. SelectSceneManager: pending entry 消費と OnV6OpenSettings の全モード有効化
- [x] 6. V6HistoryLogic: RecordedCountLabel (PlaysLabel 差し替え)
- [x] 7. V6HistoryView: ラベル一致・順位/HOLD セル削除・曲ランキング空状態分岐
- [x] 8. V6RankingLogic: ShowOnlineSegment / SongRankingEmptyMessage / PlayersEmptyMessage
- [x] 9. V6RankingView: 凡例一致・Online セグメント条件表示・PLAYERS 矢印削除
- [x] 10. EditMode テスト追加/更新 (V6HistoryLogicTests / V6RankingLogicTests / V6TitleRuntimeTests)
- [x] 11. evidence 記録 (docs/evidence/239-v6-front-door.md)
- [x] 12. compile-check 通過 (flashbeat assembly)
