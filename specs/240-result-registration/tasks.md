# Tasks: 240 — v6 Result のローカル登録契約の復元

- [x] 1. `Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs` — 純ロジック追加:
      `IsSubmittable` / `RegistrationMode` / `NormalizeLocalName` /
      `LocalSaveMessage` / `InitialName` / `ResolveChartVersion` と
      ローカル名の定数。既存のオンライン 4 区分 (`ClassifySubmitResult` /
      `SubmitStatusMessage`) は変更しない。
- [x] 2. `Assets/Game/Scripts/UI/V6/Result/V6ResultRegistration.cs` (新規) —
      `enum V6RegistrationMode` と `V6LocalNameStore` (PlayerPrefs
      `"V5PlayerName"` の読み書きのみ)。
- [x] 3. `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs` —
      方式判定 (`OpenModal`) / ローカル保存分岐 (`TrySubmit`,
      `SubmitCoroutine` の送信不可フォールバック) / ローカル結果メッセージ /
      初期値 / 入力上限と文字種の方式別切替 / `SongOf` の固定版優先 /
      `StartRankingFetch` の固定版 / 古いコメント (`:615` 付近) の修正。
- [x] 4. `Assets/Game/Tests/V6ResultRegistrationTests.cs` (新規, + `.meta`) —
      ローカル保存 (PlayerPrefs ラウンドトリップ)、ローカル語彙にオンライン語彙が
      含まれないこと、初期値、pinned 版の解決、オンライン 4 区分の維持。
      通信 (`RankingClient.PostScore` / `GetLeaderboard`) は呼ばない。
- [x] 5. compile-check: `--assembly flashbeat` / `--assembly tests` ともに成功。
- [x] 6. `specs/240-result-registration/{spec,plan,tasks}.md` と
      `docs/evidence/240-result-registration.md` の作成。

## 未実施 (スコープ外 / 他者の作業)

- [ ] EditMode テストの実実行 (Unity Editor 起動が必要なため Supervisor が統合時に実行)。
- [ ] 実機 (Windows 展示 PC) での画面確認 — 展示 PC 操作は禁止。
      `docs/evidence/240-result-registration.md` に「未検証」と記録済み。
- [ ] 実オンライン送信の確認 (実 API 呼び出しは禁止)。
