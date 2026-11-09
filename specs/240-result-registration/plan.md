# Plan: 240 — v6 Result のローカル登録契約の復元

## 変更の骨格

```
V6ResultLogic (純ロジック)          V6ResultRegistration.cs (enum + store)
  - IsSubmittable(isRemote,         - enum V6RegistrationMode { Online, Local }
      remoteId, chartVersion)          - V6LocalNameStore.Load/Save/Clear
  - RegistrationMode(...)                  (PlayerPrefs "V5PlayerName")
  - NormalizeLocalName(raw)
  - LocalSaveMessage(savedName)
  - InitialName(mode, online, local)
  - ResolveChartVersion(pinned, live)
          ▲                    ▲
          │                    │
   V6ResultView (画面)     V6ResultRegistrationTests (EditMode)
     - OpenModal: 方式確定 + 初期値
     - TrySubmit: Local → SaveLocalName / Online → 既存の送信
     - SubmitCoroutine: pinned chartVersion / 送信不可ならローカル保存
     - SaveLocalName: V5LocalNameStore.Save + ローカル結果メッセージ
     - SongOf(): GManager.GetSong (固定版を優先)
```

## 手順

1. **`V6ResultLogic` に純ロジックを追加** (`Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs`)
   - 方式判定 (`IsSubmittable` / `RegistrationMode`)、ローカル名の正規化
     (`NormalizeLocalName`)、ローカル結果メッセージ (`LocalSaveMessage`)、
     初期値 (`InitialName`)、譜面版の解決 (`ResolveChartVersion`)。
   - ローカル名の定数 (`LocalPlayerNamePrefsKey` / `LocalNameMaxLength` /
     `LocalNameDefault`) もここに置く。
   - 既存の `ClassifySubmitResult` / `SubmitStatusMessage` は **変更しない**
     (オンライン 4 区分を維持)。
2. **`V6ResultRegistration.cs` を新規作成**
   - `enum V6RegistrationMode` と `V6LocalNameStore` (PlayerPrefs 読み書きのみ)。
   - ロジックは持たせない (純ロジックは `V6ResultLogic`)。
3. **`V6ResultView` の配線** (`Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs`)
   - `OpenModal()`: 方式を確定し、初期値を `InitialName` で決める。
   - `TrySubmit()`: ローカル方式なら `SaveLocalName()` だけ。お在线方式は既存フロー。
   - `SubmitCoroutine()`: `ResolveChartVersion` で pinned 版を使い、送信不可なら
     `OnSubmitResult(null)` (=「オフライン」) ではなく `SaveLocalName()`。
   - `SaveLocalName()`: v5 と同じ規則で正規化 → `V6LocalNameStore.Save` →
     ローカル結果メッセージを表示。
   - `SongOf()`: `GManager.GetSong` を優先 (固定版を使う)。
   - `StartRankingFetch()`: 取得する譜面版も pinned 版にそろえる。
   - 入力の文字数上限を方式で切替 (ローカル 8 / オンライン 16)、ローカルでは
     v5 と同じく A-Z0-9 のみ許可。
   - 古いコメント (`:615` 付近) を実装に合わせて更新。
4. **EditMode テスト** (`Assets/Game/Tests/V6ResultRegistrationTests.cs`, 新規)
   - ローカル保存が `"V5PlayerName"` に書き込まれる / ローカル語彙に
     オンライン語彙が含まれない / 初期値 / pinned 版の解決 / オンライン 4 区分。
   - 通信はしない (純ロジック + PlayerPrefs のみ)。
5. **compile-check** (`--assembly flashbeat` と `--assembly tests`)。
6. **spec / evidence の作成とコミット**。

## 検証方法

- `script/compile-check.sh --project /Users/rebui/work/FlashBeat.w240 --assembly flashbeat`
- `script/compile-check.sh --project /Users/rebui/work/FlashBeat.w240 --assembly tests`
- EditMode テストの **実行** は Supervisor が統合時に行う (Unity Editor 起動禁止)。
