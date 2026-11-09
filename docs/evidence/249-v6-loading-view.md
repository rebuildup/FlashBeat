# #249 — v6 ローディング画面の映像面（PR #249 レビュー指摘 2 件の対応）

対象: `Assets/Game/Scripts/UI/V6/Game/Loading/V6LoadingView.cs`
起因: PR #249 への CodeRabbit レビュー指摘（`V6LoadingView.cs` の 2 件）

## 指摘 1: 警告フラグが毎フレームリセットされる

**内容**: `SetVisible(true)` が毎回 `videoErrorLogged = false` を代入していたため、
`Preparing` / `Failed` の間に複数フレームで映像エラーが通知されると、
同じ表示中に警告が繰り返されていた。

**対応**: `SetVisible` に表示状態を持たせ（`shown`）、**非表示 → 表示の遷移時だけ**
警告フラグと `videoErrored` を初期化し、映像の再生を開始する
（`SetVisible` は毎フレーム呼ばれるため、再生の開始・警告フラグのリセットは
transition 時のみ）。

## 指摘 2: 映像テクスチャが接続されておらず白画面になる

**内容**: `RawImage` に `RenderTexture` が割り当てられるだけで、
**`RenderTexture.Create()` が一度も呼ばれていなかった**。
未作成の RenderTexture を表示するため、ローディング中は
**画面全体が白い**状態になっていた。
（`OnDestroy` の `videoTexture.Release()` も、未作成のテクスチャに対する
呼び出しになるため例外になり得ると同時に、テクスチャが本当に使われていない
ことも示していた。）

**対応**:
1. 映像面の状態判断を純ロジックとして `V6LoadingVideoPolicy.Decide` に抽出
   （入力: 表示中か / clip 有無 / 準備完了か / エラー有無 → 出力: `Video` / `Background` / `Hidden`）。
2. `Video` のときだけ `RenderTexture.Create()` → `videoPlayer.targetTexture` 設定 →
   `RawImage.texture` 設定 を行い、`RawImage` を有効化する。
3. clip が無い / 準備できていない / エラー のいずれかでは `Background` を選び、
   **`RawImage` を無効化**して白画面を出さない（シーン既存の背景だけが見える）。
4. 非表示 (`Hidden`) ではテクスチャを保持しない。
5. 非表示になった時に `StopVideo()` で `VideoPlayer` を止め、
   `targetTexture` と `RawImage.texture` を解放する（裏で再生し続ける /
   古いテクスチャを保持したままにするのを防ぐ）。
6. `Release()` は `IsCreated()` が真のときだけ呼ぶ（未作成テクスチャでの例外を防ぐ）。

## テスト

`Assets/Game/Tests/V6LoadingViewTests.cs`（新規、`FlashBeat.V5CanvasTests` アセンブリ）
— `V6LoadingVideoPolicy.Decide` の純ロジックを検証:

| テスト | 入力 | 期待 |
|---|---|---|
| `Video_IsShownWhenClipReadyAndNoError` | 表示中 / clip あり / 準備完了 / エラーなし | `Video` |
| `Background_IsShownWhenNoClip` | 表示中 / clip なし | `Background` |
| `Background_IsShownWhenNotPreparedYet` | 表示中 / clip あり / 未準備 | `Background` |
| `Background_IsShownOnVideoError` | 表示中 / clip あり / 準備完了 / エラー | `Background` |
| `Hidden_IsShownWhenViewIsNotVisible` | 非表示（各組み合わせ） | `Hidden` |

`VideoPlayer` を実時間で動かす必要がないため EditMode で決定的に検証できる。

## 検証

- `script/compile-check.sh --project <worktree> --assembly flashbeat` → **Build succeeded**
- `script/compile-check.sh --project <worktree> --assembly tests` → **Build succeeded**
  （生成 DLL に `V6LoadingVideoPolicy` と `V6LoadingViewTests` の両方が含まれることを確認）
- Unity EditMode スイート: **監督側で統合後に実行**（1057/1057 が基準）

## 未検証（実機が必要）

- **実際に画面に映像が出るか**: `VideoPlayer` の準備完了は非同期であり、
  EditMode では `VideoPlayer` を実動できない。ローディング画面での映像表示・
  背景フォールバックは **実機（Windows 展示機）で未証明**。
- 映像の音声出力（`VideoAudioOutputMode.None` のままか）も実機確認が必要。

Co-Authored-By: Claude Code <noreply@anthropic.com>
