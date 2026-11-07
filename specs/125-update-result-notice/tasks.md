# Tasks: #125

- [ ] 1. `Update.ps1`: last-result.json を全終了経路で書く (Write-UpdateResult / 成功は切替後のみ / aborted と failed の区別)
- [ ] 2. `UpdateResultFile.cs` 新規 + `UpdateFlowModel.LastResultNotice` + `UpdateNoticeUI` 表示と mark-as-shown + 起動ログ文言変更
- [ ] 3. staging / 適用前の新版確認 (`IsStagedTagNewerThanCurrent`)
- [ ] 4. `UpdateResultFileTests.cs` (解析・文言・one-shot・版ガード) + `docs/operations/update-release.md` 追記
- [ ] 5. 109-patch へ同じ変更を適用して push
