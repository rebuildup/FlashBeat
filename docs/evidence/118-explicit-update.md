# Issue #118「更新は明示式」— 受入条件の実測と根拠

対象ブランチ: `w/w118-explicit-update` (base: `origin/main` d8c1b39)
対象 Issue: [#118] Update/P0 「更新は明示式: タイトル画面に『アップデートあり』表示、押したときだけ更新 (押さなければ更新しない)」

---

## 0. 出発点: Issue 本文は古い

Issue 本文は「`UpdateChecker.cs:394 付近` で `OnApplicationQuit` から自動適用している」と
書いているが、`origin/main` の現状は Issue 本文より **かなり進んでいる**。
本作業は「実装状況の確定 + 不足分のテスト・証拠化」が目的。

`grep -rn OnApplicationQuit Assets/ | grep -v NoteEditor` (更新関連の参照のみ):

```
Assets/Game/Scripts/Update/UpdateFlowModel.cs:18:    /// 自動適用の経路はこのクラスに存在しない (OnApplicationQuit から呼ぶ API を提供しない)。
Assets/Game/Scripts/Update/UpdateChecker.cs:22:    /// OnApplicationQuit からの自動適用は存在しない。通信失敗・SHA 不一致では何も変更せず
Assets/Game/Scripts/Update/UpdateChecker.cs:658:        /// ここからだけが適用への入口 (OnApplicationQuit からの自動適用は存在しない)。</summary>
Assets/Tests/Editor/UpdateFlowTests.cs:233:    /// <summary>不変条件: OnApplicationQuit からの自動適用経路が存在しないことの構造的な保証。</summary>
```

→ **`OnApplicationQuit` メソッドの実装は 1 つも存在しない** (残るのはコメントとテストだけ)。
`UpdateChecker` に `OnApplicationQuit` は無く、runtime 側のいかなる exit hook からも
適用を起動するコードは無い。
(除外している `Assets/NoteEditor/Plugins/UniRx/` の `OnApplicationQuit` は
vendored ライブラリのライフサイクル通知で、更新とは無関係。
`NoteEditor` は scene list に入らず `FlashBeat.exe` にも含まれない。)

---

## 1. 受入条件 × 根拠 × 実測結果

| # | 受入条件 | 充足 | 根拠 (実装) | 根拠 (テスト) |
|---|---|---|---|---|
| 1 | 起動時は**確認だけ**、ダウンロードしない | 充足 | `UpdateChecker.CheckForUpdate()` (`Assets/Game/Scripts/Update/UpdateChecker.cs:111`) は Releases API の取得と候補判定までしか行わず、`DownloadAndStage` を呼ばない。候補判定は純関数 `UpdateChecker.EvaluateCheck()` (同 `UpdateChecker.cs:486`) に切り出した | `UpdateSourceStructureTests.CheckForUpdate_NeverDownloadsOrApplies` / `UpdateSourceStructureTests.DownloadAndStage_IsStartedOnlyByRequestDownload` / `UpdateCheckerEntryPointTests.RequestDownload_BeforeCheck_DoesNothing` / `UpdateCheckerEntryPointTests.RequestDownload_WithoutZipAsset_DoesNothing` / `UpdateCheckDecisionTests.*` (12 件) / `UpdateSourceStructureTests.CheckForUpdate_HasThreeFailurePaths_ReturningToIdle` |
| 2 | ダウンロードは**ユーザーの明示操作**でのみ | 充足 | 唯一の入口は `UpdateChecker.RequestDownload()` (`UpdateChecker.cs:224`)。`Available` かつ ZIP アセットが無くても動かない。呼び出し元は `UpdateNoticeUI.Trigger()` (F3 / ピルクリック) と V6 Title の更新ボタンのみ | `UpdateSourceStructureTests.NoOtherRuntimeScript_CallsUpdateActions` (runtime 配下の他スクリプトから更新入口が呼ばれないこと) / `UpdateCheckerEntryPointTests.RequestDownload_*` / `UpdateNoticePresenterTests.ResolveAction_Available_IsDownload` |
| 3 | 適用も**ユーザーの明示操作**でのみ | 充足 | 入口は `UpdateChecker.RequestApply()` (`UpdateChecker.cs:320`) と `UpdateChecker.ApplyStagedUpdateNow()` (`UpdateChecker.cs:659`, Option 画面のボタン)。どちらも `Model.BeginApply()` を経由し、`UserConfirmed` が立つのは `BeginApply` のみ (`UpdateFlowModel.cs:136`) | `UpdateFlowTests.BeginApply_FromReadyOnly` / `UpdateFlowTests.NeverAutoApplies_NoPathToApplyingWithoutBeginApply` / `UpdateFlowTests.UpdateChecker_HasNoOnApplicationQuitHook` / `UpdateSourceStructureTests.UpdateChecker_ApplyEntries_AreCalledOnlyFromUserEntryPoints` / `UpdateSourceStructureTests.UpdateNoticeUI_Trigger_CallsOnlyTheExplicitEntries` / `UpdateSourceStructureTests.UpdateNoticeUI_EntryCalls_AreOnlyInTrigger` / `UpdateCheckerEntryPointTests.RequestApply_WithoutStagedUpdate_DoesNothing` |
| 4 | staging 済みでも**適用は押さない限り行われない** | 充足 | `MarkStagedReady()` (`UpdateFlowModel.cs:92`) は `Available → Ready` のみ。`Ready` から `Applying` へ移るのは `BeginApply()` (= ユーザー確認) だけ。staging 済みを起動時に再利用しても `Ready` で止まる (`UpdateChecker.cs:184-197`) | `UpdateFlowTests.MarkStagedReady_FromAvailable_GoesReadyWithFullProgress` / `UpdateFlowTests.Dismiss_HidesNoticeForSessionButKeepsState` / `UpdateFlowTests.NeverAutoApplies_NoPathToApplyingWithoutBeginApply` / `UpdateNoticePresenterTests.ResolveAction_NothingElse` (Ready 以外は何も起きない) |
| 5 | 失敗しても**現行版はそのまま使える** | 充足 | ダウンロード失敗・SHA 不一致・manifest 書き込み失敗は `Model.DownloadFailed()` → `Available` に戻り `UpdateStaging.ClearStaging()` (`UpdateChecker.cs:250-311`)。適用失敗は `Model.ApplyFailed()` → `Ready` に戻る (`UpdateChecker.cs:337`)。更新スクリプト起動前の再検証 (`TryLaunchUpdater` → `UpdateStaging.VerifyStaged`) も失敗なら起動しない (`UpdateChecker.cs:672-695`) | `UpdateFlowTests.DownloadFailed_ReturnsToAvailableWithMessageAndKeepsCurrentVersion` / `UpdateFlowTests.ApplyFailed_ReturnsToReadyWithMessage` / `UpdateStagingTests.VerifyStaged_MismatchedSha_False` / `UpdateStagingTests.VerifyStaged_MissingZip_False` / `UpdateNoticePresenterTests.BuildText_IncludesFailureMessage` / `UpdateCheckerEntryPointTests.FinishCheckWithoutUpdate_ReturnsToIdle_AndAllowsRecheck` / `UpdateCheckerEntryPointTests.FinishCheckWithoutUpdate_DoesNotWipeTheAvailableCandidate` |
| 6 | **ZIP 全体 + SHA-256 検証**を維持 | 充足 | ダウンロード後に `.sha256` を取得し、ZIP ファイル全体の SHA-256 をバックグラウンド計算して hex 完全一致を要求 (`UpdateChecker.cs:257-285`)。staging 済みは起動前にも再検証 (`UpdateChecker.cs:677`)。更新スクリプトにも `-ExpectedSha256` を渡す (`UpdateChecker.cs:712` / `:768`) | `UpdateStagingTests.ComputeSha256_KnownVector_Matches` (SHA-256("abc") の既知ベクトル) / `UpdateStagingTests.VerifyStaged_MatchingSha_True` / `UpdateStagingTests.VerifyStaged_MismatchedSha_False` |
| 7 | **prerelease を stable に自動適用しない** | 充足 | チャンネルは `StreamingAssets/update-channel.txt` で決まり、既定は stable (`UpdateChannel.cs:43`)。stable では prerelease が候補から除外 (`UpdateChecker.cs:428` → `ParseLatestRelease` の `release.prerelease` 判定)。判定は純関数 `EvaluateCheck(..., includePrerelease, ...)` に集約 | `UpdateChannelTests.StableChannel_PrereleaseIsNotAnUpdateCandidate` / `UpdateChannelTests.PreviewChannel_PrereleaseIsAnUpdateCandidate` / `UpdateChannelTests.StableChannel_OnlyPrereleaseExists_ReturnsNull` / `UpdateCheckDecisionTests.EvaluateCheck_StableChannel_DoesNotOfferPrerelease` / `UpdateCheckDecisionTests.EvaluateCheck_PreviewChannel_OffersPrerelease` / `UpdateSourceStructureTests.EvaluateCheck_TakesChannelFlagExplicitly` |
| 8 | **展示中にゲームを勝手に止めない** | 充足 | プロセス終了 (`Application.Quit()`) の呼び出しは `UpdateChecker.cs:342` の **1 箇所だけ**で、`RequestApply()` (ユーザー操作) の中。`UpdateNoticeUI` は Title 画面以外では通知を出さないので、ゲームプレイ中に更新が割り込まない | `UpdateSourceStructureTests.ApplicationQuit_OnlyInsideUserReachableCode` (プロセス終了が「利用者操作の入口から到達できるコード」の中にしか無いことを検証) / `UpdateSourceStructureTests.UpdateChecker_ApplyEntries_AreCalledOnlyFromUserEntryPoints` / `UpdateNoticePresenterTests.ShouldShow_OnlyOnTitleScene` / `UpdateFlowTests.UpdateChecker_HasNoOnApplicationQuitHook` |

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

- 起動時の候補判定 (チャンネル / プラットフォーム / 新旧 / ZIP アセット) を
  `UpdateChecker.UpdateCheckDecision` 構造体 + `EvaluateCheck()` に切り出した。
  ネットワーク・ファイル IO 不要で EditMode から決定論的に検証できる。
- `CheckForUpdate()` はこの判定結果を使うだけになった (実装は 1 箇所に集約)。
- 副次的な修正 (本 PR の唯一の**機能的変更**): 確認に失敗した 3 経路
  (ビルド版識別子が解釈不能 / 通信失敗 / 新版なし) を
  `UpdateChecker.FinishCheckWithoutUpdate(reason, warning)` に集約し、
  状態機械が `Checking` のまま固まらないようにした (従来は `Checking` で停止していた)。
  `Checking` に固まると `UpdateFlowModel.StartCheck()` が no-op になり、
  将来「再チェック」機構を追加したときに確認できなくなるため。
  → `UpdateCheckerEntryPointTests.FinishCheckWithoutUpdate_ReturnsToIdle_AndAllowsRecheck`
  (3 経路を TestCase で 1 本に) と
  `UpdateSourceStructureTests.CheckForUpdate_HasThreeFailurePaths_ReturningToIdle`
  (production 側の 3 経路が同じ共通処理を通ること) の 2 本で固定。

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
- `UpdateCheckerTests` — バージョン判定・Release 選択・staging の SHA 検証は既知ベクトルで検証済み。
  `ParseLatestRelease` は「候補を選ぶ」までで、「起動時は候補を提示するだけで
  ダウンロードしない」という主張を担保するテストが無く、ZIP アセット込みの判定も未検証 → 追加。
- `UpdateFlowTests.UpdateChecker_PublicApplyEntries_AreExplicitOnly` は
  public メソッド名の接頭辞検査而已で、private なフックや
  「誰が呼んでいるか」は見ていなかった → ソース構造検査を追加。

追加したテスト (**45 件** / 内訳は下表):

| ファイル | 件数 | 内容 |
|---|---|---|
| `Assets/Tests/Editor/UpdateSourceStructureTests.cs` (新規) | 10 | ソース構造で「起動は確認だけ」「適用は利用者操作の入口からしか呼ばない」「`Application.Quit` は利用者操作から到達できる箇所だけ」「他スクリプトから更新入口を呼ばない」を固定 |
| `Assets/Tests/Editor/UpdateNoticePresenterTests.cs` (新規) | 14 | Title 限定表示・dismiss・操作の分岐・文言 |
| `Assets/Tests/Editor/UpdateCheckerEntryPointTests.cs` (新規) | 9 | 入口 (`RequestDownload` / `RequestApply` / `ApplyStagedUpdateNow`) が副作用を起こさないこと + 確認失敗 3 経路が Idle へ戻る |
| `Assets/Tests/Editor/UpdateCheckerTests.cs` (追記) | 12 | `EvaluateCheck` の判定 (チャンネル / プラットフォーム / 新旧 / draft / ZIP アセット) |

`UpdateSourceStructureTests` は「コメントと文字列リテラルを空白に置換してから
メソッド本体を取り出す」ことで、doc comment 内の同名識別子に惑わされずに
**実コード**を検査する (`UpdateSource` ヘルパ)。
禁止語は部分一致ではなく**呼び出し構文** (`Foo(`) で判定しており、
局所変数の名前が変わっただけでは落ちない (false positive の抑制)。

#### 構造テストを採っている理由 (代替できないため)

- 本命の振る舞い「起動したら自動で適用されない」は、PlayMode でアプリを起動して
  GitHub Releases API まで通信させないと再現できない。EditMode スイートでは
  通信も起きないため、振る舞いテストでは観測できない。
- 反射では private メソッドの**呼び出し元**を検査できない
  (例: `Awake()` に `RequestApply()` を足した混入を検出する手段が無い)。
- よって「適用の入口の呼び出しがどこにあるか」だけは静的に見るしかない。

**保証できる範囲の限界**: 保証するのは*このチェックアウトのソースツリー*上での
呼び出し位置であって、コンパイル済みアセンブリそのものではない
(`Library/` のキャッシュが古いと「テストは緑・バイナリは別物」の可能性が理論上ある)。
また文字列ベースの検査なので `nameof` やリフレクション経由の間接呼び出しは検出できない。
その補うため、更新の入口は public メソッドに限定し、
入口の数をシフトする反射テスト (`UpdateFlowTests.UpdateChecker_PublicApplyEntries_AreExplicitOnly`)
と 2 本で守っている。

#### 構造テスト自体の穴を塞ぐ (レビュー指摘 S1)

初版は `UpdateChecker.cs` 自身を走査対象から除外していたため、
`Awake()` に `RequestApply()` を混入させても全テストが green のままだった
(「UpdateChecker.cs 内の入口呼び出しは利用者操作の入口からしか行われない」を
検査する `UpdateChecker_ApplyEntries_AreCalledOnlyFromUserEntryPoints` を追加)。
許可範囲は「利用者操作の入口 (`RequestApply` / `ApplyStagedUpdateNow` / `RequestDownload`)
から呼び出しグラフで到達できる範囲」なので、終了処理をヘルパーに切り出しても落ちない。

**この検査が実際に効くことの確認 (negative control)**: `UpdateChecker.Awake()` に
一時的に `if (staged != null) { RequestApply(); }` を注入して EditMode を走らせたところ、
`UpdateChecker_ApplyEntries_AreCalledOnlyFromUserEntryPoints` だけが
failed になることを確認した (注入は取り除き、1118 件すべて green に戻した)。

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
| 変更前 (main 相当, 参考) | 1073 | 1073 | 0 | 0 |
| 変更後 (本 PR) | **1118** | **1118** | **0** | 0 |

内訳: 本 PR で新規追加したテスト **45 件** (構造 10 / Presenter 14 / EntryPoint 9 / Decision 12)
がすべて green。変更前 1073 件は 1 件も触っていない (すべて green のまま)。

検証内容: negative control (意図的な違反の注入) 1 回分のみ failed を確認した
(§3.3 末尾)。最終コミットでは 1118 / 1118 passed。

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
   これは「更新は明示式」の安全性とは矛盾しないが、**必要なら**別 Issue で扱うこと。
3. **v6 Title にはリテラルの「アップデートあり」が出ない** (cc の統合判断事項):
   v6 モードでは `V6UiMode.HideLegacyUpdateNotice()` (`Assets/Game/Scripts/UI/V6/V6UiMode.cs:137`)
   が `UpdateNoticeCanvas` を非表示にするため、`UpdateNoticeUI` のピル
   (「アップデートあり v… [F3: 今すぐ更新] (押さなければ更新しません)」) は出ない。
   代わりに v6 Title のボタンラベルは `V6TitleLogic.UpdateButtonLabel()` の
   「更新して再起動」 (`Assets/Game/Scripts/UI/V6/Title/V6TitleLogic.cs:189`) になる。
   したがって **二重表示は起きない** (ピルは隠れる) が、
   受入条件の文言「タイトル画面に『アップデートあり』表示」は
   **旧 UI (TitleScene) では成立し、v6 Title ではリテラルな文言としては成立していない**。
   v6 は他 Worker の領域のため本 PR では触っていない
   (v6 側でラベルを「アップデートあり (更新して再起動)」等に変えるか、
   受入条件側で「更新して再起動 + 新バージョン表示」を等価物として明文化するかは cc の判断)。
4. **dismiss 後は F3 が効かない**: `Dismiss()` すると `ResolveAction` が `None` になるため、
   同一セッション中は Title の F3 で適用できない (Option のボタンは別経路として残る)。
   意図した挙動だが文言上の案内は「× で隠す」だけ。
5. **`Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset`**: Unity の import 副作用で
   再生成差分が出る (本 PR には含めていない)。
6. **`EvaluateCheck` の「ビルド版識別子が解釈できない」分岐は production から到達不能**:
   呼び出し元 `CheckForUpdate` が通信前に `current.Valid` を判定して `yield break` する。
   この分岐は将来 `EvaluateCheck` を直接呼ぶ利用者向けの**防御ガード**として残し、
   docstring とテスト (`EvaluateCheck_InvalidCurrentBuild_IsNoUpdate_ByDefensiveGuard`) に
   「到達不能のガードである」旨を明記した。
   production 側の経路 (current.Valid 判定 → `FinishCheckWithoutUpdate` → Idle) は
   `UpdateSourceStructureTests.EvaluateCheck_InvalidBuildBranch_IsGuardedByTheCaller` と
   `UpdateCheckerEntryPointTests.FinishCheckWithoutUpdate_ReturnsToIdle_AndAllowsRecheck` が担保する。
