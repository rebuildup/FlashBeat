# Issue #118「更新は明示式」— 受入条件の実測と根拠

対象ブランチ: `w/w118-explicit-update` (base: `origin/main` d8c1b39)
対象 Issue: [#118] Update/P0 「更新は明示式: タイトル画面に『アップデートあり』表示、押したときだけ更新 (押さなければ更新しない)」

---

## 0. 出发点: Issue 本文は古い

Issue 本文は「`UpdateChecker.cs:394 付近` で `OnApplicationQuit` から自動適用している」と
書いているが、`origin/main` の現状は Issue 本文より **かなり進んでいる**。
本作业は「実装状況の確定 + 不足分のテスト・証拠化」が目的。

`grep -rn OnApplicationQuit Assets/`:

```
Assets/Game/Scripts/Update/UpdateFlowModel.cs:18:    /// 自動適用の経路はこのクラスに存在しない (OnApplicationQuit から呼ぶ API を提供しない)。
Assets/Game/Scripts/Update/UpdateChecker.cs:22:    /// OnApplicationQuit からの自動適用は存在しない。通信失敗・SHA 不一致では何も変更せず
Assets/Game/Scripts/Update/UpdateChecker.cs:578:        /// ここからだけが適用への入口 (OnApplicationQuit からの自動適用は存在しない)。
Assets/Tests/Editor/UpdateFlowTests.cs:233:    /// <summary>不変条件: OnApplicationQuit からの自動適用経路が存在しないことの構造的な保証。</summary>
```

→ **`OnApplicationQuit` メソッドの実装は 1 つも存在しない** (残るのはコメントとテストだけ)。
`UpdateChecker` に `OnApplicationQuit` は無く、runtime 侧的いかなる exit hook からも
適用を起動するコードは無い。

---

## 1. 受入条件 × 根拠 × 実測結果

| # | 受入条件 | 充足 | 根拠 (実装) | 根拠 (テスト) |
|---|---|---|---|---|
| 1 | 起動時は**確認だけ**、ダウンロードしない | 充足 | `UpdateChecker.CheckForUpdate()` (`Assets/Game/Scripts/Update/UpdateChecker.cs:111`) は Releases API の取得と候補判定までしか行わず、`DownloadAndStage` を呼ばない。候補判定は純関数 `UpdateChecker.EvaluateCheck()` (同 `UpdateChecker.cs:459`) に切り出した | `UpdateSourceStructureTests.CheckForUpdate_NeverDownloadsOrApplies` / `UpdateSourceStructureTests.DownloadAndStage_IsStartedOnlyByRequestDownload` / `UpdateCheckerEntryPointTests.RequestDownload_BeforeCheck_DoesNothing` / `UpdateCheckerEntryPointTests.RequestDownload_WithoutZipAsset_DoesNothing` / `UpdateCheckDecisionTests.*` (11 件) |
| 2 | ダウンロードは**ユーザーの明示操作**でのみ | 充足 | 唯一の入口は `UpdateChecker.RequestDownload()` (`UpdateChecker.cs:203`)。`Available` かつ ZIP アセットが無くても動かない。呼び出し元は `UpdateNoticeUI.Trigger()` (F3 / ピルクリック) と V6 Title の更新ボタンのみ | `UpdateSourceStructureTests.NoOtherRuntimeScript_CallsUpdateActions` (runtime 配下の他スクリプトから更新入口が呼ばれないこと) / `UpdateCheckerEntryPointTests.RequestDownload_*` / `UpdateNoticePresenterTests.ResolveAction_Available_IsDownload` |
| 3 | 適用も**ユーザーの明示操作**でのみ | 充足 | 入口は `UpdateChecker.RequestApply()` (`UpdateChecker.cs:299`) と `UpdateChecker.ApplyStagedUpdateNow()` (`UpdateChecker.cs:632`, Option 画面のボタン)。どちらも `Model.BeginApply()` を経由し、`UserConfirmed` が立つのは `BeginApply` のみ (`UpdateFlowModel.cs:136`) | `UpdateFlowTests.BeginApply_FromReadyOnly` / `UpdateFlowTests.NeverAutoApplies_NoPathToApplyingWithoutBeginApply` / `UpdateFlowTests.UpdateChecker_HasNoOnApplicationQuitHook` / `UpdateSourceStructureTests.UpdateNoticeUI_Trigger_CallsOnlyTheExplicitEntries` / `UpdateCheckerEntryPointTests.RequestApply_WithoutStagedUpdate_DoesNothing` |
| 4 | staging 済みでも**適用は押さない限り行われない** | 充足 | `MarkStagedReady()` (`UpdateFlowModel.cs:92`) は `Available → Ready` のみ。`Ready` から `Applying` へ移るのは `BeginApply()` (= ユーザー確認) だけ。staging 済みを起動時に再利用しても `Ready` で止まる (`UpdateChecker.cs:183-196`) | `UpdateFlowTests.MarkStagedReady_FromAvailable_GoesReadyWithFullProgress` / `UpdateFlowTests.Dismiss_HidesNoticeForSessionButKeepsState` / `UpdateFlowTests.NeverAutoApplies_NoPathToApplyingWithoutBeginApply` / `UpdateNoticePresenterTests.ResolveAction_NothingElse` (Ready 以外は何も起きない) |
| 5 | 失敗しても**現行版はそのまま使える** | 充足 | ダウンロード失敗・SHA 不一致・manifest 書き込み失敗は `Model.DownloadFailed()` → `Available` に戻り `UpdateStaging.ClearStaging()` (`UpdateChecker.cs:229-290`)。適用失敗は `Model.ApplyFailed()` → `Ready` に戻る (`UpdateChecker.cs:317`)。更新スクリプト起動前の再検証 (`TryLaunchUpdater` → `UpdateStaging.VerifyStaged`) も失敗なら起動しない (`UpdateChecker.cs:645-668`) | `UpdateFlowTests.DownloadFailed_ReturnsToAvailableWithMessageAndKeepsCurrentVersion` / `UpdateFlowTests.ApplyFailed_ReturnsToReadyWithMessage` / `UpdateStagingTests.VerifyStaged_MismatchedSha_False` / `UpdateStagingTests.VerifyStaged_MissingZip_False` / `UpdateNoticePresenterTests.BuildText_IncludesFailureMessage` |
| 6 | **ZIP 全体 + SHA-256 検証**を維持 | 充足 | ダウンロード後に `.sha256` を取得し、ZIP ファイル全体の SHA-256 をバックグラウンド計算して hex 完全一致を要求 (`UpdateChecker.cs:236-264`)。staging 済みは起動前にも再検証 (`UpdateChecker.cs:650`)。更新スクリプトにも `-ExpectedSha256` を渡す (`UpdateChecker.cs:685` / `:741`) | `UpdateStagingTests.ComputeSha256_KnownVector_Matches` (SHA-256("abc") の既知ベクトル) / `UpdateStagingTests.VerifyStaged_MatchingSha_True` / `UpdateStagingTests.VerifyStaged_MismatchedSha_False` |
| 7 | **prerelease を stable に自動適用しない** | 充足 | チャンネルは `StreamingAssets/update-channel.txt` で決まり、既定は stable (`UpdateChannel.cs:43`)。stable では prerelease が候補から除外 (`UpdateChecker.cs:407` → `ParseLatestRelease` の `release.prerelease` 判定)。判定は純関数 `EvaluateCheck(..., includePrerelease, ...)` に集約 | `UpdateChannelTests.StableChannel_PrereleaseIsNotAnUpdateCandidate` / `UpdateChannelTests.PreviewChannel_PrereleaseIsAnUpdateCandidate` / `UpdateChannelTests.StableChannel_OnlyPrereleaseExists_ReturnsNull` / `UpdateCheckDecisionTests.EvaluateCheck_StableChannel_DoesNotOfferPrerelease` / `UpdateCheckDecisionTests.EvaluateCheck_PreviewChannel_OffersPrerelease` / `UpdateSourceStructureTests.EvaluateCheck_TakesChannelFlagExplicitly` |
| 8 | **展示中にゲームを勝手に止めない** | 充足 | プロセス終了 (`Application.Quit()`) の呼び出しは `UpdateChecker.cs:321` の **1 箇所だけ**で、`RequestApply()` (ユーザー操作) の中。`UpdateNoticeUI` は Title 画面以外では通知を出さないので、ゲームプレイ中に更新が割り込まない | `UpdateSourceStructureTests.ApplicationQuit_ExistsOnlyInsideRequestApply` (ファイル全体で 1 箇所・`RequestApply` 内のであることを検証) / `UpdateNoticePresenterTests.ShouldShow_OnlyOnTitleScene` / `UpdateFlowTests.UpdateChecker_HasNoOnApplicationQuitHook` |

---

## 2. 「押さない = 更新しない」の選択肢 — どこから可能か

| 画面 | 操作 | 結果 |
|---|---|---|
| Title (ホーム) | なにもしない | 更新しない。通知ピルを出したまま放置してもダウンロードも適用も起きない (`ResolveAction` は `Available`/`Ready` 以外で `None`) |
| Title (ホーム) | F3 / ピルクリック | `Available` → ダウンロード、`Ready` → 適用 (`UpdateNoticeUI.Trigger`) |
| Title (ホーム) | F4 / × ボタン | このセッションだけ通知を隠す。staging と状態は保持 (`UpdateFlowModel.Dismiss`) |
| Title (v6) | 更新ボタン (`更新して再起動`) | 同じ `UpdateNoticeUI.Trigger()` を呼ぶ (`V6TitleView.TriggerUpdate`) |
| Option | 「更新を適用して再起動」ボタン | `UpdateChecker.ApplyStagedUpdateNow()` → `RequestApply()` |

**どちらの Title 実装もシーン編集なしで動く**:
`UpdateNoticeUI` は `RuntimeInitializeOnLoadMethod` で起動時に自前で Canvas を生成する
(`Assets/Game/Scripts/UI/UpdateNoticeUI.cs:40-53`)。
v6 Title もコードで構築 (`V6TitleView`)。
→ **`Assets/Game/Scenes/TitleScene.unity` は編集していない** (別 Worker の担当領域のため)。

---

## 3. 今回の変更 (不足分の補完)

実装の穴は「判断ロジックが MonoBehaviour の中に埋もれていてテスト不能」だった点。
新フレームワークは作らず、既存モジュールに相乗りして**純ロジックを切り出した**。

### 3.1 `UpdateChecker.EvaluateCheck()` (純関数・新規)

- 起動時の候補判定 (チャンネル / プラットフォーム / 新旧 / ZIP 资产) を
  `UpdateChecker.UpdateCheckDecision` 構造体 + `EvaluateCheck()` に切り出した。
  ネットワーク・ファイル IO 不要で EditMode から決定論的に検証できる。
- `CheckForUpdate()` はこの判定結果を使うだけになった (実装は 1 箇所に集約)。
- 副次的な修正: 確認に失敗した経路で `Model.CheckFinishedWithoutUpdate()` を呼ぶようにし、
  状態機械が `Checking` のまま固まらないようにした (従来は `Checking` で停止していた)。

### 3.2 `UpdateNoticePresenter` (純ロジック・新規)

- `Assets/Game/Scripts/UI/UpdateNoticePresenter.cs` を追加。
  「表示するか (どのシーンか)」「何と書くか」「押したら何をするか」をここに集約。
- `UpdateNoticeUI` はこのクラスに従うだけになった (既存の文言・挙動は同一)。
- `UpdateNoticeUI` の `TitleSceneName` は `UpdateNoticePresenter.TitleSceneName` を参照
  (二重定義の排除)。

### 3.3 テストの追加 / 修正 (false green の解消)

既存テストの点検結果:

- `UpdateFlowTests` — 状態機械の遷移は十分検証されていた (偽陽性なし)。ただし
  「起動時にダウンロードしないこと」は**どこにも書かれていなかった** → 追加。
- `UpdateCheckerTests` — バージョン判定・Release 選択・staging の SHA 検証は実測済みのベクトルあり。
  `ParseLatestRelease` は「候補を選ぶ」までで、「起動時は候補を提示するだけで
  ダウンロードしない」:end 論のテストが無く、ZIP アセット込みの判定が未検証 → 追加。
- `UpdateFlowTests.UpdateChecker_PublicApplyEntries_AreExplicitOnly` は
  public メソッド名の接頭辞検査而已で、private なフックや
  「誰が呼んでいるか」は見ていなかった → ソース構造検査を追加。

追加したテスト (35 件):

| ファイル | 件数 | 内容 |
|---|---|---|
| `Assets/Tests/Editor/UpdateSourceStructureTests.cs` (新規) | 6 | ソース構造で「起動は確認だけ」「適用は `RequestApply` だけ」「`Application.Quit` は 1 箇所だけ」「他スクリプトから更新入口を呼ばない」を固定 |
| `Assets/Tests/Editor/UpdateNoticePresenterTests.cs` (新規) | 13 | Title 限定表示・dismiss・操作の分岐・文言 |
| `Assets/Tests/Editor/UpdateCheckerEntryPointTests.cs` (新規) | 5 | 入口 (`RequestDownload` / `RequestApply` / `ApplyStagedUpdateNow`) が副作用を起こさないこと |
| `Assets/Tests/Editor/UpdateCheckerTests.cs` (追記) | 11 | `EvaluateCheck` の判定 (チャンネル / プラットフォーム / 新旧 / ZIP 資産) |

`UpdateSourceStructureTests` は「コメントと文字列リテラルを空白に置換してから
メソッド本体を取り出す」ことで、doc comment 内の同名識別子に惑わされずに
**実コード**を検査する (`UpdateSource` ヘルパ)。

---

## 4. 実測結果

### 4.1 EditMode テスト (全件)

```
/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity -batchmode \
  -projectPath /Users/rebui/work/FlashBeat.w118 -runTests -testPlatform editmode \
  -testResults /Users/rebui/work/FlashBeat.w118/TestResults.xml \
  -logFile /Users/rebui/work/FlashBeat.w118/unity-test.log
```

(`-quit` は付けない — 付けると 1 件も走らない)

| 回 | total | passed | failed | skipped |
|---|---|---|---|---|
| 変更前 (main 相当, 参考) | 1074 | 1074 | 0 | 0 |
| 変更後 (本 PR) | **1109** | **1109** | **0** | 0 |

内訳: 更新関連のテスト 135 件 → 170 件 (うち新規 35 件がすべて green)。

### 4.2 実機 (macOS / Windows プレイヤー)

**未検証。** `UpdateChecker` は起動時に GitHub Releases API を叩く外部通信に依存し、
安定版 Release の有無・ビルド版識別子・`StreamingAssets/update-channel.txt` の内容に
結果が左右されるため、エディタ内テストでは再現できない。

代替検証として、**通信を伴わない純関数に候補判定を寄せる** refactoring
(§3.1) を行い、GitHub API の実レスポンスと同じ形の JSON をテスト入力として
`EvaluateCheck` に通している (`UpdateCheckDecisionTests`)。
実機確認が必要な残項目:

- Windows 実機 (展示 PC) での F3 キー操作とピルクリック → **本 Worker は展示 PC を触っていない** (指示により禁止)。
- macOS プレイヤーでの Title ピル表示の実描画確認 → 未実施。

---

## 5. 残存リスク

1. **実機未検証**: 上記 4.2。実機のキー操作・描画は他 Worker / cc による確認が必要。
2. **staging 済み更新の Offline 適用**: 起動時の通信に失敗すると `Available` に到達しないため、
   前回 staging 済みの更新を「通信できない状態で適用する」導線は無い
   (Option の適用ボタンも `staged` が立っていないと出ない)。通信回復後の適用になる。
   これは「更新は明示式」の安全性とは矛盾しないが、**如果要**は別 Issue で扱うこと。
3. **`UpdateNoticeUI` と v6 Title の二重表示**: 両方が Title で更新導線を出す
   (v6 Title のボタン + `UpdateNoticeUI` のピル)。v6 Title 側は別 Worker の領域のため触っていない。
   実機での見え方は要確認。
4. **dismiss 後は F3 が効かない**: `Dismiss()` すると `ResolveAction` が `None` になるため、
   同一セッション中は Title の F3 で適用できない (Option のボタンは別経路として残る)。
   意図した挙動だが文言上の案内は「× で隠す」だけ。
5. **`Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset`**: Unity の import 副作用で
   再生成差分が出る (本 PR には含めていない)。
