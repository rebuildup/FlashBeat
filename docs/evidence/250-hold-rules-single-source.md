# HOLD の判定ルールを単一の出所にする (Issue #250)

対象:
- `Assets/Game/Scripts/Gameplay/LongNote.cs`
- `Assets/Game/Scripts/Gameplay/JudgeQueue.cs`
- `Assets/Tests/Editor/JudgeQueueTests.cs`

## 問題

HOLD（ロングノーツ）の判定ルールが **2 つ** に存在していた。

| 場所 | 状態 |
|---|---|
| `LongNote.JudgeRelease` / `LongNote.IsBroken` / `LongNote.IsHolding` / `LongNote.Succeeded` | **実行時デッド**。`LongNoteTests` だけが呼んでいた |
| `JudgeQueue.UpdateHold` | **実際の実行経路**（`Judge.Update` → `JudgeQueue.UpdateHold`）。同じルールをインラインで再実装 |

結果として、`LongNoteTests` が緑でも **実挙動は壊れている可能性**があった
（テストと実行時経路の分離）。これは Issue #59 の受入確認で指摘された欠陥。

## 対応

### 1. 判定ルールを `LongNote` に集約（単一の出所）

`LongNote` に実機経路が呼べる形にした:

- `JudgeRelease(float releaseTime, float endSeconds, float greatWindow)` —
  判定幅を明示できる版を追加（既定幅版は互換ラッパに）。
- `IsBroken(float releaseTime, float endSeconds, float greatWindow)` —
  判定幅を明示できる版を追加。
- `IsHoldExpired(float mediaTime, float endSeconds, float greatWindow)` —
  「押しっぱなしで終端 + 許容幅をすぎた」判定（`Expired` 相当）を追加。

### 2. `JudgeQueue.UpdateHold` を `LongNote` の判定ロジック に委譲

変更前（インライン再実装）:

```csharp
if (!keyDown)
{
    holding[lane] = null;
    entry = hold;
    return Mathf.Abs(mediaTime - hold.EndTime) <= greatWindow
        ? HoldResult.Released
        : HoldResult.Broken;
}
if (mediaTime > hold.EndTime + greatWindow)
{
    holding[lane] = null;
    entry = hold;
    return HoldResult.Expired;
}
```

変更後:

```csharp
if (!keyDown)
{
    holding[lane] = null;
    entry = hold;
    return LongNote.IsBroken(mediaTime, hold.EndTime, greatWindow)
        ? HoldResult.Broken
        : HoldResult.Released;
}
if (LongNote.IsHoldExpired(mediaTime, hold.EndTime, greatWindow))
{
    holding[lane] = null;
    entry = hold;
    return HoldResult.Expired;
}
```

### 3. 挙動を変えないことの確認

**スコアの付け方・判定幅・押し直し救済の有無は一切変えていない。**

| 事象 | 変更前 | 変更後 |
|---|---|---|
| 終端 ±0.15 秒で離す | `Released`（加点なし） | 同じ |
| 終端より早く離す | `Broken` → `DowngradeHoldToBad` | 同じ |
| **終端より遅く離す** | `Broken` | 同じ |
| 押しっぱなしで終端 + 0.15 秒をすぎる | `Expired`（成功扱い） | 同じ |
| 押し直しで救済 | されない | されない |
| 保持中のノーツが Miss になる | ならない | ならない |

`LongNote.IsBroken` の定義は **旧: 「終端より前だけ」** から
**新: 「終端 ± 判定幅の外」** に変わった。
これは (a) 実行時経路の実挙動（遅く離すと `Broken`）と一致させるための変更であり、
(b) 既存の `LongNoteTests` の期待値（`IsBroken(3.5f, 4.0f)` = true、
`IsBroken(3.9f, 4.0f)` = false、`IsBroken(4.0f, 4.0f)` = false）と矛盾しない。
**判定・スコアの数値は変わっていない。**

## テスト

`Assets/Tests/Editor/JudgeQueueTests.cs` に 3 件追加
（いずれも **実行時経路** を駆動する — ヘルパ直接呼び出しではない）:

| テスト | 内容 |
|---|---|
| `Hold_RuntimeResult_MatchesLongNoteRules` | 離し時刻を 5.5 / 6.7 / 6.86 / 7.0 / 7.1 / 7.14 / 7.5 で走らせ、`UpdateHold` の結果が `LongNote.IsBroken` と `LongNote.JudgeRelease` の双方と一致することを確認 |
| `Hold_ExpiryBoundary_MatchesLongNoteRule` | 押しっぱなし的情形で `UpdateHold` の結果が `LongNote.IsHoldExpired` と一致することを確認（7.0 / 7.1 / 7.14 / 7.2 / 7.5） |
| `Hold_NeverPressed_BecomesMissAndNotHold` | 一度も押されない HOLD は保持に入らず、時間切れで Miss になることを確認 |

既存テストは**すべてそのまま**残している（変更していない）。

## 検証

- `script/compile-check.sh --project <worktree> --assembly flashbeat` → **Build succeeded**
- `FlashBeat.Tests` アセンブリ（`Assets/Tests/Editor` は compile-check の対象外）の
  手書き csproj ビルド → **Build succeeded**。
  生成 DLL に追加 3 テストが含まれることを確認:
  `Hold_RuntimeResult_MatchesLongNoteRules`, `Hold_ExpiryBoundary_MatchesLongNoteRule`,
  `Hold_NeverPressed_BecomesMissAndNotHold`
- Unity EditMode スイート: **監督側で統合後に実行**（基準 1057/1057）

## 未検証（実機が必要）

- **出荷済みの譜面に HOLD が 1 音符も存在しない**
  （`Assets/Tests/Editor/LongNoteTests.cs` は「出荷譜面のノーツはすべて type 1」を断定している）。
  つまり HOLD は ** 再生可能なコンテンツで一度も実行されていない**。
  実機での保持・判定・表示は **未証明**。
- 実入力（キーを実際に押しっぱなしにする／途中で離す）を通した確認は実機必須。
  EditMode では時刻を明示的に与える擬似入力のみ。

## 派生する未解決の論点（**本 issue では実装しない**）

- `Judge.cs:118` の Broken 経路が `message(2, …)` を呼ぶため、
  `GManager.nextNotes` が **同一ノーツで 2 回** 進みうる（採点自体は二重にならない）。
  実挙動への影響は未確認。
- NoteEditor は HOLD を編集できない（`Assets/NoteEditor/Scripts/DTO/MusicDTO.cs` に `endNum` なし）。
  HOLD を作るには Web Studio から publish するしかない。

Co-Authored-By: Claude Code <noreply@anthropic.com>
